using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApolloGameScanner.Services;

/// <summary>
/// Терпимое чтение значений из apps.json: старые версии Sunshine/Apollo
/// могли хранить булевы значения строками, массивами и т.п.
/// </summary>
internal static class JsonUtil
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string? GetString(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<string>(out var s))
        {
            return s;
        }

        if (value.TryGetValue<bool>(out var b))
        {
            return b ? "true" : "false";
        }

        if (value.TryGetValue<double>(out var d))
        {
            return d.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue<long>(out var l))
        {
            return l.ToString(CultureInfo.InvariantCulture);
        }

        return null;
    }

    public static bool GetBool(JsonNode? node, bool fallback = false)
    {
        switch (node)
        {
            case null:
                return fallback;
            case JsonArray array:
                return array.Count > 0 && GetBool(array[0], fallback);
            case JsonValue value:
                if (value.TryGetValue<bool>(out var b))
                {
                    return b;
                }

                if (value.TryGetValue<string>(out var s))
                {
                    var v = s.Trim().ToLowerInvariant();
                    return v is "true" or "on" or "yes" or "1" or "enabled";
                }

                if (value.TryGetValue<double>(out var d))
                {
                    return d != 0;
                }

                return fallback;
            default:
                return fallback;
        }
    }

    public static int GetInt(JsonNode? node, int fallback)
    {
        if (node is not JsonValue value)
        {
            return fallback;
        }

        if (value.TryGetValue<int>(out var i))
        {
            return i;
        }

        if (value.TryGetValue<double>(out var d))
        {
            return (int)d;
        }

        if (value.TryGetValue<string>(out var s) &&
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    public static List<string> GetStringList(JsonNode? node)
    {
        var result = new List<string>();
        if (node is not JsonArray array)
        {
            return result;
        }

        foreach (var item in array)
        {
            var s = GetString(item);
            if (!string.IsNullOrEmpty(s))
            {
                result.Add(s);
            }
        }

        return result;
    }

    public static JsonArray ToArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }
}
