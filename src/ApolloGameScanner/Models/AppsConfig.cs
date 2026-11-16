using System.Text.Json;
using System.Text.Json.Nodes;
using ApolloGameScanner.Services;

namespace ApolloGameScanner.Models;

/// <summary>Корневой объект apps.json: version, env, apps + сохранение неизвестных полей.</summary>
public sealed class AppsConfig
{
    public int Version { get; set; } = 2;
    public bool HadVersion { get; set; }
    public JsonObject? Env { get; set; }
    public List<AppEntry> Apps { get; } = new();
    public Dictionary<string, JsonNode?> RootExtra { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> KnownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "version", "env", "apps",
    };

    public static AppsConfig FromJson(JsonObject root)
    {
        var config = new AppsConfig();

        if (root["version"] is JsonNode versionNode)
        {
            config.HadVersion = true;
            config.Version = JsonUtil.GetInt(versionNode, 2);
        }

        if (root["env"] is JsonObject env)
        {
            config.Env = (JsonObject)env.DeepClone();
        }

        if (root["apps"] is JsonArray apps)
        {
            foreach (var item in apps)
            {
                if (item is JsonObject appObject)
                {
                    config.Apps.Add(AppEntry.FromJson(appObject));
                }
            }
        }

        foreach (var pair in root)
        {
            if (!KnownKeys.Contains(pair.Key))
            {
                config.RootExtra[pair.Key] = pair.Value?.DeepClone();
            }
        }

        return config;
    }

    public JsonObject ToJson()
    {
        var root = new JsonObject
        {
            ["version"] = 2,
        };

        if (Env is not null)
        {
            root["env"] = Env.DeepClone();
        }

        var apps = new JsonArray();
        foreach (var app in Apps)
        {
            apps.Add(app.ToJson());
        }

        root["apps"] = apps;

        foreach (var pair in RootExtra)
        {
            root[pair.Key] = pair.Value?.DeepClone();
        }

        return root;
    }

    public string Serialize() => ToJson().ToJsonString(JsonUtil.Options);

    public static AppsConfig Parse(string text)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Файл не является корректным JSON: " + ex.Message, ex);
        }

        if (node is not JsonObject root)
        {
            throw new InvalidDataException("Корневой элемент apps.json должен быть объектом.");
        }

        return FromJson(root);
    }
}
