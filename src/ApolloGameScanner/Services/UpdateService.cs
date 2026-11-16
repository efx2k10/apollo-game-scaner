using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApolloGameScanner.Models;

namespace ApolloGameScanner.Services;

/// <summary>
/// Проверка и установка обновлений из GitHub Releases.
/// Обновление заменяет только исполняемый файл и README.md рядом с ним:
/// settings.json, covers и cache не затрагиваются.
/// </summary>
public sealed class UpdateService
{
    private const string Owner = "efx2k10";
    private const string Repo = "apollo-game-scaner";

    private static readonly Uri LatestReleaseUri =
        new($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

    private static HttpClient? _client;

    public static Version CurrentVersion { get; } =
        typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0);

    public static string CurrentVersionText => CurrentVersion.ToString(3);

    public static string SourceUrl => $"https://github.com/{Owner}/{Repo}/releases/latest";

    private static HttpClient Client => _client ??= CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"ApolloGameScanner/{CurrentVersionText}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>
    /// Возвращает сведения о более новой версии или <c>null</c>, если
    /// установлена последняя.
    /// </summary>
    public async Task<UpdateInfo?> GetLatestAsync(CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(LatestReleaseUri, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(DescribeError(response.StatusCode));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagElement)
            ? tagElement.GetString() ?? string.Empty
            : string.Empty;

        var version = ParseVersion(tag);
        if (version is null || version <= CurrentVersion)
        {
            return null;
        }

        var notes = root.TryGetProperty("body", out var bodyElement)
            ? bodyElement.GetString() ?? string.Empty
            : string.Empty;

        var releaseUrl = root.TryGetProperty("html_url", out var urlElement)
            ? urlElement.GetString() ?? SourceUrl
            : SourceUrl;

        var assets = ReadAssets(root);
        var preferredName = $"ApolloGameScanner-{version.ToString(3)}-win-x64.zip";

        var asset = assets.FirstOrDefault(a =>
                        string.Equals(a.Name, preferredName, StringComparison.OrdinalIgnoreCase))
                    ?? assets.FirstOrDefault(a =>
                        a.Name is not null &&
                        a.Name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase) &&
                        !a.Name.Contains("-fdd", StringComparison.OrdinalIgnoreCase));

        if (asset?.Name is null || asset.Url is null)
        {
            throw new InvalidOperationException(
                "В последнем релизе нет подходящего ZIP-архива для win-x64.");
        }

        var assetName = asset.Name;
        var sha = asset.Digest;
        if (string.IsNullOrWhiteSpace(sha))
        {
            var hashAsset = assets.FirstOrDefault(a =>
                string.Equals(a.Name, assetName + ".sha256", StringComparison.OrdinalIgnoreCase));

            if (hashAsset?.Url is not null)
            {
                sha = await DownloadHashAsync(hashAsset.Url, cancellationToken);
            }
        }

