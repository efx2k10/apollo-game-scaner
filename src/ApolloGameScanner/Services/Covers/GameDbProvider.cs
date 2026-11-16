using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ApolloGameScanner.Models;

namespace ApolloGameScanner.Services.Covers;

/// <summary>
/// Обложки из открытого каталога LizardByte GameDB (IGDB) — тот же источник,
/// который использует Apollo в своём поиске обложек.
/// Ключ не нужен. Индекс — buckets/&lt;префикс&gt;.json, карточка — games/&lt;id&gt;.json.
/// </summary>
public sealed class GameDbProvider : ICoverProvider
{
    public const string DisplayName = "GameDB (IGDB)";

    private const string RawBaseUrl = "https://raw.githubusercontent.com/LizardByte/GameDB/gh-pages";
    private const string SiteBaseUrl = "https://app.lizardbyte.dev/GameDB";
    private const string ImageBaseUrl = "https://images.igdb.com/igdb/image/upload";
    private const int MaxGames = 12;
    private const int MaxBucketCacheEntries = 8;
    private const int MaxCoverCacheEntries = 1000;

    private static readonly TimeSpan BucketCacheLifetime = TimeSpan.FromHours(6);
    private static readonly TimeSpan NegativeCacheLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CoverCacheLifetime = TimeSpan.FromHours(24);

    private static readonly ConcurrentDictionary<string, Cached<IReadOnlyList<BucketEntry>>> BucketCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, Cached<string?>> CoverCache =
        new(StringComparer.Ordinal);

    public string Name => DisplayName;

    public async Task<IReadOnlyList<CoverResult>> SearchAsync(string query, CancellationToken token)
    {
        var name = (query ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return Array.Empty<CoverResult>();
        }

        var normalized = Normalize(name);
        if (normalized.Length == 0)
        {
            return Array.Empty<CoverResult>();
        }

        var entries = await LoadBucketAsync(name, token);
        if (entries.Count == 0)
        {
            return Array.Empty<CoverResult>();
        }

        var matches = FindMatches(entries, name, normalized);
        if (matches.Count == 0)
        {
            return Array.Empty<CoverResult>();
        }

        using var throttle = new SemaphoreSlim(4, 4);
        var tasks = matches.Select(async entry =>
        {
            await throttle.WaitAsync(token);
            try
            {
                var slug = await GetCoverSlugAsync(entry.Id, token);
                if (slug is null)
                {
                    return null;
                }

                return new CoverResult
                {
                    Source = DisplayName,
                    Title = entry.Name,
                    // Apollo сохраняет t_cover_big_2x PNG, а показывает t_cover_big JPG.
                    ThumbUrl = $"{ImageBaseUrl}/t_cover_big/{slug}.jpg",
                    FullUrl = $"{ImageBaseUrl}/t_cover_big_2x/{slug}.png",
                    // GameDB не отдаёт размеры обложки; точные размеры покажет превью.
                    Width = 0,
                    Height = 0,
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Одна битая карточка не должна ломать весь поиск.
                return null;
            }
            finally
            {
                throttle.Release();
            }
        }).ToList();

        var results = await Task.WhenAll(tasks);
        return results.OfType<CoverResult>().ToList();
    }

    // ------------------------------------------------------------------ поиск

    private static IReadOnlyList<BucketEntry> FindMatches(IReadOnlyList<BucketEntry> entries, string query,
        string normalized)
    {
        var matches = Rank(entries, normalized);
        if (matches.Count > 0)
        {
            return matches;
        }

        // Папки часто называются «Battlefield V (2018)» или «The Witcher 3 Wild Hunt».
        // Если полное название не нашлось, пробуем первые два слова, затем первое слово.
        var words = Tokenize(query);
        if (words.Count > 1)
        {
            matches = Rank(entries, string.Concat(words.Take(2)));
        }

        if (matches.Count == 0 && words.Count > 0 && words[0].Length >= 4)
        {
            matches = Rank(entries, words[0]);
        }

        return matches;
    }

    private static IReadOnlyList<BucketEntry> Rank(IReadOnlyList<BucketEntry> entries, string search)
    {
        return entries
            .Select(entry => new
            {
                Entry = entry,
                Rank = GetRank(entry.Normalized, search),
            })
            .Where(item => item.Rank < int.MaxValue)
            .OrderBy(item => item.Rank)
            .ThenBy(item => item.Entry.Normalized.Length)
            .ThenBy(item => item.Entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxGames)
            .Select(item => item.Entry)
            .ToList();
    }

    private static int GetRank(string candidate, string query)
    {
        if (candidate.Length == 0 || query.Length == 0)
        {
            return int.MaxValue;
        }

        if (candidate.Equals(query, StringComparison.Ordinal))
        {
            return 0;
        }

        if (candidate.StartsWith(query, StringComparison.Ordinal))
        {
            return 1;
        }

        if (candidate.Contains(query, StringComparison.Ordinal))
        {
            return 2;
        }

        return int.MaxValue;
    }

    private static List<string> Tokenize(string value)
    {
        var words = new List<string>();
        var builder = new StringBuilder();

        foreach (var ch in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                continue;
            }

            if (builder.Length > 0)
            {
                words.Add(builder.ToString());
                builder.Clear();
            }
        }

        if (builder.Length > 0)
        {
            words.Add(builder.ToString());
        }

        return words;
    }

    // ------------------------------------------------------------------ GameDB

