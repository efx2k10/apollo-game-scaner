using System.Text.Json.Nodes;
using ApolloGameScanner.Services;

namespace ApolloGameScanner.Models;

/// <summary>Запись из массивов prep-cmd / state-cmd.</summary>
public sealed class AppCommand
{
    public string Do { get; set; } = string.Empty;
    public string Undo { get; set; } = string.Empty;
    public bool Elevated { get; set; }

    public Dictionary<string, JsonNode?> Extra { get; } = new();

    private static readonly HashSet<string> KnownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "do", "undo", "elevated",
    };

    public static AppCommand FromJson(JsonObject obj)
    {
        var command = new AppCommand
        {
            Do = JsonUtil.GetString(obj["do"]) ?? string.Empty,
            Undo = JsonUtil.GetString(obj["undo"]) ?? string.Empty,
            Elevated = JsonUtil.GetBool(obj["elevated"]),
        };

        foreach (var pair in obj)
        {
            if (!KnownKeys.Contains(pair.Key))
            {
                command.Extra[pair.Key] = pair.Value?.DeepClone();
            }
        }

        return command;
    }

    public JsonObject ToJson()
    {
        var obj = new JsonObject();
        if (Do.Length > 0)
        {
            obj["do"] = Do;
        }

        if (Undo.Length > 0)
        {
            obj["undo"] = Undo;
        }

        if (Elevated)
        {
            obj["elevated"] = true;
        }

        foreach (var pair in Extra)
        {
            obj[pair.Key] = pair.Value?.DeepClone();
        }

        return obj;
    }

    public AppCommand Clone() => FromJson(ToJson());
}
