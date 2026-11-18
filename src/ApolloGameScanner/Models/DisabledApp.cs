namespace ApolloGameScanner.Models;

/// <summary>
/// Запись, выключенная в сканере. В <c>apps.json</c> её нет (Apollo её не видит),
/// но все поля сохраняются в архиве рядом с конфигом, чтобы игру можно было
/// включить обратно без потери настроек.
/// </summary>
public sealed class DisabledApp
{
    public AppEntry Entry { get; set; } = new();

    /// <summary>Позиция в массиве <c>apps</c> на момент выключения.</summary>
    public int Order { get; set; }
}
