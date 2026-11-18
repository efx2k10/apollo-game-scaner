using ApolloGameScanner.Models;

namespace ApolloGameScanner.Services;

/// <summary>Помечает найденные игры как уже присутствующие в конфиге.</summary>
public static class GameMatcher
{
    public static void Apply(List<DetectedGame> games, AppsConfig? config)
    {
        foreach (var game in games)
        {
            var match = config is null ? null : FindMatch(game, config.Apps);
            if (match is null)
            {
                game.ExistingApp = null;
                game.Status = game.Candidates.Count > 0 ? GameStatus.New : GameStatus.NoExe;
                continue;
            }

            game.ExistingApp = match;
            game.Status = GameStatus.Existing;

            var exe = CommandLineUtil.ExtractExe(match.Cmd);
            var candidate = game.Candidates.FirstOrDefault(c =>
                CommandLineUtil.PathsEqual(c.FullPath, exe));
            if (candidate is not null)
            {
                game.SelectedCandidate = candidate;
            }

            game.CoverPreviewPath =
                !string.IsNullOrWhiteSpace(match.ImagePath) && File.Exists(match.ImagePath)
                    ? match.ImagePath
                    : game.CoverPreviewPath;
        }
    }

    private static AppEntry? FindMatch(DetectedGame game, IEnumerable<AppEntry> apps)
    {
        var gameNorm = NormalizeName(game.Name);
        var folderNorm = CommandLineUtil.NormalizePath(game.FolderPath);

        foreach (var app in apps)
        {
            var exe = CommandLineUtil.ExtractExe(app.Cmd);

            if (!string.IsNullOrEmpty(exe) && CommandLineUtil.IsInsideFolder(exe, game.FolderPath))
            {
                return app;
            }

            if (!string.IsNullOrEmpty(app.WorkingDir) &&
                (CommandLineUtil.NormalizePath(app.WorkingDir) == folderNorm ||
                 CommandLineUtil.IsInsideFolder(app.WorkingDir, game.FolderPath)))
            {
                return app;
            }
        }

        foreach (var app in apps)
        {
            if (string.IsNullOrWhiteSpace(app.Cmd))
            {
                continue;
            }

            if (NormalizeName(app.Name) == gameNorm)
            {
                return app;
            }
        }

        return null;
    }

    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var chars = name.ToLowerInvariant().Where(char.IsLetterOrDigit);
        return new string(chars.ToArray());
    }
}