    private static async Task<IReadOnlyList<BucketEntry>> LoadBucketAsync(string name, CancellationToken token)
    {
        foreach (var bucket in GetBucketCandidates(name))
        {
            if (BucketCache.TryGetValue(bucket, out var cached) && cached.IsFresh)
            {
                return cached.Value;
            }

            var text = await GetTextAsync($"/buckets/{bucket}.json", token);
            if (text is null)
            {
                BucketCache[bucket] = new Cached<IReadOnlyList<BucketEntry>>(
                    Array.Empty<BucketEntry>(), NegativeCacheLifetime);
                TrimCache(BucketCache, MaxBucketCacheEntries);
                continue;
            }

            var entries = ParseBucket(text);
            BucketCache[bucket] = new Cached<IReadOnlyList<BucketEntry>>(entries, BucketCacheLifetime);
            TrimCache(BucketCache, MaxBucketCacheEntries);
            return entries;
        }

        return Array.Empty<BucketEntry>();
    }

    private static IEnumerable<string> GetBucketCandidates(string name)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Точно как в Apollo: первые два символа исходного названия, только a-z0-9.
        var primary = GetBucket(name);
        if (seen.Add(primary))
        {
            yield return primary;
        }

        // Индекс GameDB строится по первым двум Unicode-символам. Для названий
        // с пунктуацией в начале пробуем первые два буквенно-цифровых символа.
        var fromAlphanumeric = GetBucketFromAlphanumeric(name);
        if (seen.Add(fromAlphanumeric))
        {
            yield return fromAlphanumeric;
        }

        var firstLetter = GetBucket(name, 1);
        if (seen.Add(firstLetter))
        {
            yield return firstLetter;
        }
    }

    private static string GetBucket(string name) => GetBucket(name, 2);

    private static string GetBucket(string name, int length)
    {
        var prefix = name.Length <= length ? name : name[..length];
        var bucket = new string(prefix
            .ToLowerInvariant()
            .Where(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            .ToArray());

        return bucket.Length == 0 ? "@" : bucket;
    }

    private static string GetBucketFromAlphanumeric(string name)
    {
        var prefix = new string(name
            .ToLowerInvariant()
            .Where(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            .Take(2)
            .ToArray());

        return prefix.Length == 0 ? "@" : prefix;
    }

    private static IReadOnlyList<BucketEntry> ParseBucket(string text)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("GameDB вернул некорректный индекс.", ex);
        }

        if (node is not JsonObject root)
        {
            return Array.Empty<BucketEntry>();
        }

        var entries = new List<BucketEntry>(root.Count);
        foreach (var pair in root)
        {
            if (pair.Value is not JsonObject item)
            {
                continue;
            }

            var name = JsonUtil.GetString(item["name"]);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var normalized = Normalize(name);
            if (normalized.Length == 0)
            {
                continue;
            }

            entries.Add(new BucketEntry(pair.Key, name, normalized));
        }

        return entries;
    }

    private static async Task<string?> GetCoverSlugAsync(string id, CancellationToken token)
    {
        if (CoverCache.TryGetValue(id, out var cached) && cached.IsFresh)
        {
            return cached.Value;
        }

        var text = await GetTextAsync($"/games/{Uri.EscapeDataString(id)}.json", token);
        var slug = text is null ? null : ParseCoverSlug(text);
        CoverCache[id] = new Cached<string?>(slug, CoverCacheLifetime);
        TrimCache(CoverCache, MaxCoverCacheEntries);
        return slug;
    }

    private static string? ParseCoverSlug(string text)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }

        if (node is not JsonObject game || game["cover"] is not JsonObject cover)
        {
            return null;
        }

        var url = JsonUtil.GetString(cover["url"]);
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var path = url.Split('?', '#')[0];
        var slash = path.LastIndexOf('/');
        if (slash < 0 || slash == path.Length - 1)
        {
            return null;
        }

        var fileName = path[(slash + 1)..];
        var dot = fileName.LastIndexOf('.');
        if (dot <= 0)
        {
            return null;
        }

        var slug = fileName[..dot];
        return string.IsNullOrWhiteSpace(slug) ? null : slug;
    }

    private static async Task<string?> GetTextAsync(string relativePath, CancellationToken token)
    {
        Exception? lastError = null;

        foreach (var baseUrl in new[] { RawBaseUrl, SiteBaseUrl })
        {
            try
            {
                using var response = await CoverService.Http.GetAsync(baseUrl + relativePath, token);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"GameDB ответил {(int)response.StatusCode} {response.ReasonPhrase}.");
                }

                return await response.Content.ReadAsStringAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
            {
                lastError = ex;
            }
        }

        throw new HttpRequestException(
            "Не удалось связаться с GameDB. Проверьте подключение к интернету.", lastError);
    }

    // ------------------------------------------------------------------ утилиты

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static void TrimCache<TKey, TValue>(ConcurrentDictionary<TKey, Cached<TValue>> cache, int max)
        where TKey : notnull
    {
        if (cache.Count <= max)
        {
            return;
        }

        foreach (var pair in cache)
        {
            if (cache.Count <= max)
            {
                break;
            }

            cache.TryRemove(pair.Key, out _);
        }
    }

    private sealed record BucketEntry(string Id, string Name, string Normalized);

    private sealed class Cached<T>
    {
        public T Value { get; }
        private DateTimeOffset ExpiresAt { get; }

        public Cached(T value, TimeSpan lifetime)
        {
            Value = value;
            ExpiresAt = DateTimeOffset.UtcNow + lifetime;
        }

        public bool IsFresh => DateTimeOffset.UtcNow < ExpiresAt;
    }
}
