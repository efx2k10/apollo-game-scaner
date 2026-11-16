using ApolloGameScanner.Models;

namespace ApolloGameScanner.Services;

/// <summary>Формирование значения working-dir по настройке.</summary>
public static class WorkingDirUtil
{
    public static string Resolve(WorkingDirMode mode, string? gameFolder, string? exePath)
    {
        try
        {
            return mode switch
            {
                WorkingDirMode.GameFolder => gameFolder ?? string.Empty,
                WorkingDirMode.ExeFolder => ResolveExeFolder(exePath),
                WorkingDirMode.Empty => string.Empty,
                _ => gameFolder ?? string.Empty,
            };
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveExeFolder(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) ||
            exePath.Contains("://", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetDirectoryName(exePath.Trim().Trim('"')) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string GetDisplayName(WorkingDirMode mode) => mode switch
    {
        WorkingDirMode.GameFolder => "папка игры",
        WorkingDirMode.ExeFolder => "папка exe",
        WorkingDirMode.Empty => "пусто",
        _ => "папка игры",
    };
}
