using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using ApolloGameScanner.Models;

namespace ApolloGameScanner.Services.Covers;

/// <summary>Поиск обложек через официальный API SteamGridDB.</summary>
public sealed class SteamGridDbProvider : ICoverProvider
{
    private const string BaseUrl = "https://www.steamgriddb.com/api/v2";

    private readonly string _apiKey;

    public SteamGridDbProvider(string apiKey)
    {
        _apiKey = apiKey ?? string.Empty;
    }

    public string Name => "SteamGridDB";

    public async Task<IReadOnlyList<CoverResult>> SearchAsync(string query, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException(
                "Ключ SteamGridDB не задан. Откройте «Настройки» и вставьте API-ключ.");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<CoverResult>();
        }

        var gamesNode = await GetAsync($"{BaseUrl}/search/autocomplete/{Uri.EscapeDataString(query.Trim())}", token);
        var games = ExtractData(gamesNode);
        if (games.Count == 0)
        {
            return Array.Empty<CoverResult>();
        }

        var results = new List<CoverResult>();

        foreach (var game in games.Take(4))
        {
            if (game is not JsonObject gameObject)
            {
                continue;
            }

            if (!gameObject.TryGetPropertyValue("id", out var idNode) || idNode is null)
            {
                continue;
            }

            int id;
            try
            {
                id = idNode.GetValue<int>();
            }
            catch
            {
                continue;
            }

            var gameName = gameObject["name"]?.GetValue<string>() ?? query;

            var grids = await FetchGridsAsync(id, gameName, useDimensions: true, token);
            if (grids.Count == 0)
            {
                grids = await FetchGridsAsync(id, gameName, useDimensions: false, token);
            }

            results.AddRange(grids);
            if (results.Count >= 40)
            {
                break;
            }
        }

        return results;
    }

    private async Task<List<CoverResult>> FetchGridsAsync(int gameId, string gameName, bool useDimensions,
        CancellationToken token)
    {
        var url = $"{BaseUrl}/grids/game/{gameId}?types=static&nsfw=false&humor=false";
        if (useDimensions)
        {
            url += "&dimensions=600x900,342x482,660x930";
        }

        var node = await GetAsync(url, token);
        var data = ExtractData(node);
        var results = new List<CoverResult>();

        foreach (var item in data)
        {
            if (item is not JsonObject grid)
            {
                continue;
            }

            var fullUrl = grid["url"]?.GetValue<string>();
            if (string.IsNullOrEmpty(fullUrl))
            {
                continue;
            }

            var thumbUrl = grid["thumb"]?.GetValue<string>() ?? fullUrl;
            var width = ReadInt(grid["width"]);
            var height = ReadInt(grid["height"]);

            results.Add(new CoverResult
            {
                Source = Name,
                Title = gameName,
                FullUrl = fullUrl,
                ThumbUrl = thumbUrl,
                Width = width,
                Height = height,
            });
        }

        return results;
    }

    private async Task<JsonNode?> GetAsync(string url, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await CoverService.Http.SendAsync(request, token);
        var text = await response.Content.ReadAsStringAsync(token);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"SteamGridDB ответил {(int)response.StatusCode} {response.ReasonPhrase}. " +
                "Проверьте API-ключ и повторите попытку.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch
        {
            return null;
        }
    }

    private static JsonArray ExtractData(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return new JsonArray();
        }

        if (!JsonUtil.GetBool(obj["success"], true))
        {
            return new JsonArray();
        }

        return obj["data"] as JsonArray ?? new JsonArray();
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
            try
            {
                return (int)node.GetValue<double>();
            }
            catch
            {
                return 0;
            }
        }
    }
}
