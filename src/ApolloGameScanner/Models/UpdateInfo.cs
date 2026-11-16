namespace ApolloGameScanner.Models;

/// <summary>Сведения о более новой версии из GitHub Releases.</summary>
public sealed class UpdateInfo
{
    public required string Tag { get; init; }
    public required Version Version { get; init; }
    public string Notes { get; init; } = string.Empty;
    public required string AssetName { get; init; }
    public required string AssetUrl { get; init; }
    public long AssetSize { get; init; }
    public string? Sha256 { get; init; }
    public required string ReleaseUrl { get; init; }

    public string VersionText => Version.ToString(3);
}
