namespace ApolloGameScanner.Models;

public enum GameStatus
{
    New,
    Existing,
    NoExe,
}

public sealed class ExeCandidate
{
    public string FullPath { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public long Size { get; init; }
    public double Score { get; set; }

    public override string ToString()
    {
        var size = Size >= 1024 * 1024
            ? $"{Size / 1024d / 1024d:0.#} МБ"
            : $"{Math.Max(1, Size / 1024)} КБ";
        return $"{RelativePath}  ({size})";
    }
}

/// <summary>Папка первого уровня, рассматриваемая как игра.</summary>
public sealed class DetectedGame
{
    public string FolderPath { get; init; } = string.Empty;
    public string FolderName { get; init; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public GameStatus Status { get; set; }
    public List<ExeCandidate> Candidates { get; } = new();
    public ExeCandidate? SelectedCandidate { get; set; }
    public int ExcludedFiles { get; set; }
    public bool IsSelected { get; set; }

    /// <summary>Черновик записи конфига (имя, exe, рабочая папка, обложка, доп. параметры).</summary>
    public AppEntry Draft { get; set; } = new();

    /// <summary>Существующая запись конфига, если игра уже добавлена.</summary>
    public AppEntry? ExistingApp { get; set; }

    /// <summary>Картинка для превью в списке.</summary>
    public string? CoverPreviewPath { get; set; }

    public override string ToString() => Name;
}
