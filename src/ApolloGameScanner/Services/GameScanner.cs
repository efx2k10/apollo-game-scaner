using System.IO.Enumeration;
using System.Text.RegularExpressions;
using ApolloGameScanner.Models;

namespace ApolloGameScanner.Services;

public readonly record struct ScanProgress(int Current, int Total, string Folder);

public sealed class GameScanner
{
    private readonly AppSettings _settings;
    private readonly Func<string, string, string?> _coverLocator;

    public GameScanner(AppSettings settings, Func<string, string, string?> coverLocator)
    {
        _settings = settings;
        _coverLocator = coverLocator;
    }

    public List<DetectedGame> Scan(string root, IProgress<ScanProgress>? progress, CancellationToken token)
    {
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException("Папка с играми не найдена: " + root);
        }

        var folders = Directory.EnumerateDirectories(root)
            .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var games = new List<DetectedGame>(folders.Count);
        for (var index = 0; index < folders.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            var folder = folders[index];
            var folderName = Path.GetFileName(folder);
            progress?.Report(new ScanProgress(index + 1, folders.Count, folderName));

            if (MatchesFolderPattern(folderName))
            {
                continue;
            }

            var game = ScanFolder(folder, folderName, token);
            if (game is not null)
            {
                games.Add(game);
            }
        }

        return games;
    }

    private DetectedGame? ScanFolder(string folder, string folderName, CancellationToken token)
    {
        var game = new DetectedGame
        {
            FolderPath = folder,
            FolderName = folderName,
            Name = CleanGameName(folderName),
        };

        var candidates = new List<ExeCandidate>();
        var excluded = 0;

        Walk(folder, folder, 0, candidates, ref excluded, token);

        game.ExcludedFiles = excluded;

        var gameNorm = Normalize(folderName);
        foreach (var candidate in candidates)
        {
            candidate.Score = ScoreCandidate(candidate, gameNorm);
        }

        foreach (var candidate in candidates.OrderByDescending(c => c.Score).ThenByDescending(c => c.Size))
        {
            game.Candidates.Add(candidate);
        }

        game.SelectedCandidate = game.Candidates.FirstOrDefault();

        var draft = new AppEntry
        {
            Name = game.Name,
            WorkingDir = WorkingDirUtil.Resolve(
                _settings.WorkingDirMode, folder, game.SelectedCandidate?.FullPath),
        };

        if (game.SelectedCandidate is not null)
        {
            draft.Cmd = CommandLineUtil.Combine(game.SelectedCandidate.FullPath, null);
            game.Status = GameStatus.New;
        }
        else
        {
            game.Status = GameStatus.NoExe;
        }

        game.Draft = draft;

        try
        {
            game.CoverPreviewPath = _coverLocator(folder, game.Name);
        }
        catch
        {
            game.CoverPreviewPath = null;
        }

        return game;
    }

    private void Walk(string gameRoot, string directory, int depth, List<ExeCandidate> candidates,
        ref int excluded, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (_settings.MaxDepth > 0 && depth > _settings.MaxDepth)
        {
            return;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return;
        }

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (MatchesFilePattern(fileName) ||
                MatchesFilePattern(Path.GetFileNameWithoutExtension(fileName)))
            {
                excluded++;
                continue;
            }

            long size;
            try
            {
                size = new FileInfo(file).Length;
            }
            catch
            {
                continue;
            }

            candidates.Add(new ExeCandidate
            {
                FullPath = file,
                RelativePath = Path.GetRelativePath(gameRoot, file),
                Size = size,
            });
        }

        IEnumerable<string> subdirectories;
        try
        {
            subdirectories = Directory.EnumerateDirectories(directory).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return;
        }

        foreach (var subdirectory in subdirectories)
        {
            token.ThrowIfCancellationRequested();
            var name = Path.GetFileName(subdirectory);
            if (MatchesFolderPattern(name))
            {
                continue;
            }

            if ((File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            Walk(gameRoot, subdirectory, depth + 1, candidates, ref excluded, token);
        }
    }

    private static double ScoreCandidate(ExeCandidate candidate, string gameNorm)
    {
        var exeName = Path.GetFileNameWithoutExtension(candidate.FullPath);
        var exeNorm = Normalize(exeName);
        var relative = candidate.RelativePath.Replace('/', '\\').ToLowerInvariant();

        double score = 0;

        if (exeNorm.Length > 0 && exeNorm == gameNorm)
        {
            score += 100;
        }
        else if (exeNorm.Length >= 3 && (exeNorm.Contains(gameNorm) || gameNorm.Contains(exeNorm)))
        {
            score += 60;
        }
        else if (exeNorm.Length >= 3 &&
                 (exeNorm.StartsWith(gameNorm, StringComparison.Ordinal) ||
                  gameNorm.StartsWith(exeNorm, StringComparison.Ordinal)))
        {
            score += 40;
        }

        score += Math.Min(30, Math.Log10(Math.Max(1, candidate.Size)) * 6);

        var depth = relative.Count(ch => ch == '\\');
        if (depth <= 1)
        {
            score += 15;
        }
        else if (depth == 2)
        {
            score += 8;
        }

        if (Regex.IsMatch(relative, @"(^|\\)(bin|bin64|binaries|x64|win64|game|gamefiles)(\\|$)"))
        {
            score += 12;
        }

        if (relative.Contains("x86") || relative.Contains("win32") || relative.Contains("32-bit"))
        {
            score -= 10;
        }

        if (exeNorm.Contains("launcher"))
        {
            score -= 35;
        }

        if (exeNorm.Contains("config") || exeNorm.Contains("editor") ||
            exeNorm.Contains("benchmark") || exeNorm.Contains("tool") ||
            exeNorm.Contains("server"))
        {
            score -= 30;
        }

        if (exeNorm.Contains("crash") || exeNorm.Contains("report") || exeNorm.Contains("helper"))
        {
            score -= 45;
        }

        return score;
    }

    private static string Normalize(string value)
    {
        var chars = value.ToLowerInvariant().Where(char.IsLetterOrDigit);
        return new string(chars.ToArray());
    }

    private static readonly Regex VersionSuffix = new(
        @"[\s._-]*(?:v(?:er)?\s*)?\d+(?:\.\d+)+(?:[\s._-]*(?:final|repack|build|fix|update|hotfix|dlc\d*))*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string CleanGameName(string folderName)
    {
        var name = folderName.Replace('_', ' ').Trim();
        name = VersionSuffix.Replace(name, string.Empty).Trim();
        name = Regex.Replace(name, @"\s{2,}", " ");
        name = name.Trim(' ', '-', '.', '_');

        if (name.Length == 0)
        {
            name = folderName;
        }

        return name;
    }

    private bool MatchesFilePattern(string name) =>
        _settings.ExcludedFilePatterns.Any(pattern =>
            SafeMatch(pattern, name));

    private bool MatchesFolderPattern(string name) =>
        _settings.ExcludedFolderPatterns.Any(pattern =>
            SafeMatch(pattern, name));

    private static bool SafeMatch(string pattern, string name)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        try
        {
            return FileSystemName.MatchesSimpleExpression(pattern, name, ignoreCase: true);
        }
        catch
        {
            return false;
        }
    }
}
