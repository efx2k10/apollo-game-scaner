using ApolloGameScanner.Models;

namespace ApolloGameScanner.Services.Covers;

public interface ICoverProvider
{
    /// <summary>Имя источника для интерфейса.</summary>
    string Name { get; }

    /// <summary>Ищет варианты обложек по названию игры.</summary>
    Task<IReadOnlyList<CoverResult>> SearchAsync(string query, CancellationToken token);
}
