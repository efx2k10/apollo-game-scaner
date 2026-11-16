namespace ApolloGameScanner.Models;

/// <summary>Как формировать поле <c>working-dir</c> у новых записей.</summary>
public enum WorkingDirMode
{
    /// <summary>Папка игры (первый уровень сканирования) — прежнее поведение.</summary>
    GameFolder = 0,

    /// <summary>Папка выбранного исполняемого файла.</summary>
    ExeFolder = 1,

    /// <summary>Не записывать working-dir.</summary>
    Empty = 2,
}
