using System.IO;
using Microsoft.Win32;

namespace Kvatyrka.Shell.Services;

// Що закріпити на острові при першому запуску: лише те, що справді є на цьому ПК.
public static class Defaults
{
    public const string SettingsTarget = "kvatyrka:settings";
    private const string TerminalAumid = "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App";
    private const string StoreAumid = "Microsoft.WindowsStore_8wekyb3d8bbwe!App";

    public static List<PinnedApp> Pinned()
    {
        var installed = AppCatalog.Instance.All.Select(a => a.Target).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var list = new List<PinnedApp>
        {
            new() { Name = "Файли", Target = "explorer.exe", WindowClass = "CabinetWClass", Glyph = "Folder" },
        };
        if (DefaultBrowser() is { } browser)
            list.Add(new() { Name = "Браузер", Target = browser, Exe = Path.GetFileName(browser), Glyph = "Globe" });
        if (installed.Contains(TerminalAumid))
            list.Add(new() { Name = "Термінал", Target = TerminalAumid, Exe = "WindowsTerminal.exe", Glyph = "Terminal" });
        if (installed.Contains(StoreAumid))
            list.Add(new() { Name = "Крамниця", Target = StoreAumid, Glyph = "Bag" });
        list.Add(new() { Name = "Налаштування", Target = SettingsTarget, Glyph = "Sliders" });
        return list;
    }

    // Браузер за замовчуванням: ProgId для http → команда відкриття → шлях до .exe.
    public static string? DefaultBrowser()
    {
        try
        {
            using var choice = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
            if (choice?.GetValue("ProgId") is not string progId) return null;
            using var cmd = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
            if (cmd?.GetValue(null) is not string line) return null;
            var exe = line.StartsWith('"') ? line[1..line.IndexOf('"', 1)] : line.Split(' ')[0];
            return File.Exists(exe) ? exe : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
