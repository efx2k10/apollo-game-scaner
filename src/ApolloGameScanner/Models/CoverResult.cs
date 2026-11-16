namespace ApolloGameScanner.Models;

public enum CoverSource
{
    LocalFolder,
    ExeIcon,
    GameDb,
    SteamGridDb,
    DuckDuckGo,
    Manual,
}

/// <summary>Результат поиска обложки у онлайн-провайдера.</summary>
public sealed class CoverResult
{
    public string Source { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string ThumbUrl { get; init; } = string.Empty;
    public string FullUrl { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
}
