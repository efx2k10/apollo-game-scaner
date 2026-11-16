using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ApolloGameScanner.Models;

namespace ApolloGameScanner.Services.Covers;

/// <summary>
/// Неофициальный поиск картинок через image-выдачу DuckDuckGo.
/// Может перестать работать в любой момент — это запасной источник.
/// </summary>
public sealed class DuckDuckGoProvider : ICoverProvider
{
    private static readonly Regex VqdRegex = new(@"vqd\s*=\s*[""']?([\d-]+)", RegexOptions.Compiled);

    public string Name => "DuckDuckGo";

    public async Task<IReadOnlyList<CoverResult>> SearchAsync(string query, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<CoverResult>();
        }

        var vqd = await GetVqdAsync(query.Trim(), token);
        if (string.IsNullOrEmpty(vqd))
        {
            throw new InvalidOperationException(
                "DuckDuckGo не вернул токен поиска. Источник неофициальный и иногда недоступен — попробуйте позже.");
        }

        var url = "https://duckduckgo.com/i.js?l=us-en&o=json&f=,,,&p=1" +
                  "&q=" + Uri.EscapeDataString(query.Trim()) +
                  "&vqd=" + Uri.EscapeDataString(vqd);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Referrer = new Uri("https://duckduckgo.com/");

        using var response = await CoverService.Http.SendAsync(request, token);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"DuckDuckGo ответил {(int)response.StatusCode}. Источник неофициальный, попробуйте позже.");
        }

        var text = await response.Content.ReadAsStringAsync(token);
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch
        {
            throw new InvalidOperationException("DuckDuckGo вернул неожиданный ответ.");
        }

        if (node is not JsonObject obj || obj["results"] is not JsonArray resultsArray)
        {
            return Array.Empty<CoverResult>();
        }

        var results = new List<CoverResult>();
        foreach (var item in resultsArray)
        {
            if (item is not JsonObject image)
            {
                continue;
            }

            var imageUrl = image["image"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(imageUrl) ||
                !imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            results.Add(new CoverResult
            {
                Source = Name,
                Title = image["title"]?.GetValue<string>() ?? query,
                FullUrl = imageUrl,
                ThumbUrl = image["thumbnail"]?.GetValue<string>() ?? imageUrl,
                Width = ReadInt(image["width"]),
                Height = ReadInt(image["height"]),
            });

            if (results.Count >= 30)
            {
                break;
            }
        }

        return results;
    }

    private static async Task<string?> GetVqdAsync(string query, CancellationToken token)
    {
        var url = "https://duckduckgo.com/?q=" + Uri.EscapeDataString(query) + "&iax=images&ia=images";
        var html = await CoverService.Http.GetStringAsync(url, token);
        var match = VqdRegex.Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static int ReadInt(JsonNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        try
        {
            return node.GetValue<int>();
        }
        catch
        {
            return 0;
        }
    }
}
