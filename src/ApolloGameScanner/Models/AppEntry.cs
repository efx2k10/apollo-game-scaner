using System.Text.Json.Nodes;
using ApolloGameScanner.Services;

namespace ApolloGameScanner.Models;

/// <summary>
/// Одна запись из массива "apps" в apps.json Apollo/Sunshine.
/// Неизвестные поля сохраняются в <see cref="Extra"/> и записываются обратно без потерь.
/// </summary>
public sealed class AppEntry
{
    public string Uuid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Cmd { get; set; } = string.Empty;
    public string WorkingDir { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;

    public List<string> Detached { get; set; } = new();
    public List<AppCommand> PrepCmd { get; set; } = new();
    public List<AppCommand> StateCmd { get; set; } = new();

    public bool Elevated { get; set; }
    public bool AutoDetach { get; set; } = true;
    public bool WaitAll { get; set; } = true;
    public bool VirtualDisplay { get; set; }
    public int ExitTimeout { get; set; } = 5;
    public int ScaleFactor { get; set; } = 100;
    public bool UseAppIdentity { get; set; }
    public bool PerClientAppIdentity { get; set; }
    public bool AllowClientCommands { get; set; } = true;
    public bool TerminateOnPause { get; set; }
    public bool ExcludeGlobalPrepCmd { get; set; }
    public bool ExcludeGlobalStateCmd { get; set; }
    public string Gamepad { get; set; } = string.Empty;

    /// <summary>Ключи, которые физически были в исходном JSON (чтобы не раздувать файл).</summary>
    public HashSet<string> PresentKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Неизвестные программе поля, сохраняются как есть.</summary>
    public Dictionary<string, JsonNode?> Extra { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Выбрана ли запись в интерфейсе для пакетных действий (не сохраняется).</summary>
    public bool IsSelected { get; set; }

    private static readonly HashSet<string> KnownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "uuid", "name", "cmd", "working-dir", "image-path", "output",
        "detached", "prep-cmd", "state-cmd",
        "elevated", "auto-detach", "wait-all", "exit-timeout", "scale-factor",
        "virtual-display", "use-app-identity", "per-client-app-identity",
        "allow-client-commands", "terminate-on-pause",
        "exclude-global-prep-cmd", "exclude-global-state-cmd", "gamepad",
    };

    public static AppEntry FromJson(JsonObject obj)
    {
        var entry = new AppEntry
        {
            Uuid = JsonUtil.GetString(obj["uuid"]) ?? string.Empty,
            Name = JsonUtil.GetString(obj["name"]) ?? string.Empty,
            Cmd = JsonUtil.GetString(obj["cmd"]) ?? string.Empty,
            WorkingDir = JsonUtil.GetString(obj["working-dir"]) ?? string.Empty,
            ImagePath = JsonUtil.GetString(obj["image-path"]) ?? string.Empty,
            Output = JsonUtil.GetString(obj["output"]) ?? string.Empty,
            Elevated = JsonUtil.GetBool(obj["elevated"]),
            AutoDetach = JsonUtil.GetBool(obj["auto-detach"], true),
            WaitAll = JsonUtil.GetBool(obj["wait-all"], true),
            VirtualDisplay = JsonUtil.GetBool(obj["virtual-display"]),
            ExitTimeout = JsonUtil.GetInt(obj["exit-timeout"], 5),
            ScaleFactor = JsonUtil.GetInt(obj["scale-factor"], 100),
            UseAppIdentity = JsonUtil.GetBool(obj["use-app-identity"]),
            PerClientAppIdentity = JsonUtil.GetBool(obj["per-client-app-identity"]),
            AllowClientCommands = JsonUtil.GetBool(obj["allow-client-commands"], true),
            TerminateOnPause = JsonUtil.GetBool(obj["terminate-on-pause"]),
            ExcludeGlobalPrepCmd = JsonUtil.GetBool(obj["exclude-global-prep-cmd"]),
            ExcludeGlobalStateCmd = JsonUtil.GetBool(obj["exclude-global-state-cmd"]),
            Gamepad = JsonUtil.GetString(obj["gamepad"]) ?? string.Empty,
            Detached = JsonUtil.GetStringList(obj["detached"]),
        };

        if (obj["prep-cmd"] is JsonArray prepArray)
        {
            foreach (var item in prepArray)
            {
                if (item is JsonObject command)
                {
                    entry.PrepCmd.Add(AppCommand.FromJson(command));
                }
            }
        }

        if (obj["state-cmd"] is JsonArray stateArray)
        {
            foreach (var item in stateArray)
            {
                if (item is JsonObject command)
                {
                    entry.StateCmd.Add(AppCommand.FromJson(command));
                }
            }
        }

        foreach (var pair in obj)
        {
            entry.PresentKeys.Add(pair.Key);
            if (!KnownKeys.Contains(pair.Key))
            {
                entry.Extra[pair.Key] = pair.Value?.DeepClone();
            }
        }

        return entry;
    }

    public JsonObject ToJson()
    {
        var obj = new JsonObject();

        PutString(obj, "uuid", Uuid);
        PutString(obj, "name", Name);
        PutString(obj, "cmd", Cmd);
        PutString(obj, "working-dir", WorkingDir);
        PutString(obj, "image-path", ImagePath);
        PutString(obj, "output", Output);

        if (Detached.Count > 0)
        {
            obj["detached"] = JsonUtil.ToArray(Detached);
        }

        if (PrepCmd.Count > 0)
        {
            var array = new JsonArray();
            foreach (var command in PrepCmd)
            {
                array.Add(command.ToJson());
            }

            obj["prep-cmd"] = array;
        }

        if (StateCmd.Count > 0)
        {
            var array = new JsonArray();
            foreach (var command in StateCmd)
            {
                array.Add(command.ToJson());
            }

            obj["state-cmd"] = array;
        }

        PutBool(obj, "elevated", Elevated, false);
        PutBool(obj, "auto-detach", AutoDetach, true);
        PutBool(obj, "wait-all", WaitAll, true);
        PutInt(obj, "exit-timeout", ExitTimeout, 5);
        PutInt(obj, "scale-factor", ScaleFactor, 100);
        PutBool(obj, "virtual-display", VirtualDisplay, false);
        PutBool(obj, "use-app-identity", UseAppIdentity, false);
        PutBool(obj, "per-client-app-identity", PerClientAppIdentity, false);
        PutBool(obj, "allow-client-commands", AllowClientCommands, true);
        PutBool(obj, "terminate-on-pause", TerminateOnPause, false);
        PutBool(obj, "exclude-global-prep-cmd", ExcludeGlobalPrepCmd, false);
        PutBool(obj, "exclude-global-state-cmd", ExcludeGlobalStateCmd, false);
        PutString(obj, "gamepad", Gamepad);

        foreach (var pair in Extra)
        {
            obj[pair.Key] = pair.Value?.DeepClone();
        }

        return obj;
    }

    public AppEntry Clone() => FromJson(ToJson());

    public void CopyFrom(AppEntry other)
    {
        Uuid = other.Uuid;
        Name = other.Name;
        Cmd = other.Cmd;
        WorkingDir = other.WorkingDir;
        ImagePath = other.ImagePath;
        Output = other.Output;
        Detached = new List<string>(other.Detached);
        PrepCmd = other.PrepCmd.Select(command => command.Clone()).ToList();
        StateCmd = other.StateCmd.Select(command => command.Clone()).ToList();
        Elevated = other.Elevated;
        AutoDetach = other.AutoDetach;
        WaitAll = other.WaitAll;
        VirtualDisplay = other.VirtualDisplay;
        ExitTimeout = other.ExitTimeout;
        ScaleFactor = other.ScaleFactor;
        UseAppIdentity = other.UseAppIdentity;
        PerClientAppIdentity = other.PerClientAppIdentity;
        AllowClientCommands = other.AllowClientCommands;
        TerminateOnPause = other.TerminateOnPause;
        ExcludeGlobalPrepCmd = other.ExcludeGlobalPrepCmd;
        ExcludeGlobalStateCmd = other.ExcludeGlobalStateCmd;
        Gamepad = other.Gamepad;

        PresentKeys.Clear();
        foreach (var key in other.PresentKeys)
        {
            PresentKeys.Add(key);
        }

        Extra.Clear();
        foreach (var pair in other.Extra)
        {
            Extra[pair.Key] = pair.Value?.DeepClone();
        }
    }

    public string DisplayCmd
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Cmd))
            {
                return Cmd;
            }

            if (Detached.Count > 0)
            {
                return "detached: " + string.Join("; ", Detached);
            }

            return "команда не задана";
        }
    }

    private void PutString(JsonObject obj, string key, string value)
    {
        if (value.Length > 0 || PresentKeys.Contains(key))
        {
            obj[key] = value;
        }
    }

    private void PutBool(JsonObject obj, string key, bool value, bool fallback)
    {
        if (value != fallback || PresentKeys.Contains(key))
        {
            obj[key] = value;
        }
    }

    private void PutInt(JsonObject obj, string key, int value, int fallback)
    {
        if (value != fallback || PresentKeys.Contains(key))
        {
            obj[key] = value;
        }
    }
}
