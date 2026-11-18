using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ApolloGameScanner.Models;

namespace ApolloGameScanner.Services;

/// <summary>
/// Архив выключенных записей: <c>apps.disabled.json</c> рядом с <c>apps.json</c>.
/// Apollo о выключенных играх не знает — их просто нет в основном конфиге.
/// </summary>
public sealed class DisabledAppsService
{
    private const int CurrentVersion = 1;

    /// <summary>Путь к архиву для указанного конфига: apps.json → apps.disabled.json.</summary>
    public static string GetSidecarPath(string configPath)
    {
        var fullPath = Path.GetFullPath(configPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("Не удалось определить папку конфига.");
        }

        var name = Path.GetFileNameWithoutExtension(fullPath);
        return Path.Combine(directory, name + ".disabled.json");
    }

    public List<DisabledApp> Load(string sidecarPath)
    {
        var result = new List<DisabledApp>();
        if (!File.Exists(sidecarPath))
        {
            return result;
        }

        var text = File.ReadAllText(sidecarPath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Файл " + Path.GetFileName(sidecarPath) + " не является корректным JSON: " + ex.Message, ex);
        }

        if (node is not JsonObject root || root["apps"] is not JsonArray apps)
        {
            return result;
        }

        foreach (var item in apps)
        {
            if (item is not JsonObject wrapper || wrapper["app"] is not JsonObject app)
            {
                continue;
            }

            result.Add(new DisabledApp
            {
                Entry = AppEntry.FromJson(app),
                Order = JsonUtil.GetInt(wrapper["order"], result.Count),
            });
        }

        return result;
    }

    public void Save(string sidecarPath, IReadOnlyList<DisabledApp> items)
    {
        if (items.Count == 0)
        {
            TryDelete(sidecarPath);
            return;
        }

        var apps = new JsonArray();
        foreach (var item in items)
        {
            apps.Add(new JsonObject
            {
                ["order"] = item.Order,
                ["app"] = item.Entry.ToJson(),
            });
        }

        var root = new JsonObject
        {
            ["version"] = CurrentVersion,
            ["apps"] = apps,
        };

        var text = root.ToJsonString(JsonUtil.Options);
        var fullPath = Path.GetFullPath(sidecarPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("Не удалось определить папку для архива выключенных игр.");
        }

        Directory.CreateDirectory(directory);
        var temp = fullPath + ".tmp";
        File.WriteAllText(temp, text, new UTF8Encoding(false));

        try
        {
            if (File.Exists(fullPath))
            {
                File.Replace(temp, fullPath, fullPath + ".bak", true);
            }
            else
            {
                File.Move(temp, fullPath);
            }
        }
        catch (Exception) when (File.Exists(temp))
        {
            File.Move(temp, fullPath, true);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Архив не критичен для работы программы.
        }
    }
}
