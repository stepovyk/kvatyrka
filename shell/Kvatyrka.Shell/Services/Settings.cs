using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kvatyrka.Shell.Services;

public enum Appearance { Auto, Dark, Light }

// Закріплена програма. Target – шлях до .exe, AUMID програми зі Store або внутрішня адреса «kvatyrka:…».
public sealed class PinnedApp
{
    public string Name { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? Exe { get; set; }
    public string? WindowClass { get; set; }
    public string? Glyph { get; set; }
}

// Усі налаштування користувача в одному файлі %APPDATA%\Kvatyrka\settings.json – їх можна міняти й руками.
public sealed class Settings
{
    public int Version { get; set; } = 2;
    public Appearance Theme { get; set; } = Appearance.Auto;
    public string Accent { get; set; } = "#5B9BFF";

    // Острів замість панелі задач Windows – за бажанням, типово вимкнений.
    public bool Island { get; set; }
    public bool WinKeyOpensMenu { get; set; } = true;
    public bool Transparency { get; set; } = true;
    public List<PinnedApp> Pinned { get; set; } = new();

    [JsonIgnore] public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kvatyrka");
    [JsonIgnore] public static string FilePath => Path.Combine(Folder, "settings.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static readonly string[] Accents = { "#5B9BFF", "#3FB68B", "#E5A23B", "#E26D5A", "#B07CF0", "#8A93A0" };
    public static readonly string[] AccentNames = { "Блакитний", "Зелений", "Бурштиновий", "Кораловий", "Бузковий", "Сірий" };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json);
                if (s is not null)
                {
                    if (!Accents.Contains(s.Accent, StringComparer.OrdinalIgnoreCase)) s.Accent = Accents[0];
                    return s;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Write($"Не вдалося прочитати налаштування, беру типові: {ex.Message}");
            TryBackup();
        }
        var fresh = new Settings { Pinned = Defaults.Pinned() };
        fresh.Save();
        return fresh;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Не вдалося зберегти налаштування: {ex.Message}");
        }
    }

    private static void TryBackup()
    {
        try { File.Copy(FilePath, FilePath + ".broken", true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

public static class Log
{
    private static readonly object Gate = new();

    // Журнал людини – у її профілі; процеси адміністратора й системи пишуть у спільний для ПК.
    public static string Path { get; set; } = System.IO.Path.Combine(Settings.Folder, "kvatyrka.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                var path = Path;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024) File.Delete(path);
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
