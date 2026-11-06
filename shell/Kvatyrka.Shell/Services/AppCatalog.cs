using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using Kvatyrka.Shell.Native;

namespace Kvatyrka.Shell.Services;

public sealed record AppInfo(string Name, string Target)
{
    public string? Exe => Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? Path.GetFileName(Target) : null;
}

// Каталог усіх встановлених програм і їхніх іконок.
public sealed class AppCatalog
{
    public static AppCatalog Instance { get; } = new();

    private static readonly string[] Junk = { "uninstall", "видалити", "деінсталя", "remove ", "readme", "help", "довідка", "documentation", "website" };
    private static readonly string[] JunkExt = { ".url", ".chm", ".pdf", ".txt", ".htm", ".html", ".hlp", ".ini", ".log" };
    private readonly ConcurrentDictionary<(string, int), BitmapSource?> _icons = new();
    private List<AppInfo>? _all;

    public IReadOnlyList<AppInfo> All => _all ??= Load();

    public void Refresh() => _all = Load();

    private static List<AppInfo> Load()
    {
        var uk = CultureInfo.GetCultureInfo("uk-UA");
        try
        {
            return ShellItems.EnumerateApps()
                .Where(e => !IsJunk(e))
                .GroupBy(e => e.ParsingName, StringComparer.OrdinalIgnoreCase)
                .Select(g => new AppInfo(g.First().Name, g.Key))
                .OrderBy(a => a.Name, StringComparer.Create(uk, true))
                .ToList();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidCastException)
        {
            Log.Write($"Не вдалося отримати список програм: {ex.Message}");
            return new List<AppInfo>();
        }
    }

    private static bool IsJunk(ShellItems.Entry e)
    {
        var name = e.Name.ToLowerInvariant();
        if (Junk.Any(name.Contains)) return true;
        var p = e.ParsingName.ToLowerInvariant();
        return p.StartsWith("http") || JunkExt.Any(p.EndsWith);
    }

    public BitmapSource? Icon(string target, int px)
    {
        var key = target.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe") : target;
        return _icons.GetOrAdd((key, px), k =>
            ShellItems.Icon(File.Exists(k.Item1) ? k.Item1 : $"shell:AppsFolder\\{k.Item1}", k.Item2));
    }

    private static readonly Dictionary<string, Ui.IconKind> SystemGlyphs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.Windows.Explorer"] = Ui.IconKind.Folder,
        ["Microsoft.WindowsStore_8wekyb3d8bbwe!App"] = Ui.IconKind.Bag,
        ["Microsoft.WindowsTerminal_8wekyb3d8bbwe!App"] = Ui.IconKind.Terminal,
        ["windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel"] = Ui.IconKind.Sliders,
    };

    private static readonly Lazy<string?> BrowserExe = new(() => Defaults.DefaultBrowser() is { } b ? Path.GetFileName(b) : null);

    // Системні програми малюються іконками Кватирки – так само, як на острові.
    public static Ui.IconKind GlyphFor(AppInfo app) =>
        SystemGlyphs.TryGetValue(app.Target, out var g) ? g
        : app.Exe is not null && string.Equals(app.Exe, BrowserExe.Value, StringComparison.OrdinalIgnoreCase) ? Ui.IconKind.Globe
        : Ui.IconKind.None;

    // Іконки всіх програм готуються заздалегідь у фоні, щоб меню відкривалося миттєво.
    public void WarmUp(int px)
    {
        var apps = All.ToList();
        var thread = new Thread(() => { foreach (var a in apps) Icon(a.Target, px); }) { IsBackground = true, Priority = ThreadPriority.BelowNormal };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public static void Launch(string target)
    {
        try
        {
            if (target == Defaults.SettingsTarget) { App.Current.ShowSettings(); return; }
            if (target.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase)) { Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true }); return; }
            if (Path.IsPathRooted(target) && File.Exists(target))
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(target) });
                return;
            }
            Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{target}") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            Log.Write($"Не вдалося запустити {target}: {ex.Message}");
        }
    }
}
