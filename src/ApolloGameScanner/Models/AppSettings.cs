namespace ApolloGameScanner.Models;

/// <summary>Пользовательские настройки (settings.json рядом с exe).</summary>
public sealed class AppSettings
{
    public string? LastConfigPath { get; set; }
    public string? LastGamesFolder { get; set; }
    public List<string> ExcludedFilePatterns { get; set; } = DefaultFilePatterns();
    public List<string> ExcludedFolderPatterns { get; set; } = DefaultFolderPatterns();
    public int MaxDepth { get; set; }
    public string? CoversFolder { get; set; }
    public string SteamGridDbKey { get; set; } = string.Empty;
    public bool ShowOnlyNew { get; set; }

    /// <summary>
    /// Как формировать working-dir у новых записей. По умолчанию — папка
    /// выбранного exe (раньше всегда использовалась папка игры).
    /// </summary>
    public WorkingDirMode WorkingDirMode { get; set; } = WorkingDirMode.ExeFolder;

    public static List<string> DefaultFilePatterns() => new()
    {
        "unins*",
        "uninstall*",
        "*uninst*",
        "setup*",
        "*setup*",
        "vcredist*",
        "vc_redist*",
        "vcredist_*",
        "dxsetup*",
        "dxwebsetup*",
        "*redist*",
        "dotnetfx*",
        "ndp*.exe",
        "UnityCrashHandler*",
        "UnityPlayer*",
        "crash*",
        "*crashhandler*",
        "*report*",
        "activation*",
        "*activation*",
        "patcher*",
        "*benchmark*",
        "oalinst*",
    };

    public static List<string> DefaultFolderPatterns() => new()
    {
        "Redist",
        "_CommonRedist",
        "CommonRedist",
        "DirectX",
        "DotNet",
        "Support",
        "support",
        "__Installer",
        "installer",
        "Installers",
    };

    public void Normalize()
    {
        ExcludedFilePatterns ??= DefaultFilePatterns();
        ExcludedFolderPatterns ??= DefaultFolderPatterns();
        SteamGridDbKey ??= string.Empty;

        ExcludedFilePatterns = ExcludedFilePatterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        ExcludedFolderPatterns = ExcludedFolderPatterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (MaxDepth < 0)
        {
            MaxDepth = 0;
        }

        if (!Enum.IsDefined(WorkingDirMode))
        {
            WorkingDirMode = WorkingDirMode.ExeFolder;
        }
    }
}
