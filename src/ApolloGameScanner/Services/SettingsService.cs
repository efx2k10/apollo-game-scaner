using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ApolloGameScanner.Models;

namespace ApolloGameScanner.Services;

/// <summary>
/// settings.json лежит рядом с исполняемым файлом (портативный режим).
/// Если папка недоступна для записи — работаем без сохранения и сообщаем об этом.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string SettingsPath { get; }
    public AppSettings Settings { get; private set; } = new();
    public string? LastError { get; private set; }

    public SettingsService()
    {
        SettingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
    }

    public void Load()
    {
        LastError = null;
        try
        {
            if (!File.Exists(SettingsPath))
            {
                Settings = new AppSettings();
                return;
            }

            var text = File.ReadAllText(SettingsPath, Encoding.UTF8);
            Settings = JsonSerializer.Deserialize<AppSettings>(text, Options) ?? new AppSettings();
            Settings.Normalize();
        }
        catch (Exception ex)
        {
            LastError = "Не удалось прочитать settings.json: " + ex.Message;
            Settings = new AppSettings();
        }
    }

    public bool Save()
    {
        LastError = null;
        try
        {
            Settings.Normalize();
            var text = JsonSerializer.Serialize(Settings, Options);
            var temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));

            if (File.Exists(SettingsPath))
            {
                var backup = SettingsPath + ".bak";
                try
                {
                    File.Replace(temp, SettingsPath, backup, true);
                }
                catch (Exception) when (File.Exists(temp))
                {
                    File.Copy(SettingsPath, backup, true);
                    File.Delete(SettingsPath);
                    File.Move(temp, SettingsPath);
                }
            }
            else
            {
                File.Move(temp, SettingsPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            LastError = "Не удалось сохранить настройки рядом с программой: " + ex.Message +
                        Environment.NewLine + "Проверьте, что папка программы доступна для записи.";
            return false;
        }
    }
}
