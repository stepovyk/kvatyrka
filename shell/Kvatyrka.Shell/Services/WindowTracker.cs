using System.IO;
using Kvatyrka.Shell.Native;

namespace Kvatyrka.Shell.Services;

public sealed record AppWindow(IntPtr Handle, string Title, string? ExePath, string? Aumid, string Class)
{
    public string? ExeName => ExePath is null ? null : Path.GetFileName(ExePath);
    public string GroupKey => Aumid ?? ExePath ?? Class;
}

// Відкриті вікна програм, як їх бачить людина: без службових, прихованих і власних вікон оболонки.
public static class WindowTracker
{
    private static readonly HashSet<string> SkipClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow",
    };

    private static readonly uint OwnPid = (uint)Environment.ProcessId;
    private static readonly Dictionary<uint, (string? Path, string? Aumid)> ProcessCache = new();
    private static readonly Dictionary<IntPtr, long> FirstSeen = new();
    private static long _counter;

    public static List<AppWindow> Snapshot()
    {
        var result = new List<AppWindow>();
        Win32.EnumWindows((h, _) =>
        {
            if (!Win32.IsWindowVisible(h) || Win32.IsCloaked(h)) return true;
            if (Win32.GetWindow(h, Win32.GW_OWNER) != IntPtr.Zero) return true;
            var ex = Win32.GetWindowLongPtr(h, Win32.GWL_EXSTYLE).ToInt64();
            if ((ex & Win32.WS_EX_TOOLWINDOW) != 0 && (ex & Win32.WS_EX_APPWINDOW) == 0) return true;
            var cls = Win32.ClassName(h);
            if (SkipClasses.Contains(cls)) return true;
            var title = Win32.WindowText(h);
            if (title.Length == 0) return true;
            Win32.GetWindowThreadProcessId(h, out var pid);
            if (pid == OwnPid) return true;
            var (path, processAumid) = ProcessInfo(pid);
            var aumid = ShellItems.WindowAumid(h) ?? processAumid;
            if (aumid is null && path is not null && Path.GetFileName(path).Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase)) return true;
            result.Add(new AppWindow(h, title, cls == "ApplicationFrameWindow" ? null : path, aumid, cls));
            return true;
        }, IntPtr.Zero);

        // PID перевикористовуються: кеш тримає лише процеси з цього знімка.
        var pids = new HashSet<uint>();
        foreach (var w in result) { Win32.GetWindowThreadProcessId(w.Handle, out var pid); pids.Add(pid); }
        foreach (var dead in ProcessCache.Keys.Where(p => !pids.Contains(p)).ToList()) ProcessCache.Remove(dead);

        // Сталий порядок: у якому вікна з'явилися, а не z-порядок, що міняється з кожним перемиканням.
        foreach (var w in result) if (!FirstSeen.ContainsKey(w.Handle)) FirstSeen[w.Handle] = ++_counter;
        if (FirstSeen.Count > result.Count + 64)
        {
            var alive = result.Select(w => w.Handle).ToHashSet();
            foreach (var dead in FirstSeen.Keys.Where(k => !alive.Contains(k)).ToList()) FirstSeen.Remove(dead);
        }
        return result.OrderBy(w => FirstSeen[w.Handle]).ToList();
    }

    private static (string? Path, string? Aumid) ProcessInfo(uint pid)
    {
        if (ProcessCache.TryGetValue(pid, out var p)) return p;
        if (ProcessCache.Count > 512) ProcessCache.Clear();
        return ProcessCache[pid] = (Win32.ProcessPath(pid), Win32.ProcessAumid(pid));
    }

    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase);

    // Назва програми, а не заголовок вікна: зі списку програм, з опису .exe або з імені файла.
    public static string AppName(AppWindow w)
    {
        if (Names.TryGetValue(w.GroupKey, out var cached)) return cached;
        string? name = null;
        if (w.Aumid is not null) name = AppCatalog.Instance.All.FirstOrDefault(a => string.Equals(a.Target, w.Aumid, StringComparison.OrdinalIgnoreCase))?.Name;
        if (name is null && w.ExePath is not null)
        {
            name = AppCatalog.Instance.All.FirstOrDefault(a => string.Equals(a.Exe, w.ExeName, StringComparison.OrdinalIgnoreCase))?.Name;
            if (name is null)
            {
                try { name = System.Diagnostics.FileVersionInfo.GetVersionInfo(w.ExePath).FileDescription; }
                catch (FileNotFoundException) { }
                if (string.IsNullOrWhiteSpace(name)) name = Path.GetFileNameWithoutExtension(w.ExePath);
            }
        }
        return Names[w.GroupKey] = name ?? w.Title;
    }

    public static bool Matches(PinnedApp pin, AppWindow w)
    {
        if (pin.WindowClass is not null) return string.Equals(pin.WindowClass, w.Class, StringComparison.Ordinal);
        if (w.Aumid is not null && string.Equals(pin.Target, w.Aumid, StringComparison.OrdinalIgnoreCase)) return true;
        if (w.ExePath is not null && string.Equals(pin.Target, w.ExePath, StringComparison.OrdinalIgnoreCase)) return true;
        return pin.Exe is not null && string.Equals(pin.Exe, w.ExeName, StringComparison.OrdinalIgnoreCase);
    }

    // Клік по програмі: немає вікон – запуск; вікно вже спереду – згорнути; інакше – показати найближче.
    public static void Toggle(IReadOnlyList<AppWindow> windows, Action launch)
    {
        if (windows.Count == 0) { launch(); return; }
        var fg = Win32.GetForegroundWindow();
        var front = windows.FirstOrDefault(w => w.Handle == fg);
        if (front is not null && windows.Count == 1) { Win32.ShowWindow(front.Handle, Win32.SW_MINIMIZE); return; }
        // Вікна в сталому порядку: наступне після переднього, по колу – так по черзі видно всі.
        var index = front is null ? 0 : (IndexOf(windows, front) + 1) % windows.Count;
        Activate(windows[index].Handle);
    }

    private static int IndexOf(IReadOnlyList<AppWindow> list, AppWindow item)
    {
        for (var i = 0; i < list.Count; i++) if (list[i].Handle == item.Handle) return i;
        return -1;
    }

    public static void Activate(IntPtr h)
    {
        if (Win32.IsIconic(h)) Win32.ShowWindow(h, Win32.SW_RESTORE);
        Win32.GetWindowThreadProcessId(h, out var pid);
        Win32.AllowSetForegroundWindow((int)pid);
        Win32.SetForegroundWindow(h);
    }
}
