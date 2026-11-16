using System.Text;
using ApolloGameScanner.Models;

namespace ApolloGameScanner.Services;

public sealed class AppsConfigService
{
    public AppsConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Файл конфига не найден.", path);
        }

        var text = File.ReadAllText(path, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new AppsConfig();
        }

        return AppsConfig.Parse(text);
    }

    public void Save(string path, AppsConfig config)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("Не удалось определить папку для сохранения конфига.");
        }

        Directory.CreateDirectory(directory);

        var text = config.Serialize();
        var temp = fullPath + ".tmp";
        File.WriteAllText(temp, text, new UTF8Encoding(false));

        if (File.Exists(fullPath))
        {
            var backup = fullPath + ".bak";
            try
            {
                File.Replace(temp, fullPath, backup, true);
            }
            catch (Exception) when (File.Exists(temp))
            {
                File.Copy(fullPath, backup, true);
                File.Delete(fullPath);
                File.Move(temp, fullPath);
            }
        }
        else
        {
            File.Move(temp, fullPath);
        }
    }
}