        return new UpdateInfo
        {
            Tag = tag,
            Version = version,
            Notes = notes.Trim(),
            AssetName = asset.Name,
            AssetUrl = asset.Url,
            AssetSize = asset.Size,
            Sha256 = sha,
            ReleaseUrl = releaseUrl,
        };
    }

    /// <summary>
    /// Скачивает архив обновления, проверяет SHA-256 и распаковывает его
    /// во временный каталог. Возвращает путь к каталогу с файлами.
    /// </summary>
    public async Task<string> DownloadAndExtractAsync(
        UpdateInfo info,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "ApolloGameScanner-update", info.VersionText);
        if (Directory.Exists(workDir))
        {
            Directory.Delete(workDir, true);
        }

        Directory.CreateDirectory(workDir);
        var zipPath = Path.Combine(workDir, info.AssetName);

        using (var response = await Client.GetAsync(
                   info.AssetUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(DescribeError(response.StatusCode));
            }

            var total = response.Content.Headers.ContentLength ?? info.AssetSize;

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long received = 0;
            int read;

            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                received += read;

                if (total > 0)
                {
                    progress?.Report(Math.Min(1d, (double)received / total));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(info.Sha256))
        {
            var actual = await ComputeSha256Async(zipPath, cancellationToken);
            if (!string.Equals(actual, info.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Контрольная сумма скачанного архива не совпадает. Обновление отменено.");
            }
        }

        var filesDir = Path.Combine(workDir, "files");
        Directory.CreateDirectory(filesDir);
        ZipFile.ExtractToDirectory(zipPath, filesDir, overwriteFiles: true);

        if (!File.Exists(Path.Combine(filesDir, "ApolloGameScanner.exe")))
        {
            throw new InvalidOperationException("В архиве обновления нет ApolloGameScanner.exe.");
        }

        try
        {
            File.Delete(zipPath);
        }
        catch
        {
            // Не критично: временный каталог будет удалён после установки.
        }

        return filesDir;
    }

    /// <summary>Можно ли записать файл рядом с текущим exe.</summary>
    public bool CanWriteInstallDirectory()
    {
        try
        {
            var probe = Path.Combine(AppContext.BaseDirectory, ".update-write-test");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Запускает скрытый помощник, который дождётся завершения текущего
    /// процесса, заменит exe (и README.md) и снова запустит программу.
    /// </summary>
    public void ApplyAndRestart(string extractedDir)
    {
        var target = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(target))
        {
            target = Path.Combine(AppContext.BaseDirectory, "ApolloGameScanner.exe");
        }

        var workDir = Path.GetDirectoryName(extractedDir.TrimEnd(Path.DirectorySeparatorChar))
                      ?? extractedDir;
        var scriptPath = Path.Combine(workDir, "update.ps1");

        File.WriteAllText(scriptPath, UpdaterScript, new UTF8Encoding(false));

        var psi = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = workDir,
        };

        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-WindowStyle");
        psi.ArgumentList.Add("Hidden");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("-ProcessId");
        psi.ArgumentList.Add(Environment.ProcessId.ToString());
        psi.ArgumentList.Add("-Source");
        psi.ArgumentList.Add(extractedDir);
        psi.ArgumentList.Add("-Target");
        psi.ArgumentList.Add(target);
        psi.ArgumentList.Add("-WorkDir");
        psi.ArgumentList.Add(workDir);

        Process.Start(psi);
    }

    public static void OpenReleasePage(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Открыть браузер не удалось — пользователь может сделать это вручную.
        }
    }

    private static List<AssetInfo> ReadAssets(JsonElement root)
    {
        var result = new List<AssetInfo>();
        if (!root.TryGetProperty("assets", out var assets) ||
            assets.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;

            var url = asset.TryGetProperty("browser_download_url", out var urlElement)
                ? urlElement.GetString()
                : null;

            var size = asset.TryGetProperty("size", out var sizeElement) &&
                       sizeElement.TryGetInt64(out var value)
                ? value
                : 0;

            var digest = asset.TryGetProperty("digest", out var digestElement) &&
                         digestElement.ValueKind == JsonValueKind.String
                ? digestElement.GetString()
                : null;

            if (digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                digest = digest[7..];
            }

            result.Add(new AssetInfo(name, url, size, digest));
        }

        return result;
    }

    private static async Task<string?> DownloadHashAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var text = await Client.GetStringAsync(url, cancellationToken);

            var token = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(part => part.Length == 64 && part.All(Uri.IsHexDigit));

            return token?.ToUpperInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static Version? ParseVersion(string tag)
    {
        var text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            text = text[..dash];
        }

        var parts = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var numbers = new int[4];
        for (var i = 0; i < parts.Length && i < 4; i++)
        {
            if (!int.TryParse(parts[i], out numbers[i]) || numbers[i] < 0)
            {
                return null;
            }
        }

        return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
    }

    private static string DescribeError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.NotFound => "Релизы не найдены (проверьте, что релиз опубликован).",
        HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests =>
            "GitHub ограничил частоту запросов. Попробуйте позже.",
        _ => $"GitHub вернул ошибку {(int)status} ({status}).",
    };

    private sealed record AssetInfo(string Name, string? Url, long Size, string? Digest);

    private const string UpdaterScript = """
param(
    [int]$ProcessId,
    [string]$Source,
    [string]$Target,
    [string]$WorkDir
)

$ErrorActionPreference = 'Stop'

try {
    Wait-Process -Id $ProcessId -Timeout 120 -ErrorAction SilentlyContinue
} catch {
}

for ($i = 0; $i -lt 60; $i++) {
    try {
        $stream = [IO.File]::Open($Target, 'Open', 'ReadWrite', 'None')
        $stream.Close()
        break
    } catch {
        Start-Sleep -Milliseconds 500
    }
}

$newExe = Join-Path $Source 'ApolloGameScanner.exe'
Copy-Item -LiteralPath $newExe -Destination $Target -Force

$newReadme = Join-Path $Source 'README.md'
if (Test-Path -LiteralPath $newReadme) {
    $readmeTarget = Join-Path (Split-Path -Parent $Target) 'README.md'
    Copy-Item -LiteralPath $newReadme -Destination $readmeTarget -Force
}

Start-Process -FilePath $Target -WorkingDirectory (Split-Path -Parent $Target)
Start-Sleep -Milliseconds 1000
Remove-Item -LiteralPath $WorkDir -Recurse -Force -ErrorAction SilentlyContinue
""";
}
