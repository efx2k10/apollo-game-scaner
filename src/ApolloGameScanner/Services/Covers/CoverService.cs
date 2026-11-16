using System.Drawing.Imaging;
using System.Text;
using ApolloGameScanner.Models;

namespace ApolloGameScanner.Services.Covers;

/// <summary>
/// Обложки: поиск в папке игры, иконка exe, конвертация в PNG и копирование в covers.
/// </summary>
public sealed class CoverService
{
    internal static readonly HttpClient Http = CreateHttpClient();

    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    private static readonly string[] CoverSubfolders =
    {
        "artwork", "cover", "covers", "box", "art", "media", "images", "screenshots",
    };

    private readonly AppSettings _settings;

    public CoverService(AppSettings settings)
    {
        _settings = settings;
    }

    public static string CacheDirectory => Path.Combine(AppContext.BaseDirectory, "cache", "covers");

    public string GetCoversDirectory(string? configPath)
    {
        if (!string.IsNullOrWhiteSpace(_settings.CoversFolder))
        {
            return _settings.CoversFolder!;
        }

        if (!string.IsNullOrWhiteSpace(configPath))
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(configPath));
            if (!string.IsNullOrEmpty(directory))
            {
                return Path.Combine(directory, "covers");
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "covers");
    }

    /// <summary>Ищет подходящую картинку внутри папки игры.</summary>
    public string? FindLocalCover(string gameFolder, string gameName)
    {
        if (!Directory.Exists(gameFolder))
        {
            return null;
        }

        var gameNorm = Normalize(gameName);
        var candidates = new List<(string Path, double Score)>();

        AddCandidates(gameFolder, gameFolder, gameNorm, candidates);

        foreach (var subfolderName in CoverSubfolders)
        {
            var subfolder = Path.Combine(gameFolder, subfolderName);
            if (!Directory.Exists(subfolder))
            {
                continue;
            }

            AddCandidates(gameFolder, subfolder, gameNorm, candidates);
        }

        return candidates
            .OrderByDescending(c => c.Score)
            .Select(c => c.Path)
            .FirstOrDefault();
    }

    private static void AddCandidates(string gameFolder, string directory, string gameNorm,
        List<(string Path, double Score)> result)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return;
        }

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            if (!ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            long size;
            int width;
            int height;
            try
            {
                size = new FileInfo(file).Length;
                if (size < 512)
                {
                    continue;
                }

                using var stream = File.OpenRead(file);
                using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
                width = image.Width;
                height = image.Height;
            }
            catch
            {
                continue;
            }

            if (width < 100 || height < 100)
            {
                continue;
            }

            var nameNorm = Normalize(Path.GetFileNameWithoutExtension(file));
            double score = 0;

            if (nameNorm.Length > 0 && nameNorm == gameNorm)
            {
                score += 60;
            }
            else if (nameNorm.Contains("cover") || nameNorm.Contains("box") ||
                     nameNorm.Contains("poster") || nameNorm.Contains("artwork") ||
                     nameNorm.Contains("folder"))
            {
                score += 50;
            }
            else if (nameNorm.Length >= 4 && gameNorm.Contains(nameNorm))
            {
                score += 35;
            }
            else if (nameNorm.Contains("logo") || nameNorm.Contains("icon") || nameNorm.Contains("thumb"))
            {
                score += 5;
            }
            else
            {
                score += 15;
            }

            if (height > width)
            {
                score += 25;
                var ratio = (double)height / width;
                if (ratio is >= 1.2 and <= 2.0)
                {
                    score += 10;
                }
            }

            score += Math.Log10(width * (double)height) * 2;

            if (width < 200 || height < 200)
            {
                score -= 30;
            }

            if (string.Equals(directory, gameFolder, StringComparison.OrdinalIgnoreCase))
            {
                score += 4;
            }

            result.Add((file, score));
        }
    }

    /// <summary>Копирует/конвертирует картинку в PNG и возвращает итоговый путь.</summary>
    public string SaveCover(string sourcePath, string coversDirectory, string gameName, bool overwrite = true)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Файл изображения не найден.", sourcePath);
        }

        Directory.CreateDirectory(coversDirectory);

        var target = BuildTargetPath(coversDirectory, Slug(gameName), ".png", overwrite);
        if (CommandLineUtil.NormalizePath(sourcePath) == CommandLineUtil.NormalizePath(target))
        {
            return target;
        }

        using var image = Image.FromFile(sourcePath);
        image.Save(target, ImageFormat.Png);
        return target;
    }

    /// <summary>Достаёт иконку exe и сохраняет её как PNG (временная обложка).</summary>
    public string? ExtractExeIcon(string exePath, string coversDirectory, string gameName)
    {
        if (!File.Exists(exePath))
        {
            return null;
        }

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon is null)
            {
                return null;
            }

            Directory.CreateDirectory(coversDirectory);
            var target = BuildTargetPath(coversDirectory, Slug(gameName) + "-icon", ".png", overwrite: false);

            using var bitmap = new Bitmap(128, 128);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                using var scaled = new Icon(icon, new Size(128, 128));
                graphics.DrawIcon(scaled, new Rectangle(0, 0, 128, 128));
            }

            bitmap.Save(target, ImageFormat.Png);
            return target;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Загружает картинку во временный файл кэша.</summary>
    public async Task<string> DownloadToCacheAsync(string url, CancellationToken token)
    {
        var bytes = await Http.GetByteArrayAsync(url, token);
        if (bytes.Length == 0)
        {
            throw new InvalidDataException("Сервер вернул пустой ответ.");
        }

        Directory.CreateDirectory(CacheDirectory);
        var extension = Path.GetExtension(new Uri(url).AbsolutePath);
        if (extension.Length is 0 or > 5)
        {
            extension = ".img";
        }

        var path = Path.Combine(CacheDirectory, Guid.NewGuid().ToString("N") + extension);
        await File.WriteAllBytesAsync(path, bytes, token);
        return path;
    }

    public static async Task<byte[]> DownloadBytesAsync(string url, CancellationToken token)
        => await Http.GetByteArrayAsync(url, token);

    /// <summary>Загружает картинку без блокировки файла.</summary>
    public static Image? LoadImageNoLock(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            using var stream = new MemoryStream(bytes);
            return Image.FromStream(stream);
        }
        catch
        {
            return null;
        }
    }

    public static void ClearCache()
    {
        if (!Directory.Exists(CacheDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(CacheDirectory, recursive: true);
        }
        catch
        {
            // кэш не критичен
        }
    }

    public static string Slug(string? name)
    {
        var source = (name ?? string.Empty).Trim();
        var builder = new StringBuilder(source.Length);

        foreach (var ch in source)
        {
            var lower = char.ToLowerInvariant(ch);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(lower);
                continue;
            }

            var translit = Transliterate(lower);
            if (translit.Length > 0)
            {
                builder.Append(translit);
            }
            else
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, "-{2,}", "-").Trim('-');

        if (slug.Length > 60)
        {
            slug = slug[..60].Trim('-');
        }

        return slug.Length == 0 ? "cover" : slug;
    }

    private static string Transliterate(char ch) => ch switch
    {
        'а' => "a", 'б' => "b", 'в' => "v", 'г' => "g", 'д' => "d", 'е' => "e",
        'ё' => "e", 'ж' => "zh", 'з' => "z", 'и' => "i", 'й' => "y", 'к' => "k",
        'л' => "l", 'м' => "m", 'н' => "n", 'о' => "o", 'п' => "p", 'р' => "r",
        'с' => "s", 'т' => "t", 'у' => "u", 'ф' => "f", 'х' => "h", 'ц' => "c",
        'ч' => "ch", 'ш' => "sh", 'щ' => "sch", 'ъ' => "", 'ы' => "y", 'ь' => "",
        'э' => "e", 'ю' => "yu", 'я' => "ya",
        'і' => "i", 'ї' => "yi", 'є' => "ye", 'ґ' => "g",
        _ => string.Empty,
    };

    private static string BuildTargetPath(string directory, string baseName, string extension, bool overwrite)
    {
        var target = Path.Combine(directory, baseName + extension);
        if (overwrite || !File.Exists(target))
        {
            return target;
        }

        for (var index = 2; index < 1000; index++)
        {
            target = Path.Combine(directory, $"{baseName}-{index}{extension}");
            if (!File.Exists(target))
            {
                return target;
            }
        }

        return Path.Combine(directory, $"{baseName}-{Guid.NewGuid():N}{extension}");
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.ToLowerInvariant().Where(char.IsLetterOrDigit);
        return new string(chars.ToArray());
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20),
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ApolloGameScanner/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en;q=0.8");
        return client;
    }
}
