namespace ApolloGameScanner.Services;

public static class CommandLineUtil
{
    /// <summary>Разбирает строку cmd на путь к exe и аргументы (с учётом кавычек).</summary>
    public static (string Exe, string Args) Split(string? cmd)
    {
        var text = (cmd ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            if (end > 0)
            {
                var exe = text.Substring(1, end - 1);
                var args = text[(end + 1)..].Trim();
                return (exe, args);
            }
        }

        var space = text.IndexOf(' ');
        if (space < 0)
        {
            return (text, string.Empty);
        }

        return (text[..space].Trim(), text[(space + 1)..].Trim());
    }

    public static string Combine(string exe, string? args)
    {
        if (string.IsNullOrWhiteSpace(exe))
        {
            return string.Empty;
        }

        var exeTrimmed = exe.Trim();
        var trimmedArgs = (args ?? string.Empty).Trim();

        // URI-команды (Epic, Steam и т.п.) не заключаем в кавычки.
        if (exeTrimmed.Contains("://", StringComparison.Ordinal))
        {
            return trimmedArgs.Length == 0 ? exeTrimmed : exeTrimmed + " " + trimmedArgs;
        }

        var result = Quote(exeTrimmed);
        return trimmedArgs.Length == 0 ? result : result + " " + trimmedArgs;
    }

    public static string Quote(string path)
    {
        var trimmed = (path ?? string.Empty).Trim().Trim('"');
        return "\"" + trimmed + "\"";
    }

    public static string ExtractExe(string? cmd) => Split(cmd).Exe;

    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Trim()
            .Trim('"')
            .Replace('/', '\\')
            .TrimEnd('\\')
            .ToLowerInvariant();
    }

    public static bool PathsEqual(string? left, string? right)
    {
        var a = NormalizePath(left);
        var b = NormalizePath(right);
        return a.Length > 0 && a == b;
    }

    /// <summary>Лежит ли файл внутри папки.</summary>
    public static bool IsInsideFolder(string? filePath, string? folderPath)
    {
        var file = NormalizePath(filePath);
        var folder = NormalizePath(folderPath);
        if (file.Length == 0 || folder.Length == 0)
        {
            return false;
        }

        return file.StartsWith(folder + "\\", StringComparison.Ordinal);
    }
}
