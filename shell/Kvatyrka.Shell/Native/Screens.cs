using System.Runtime.InteropServices;

namespace Kvatyrka.Shell.Native;

internal static class Screens
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public Win32.RECT rcMonitor, rcWork; public uint dwFlags; }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out Win32.RECT rect);
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;
    private const uint MONITORINFOF_PRIMARY = 1;

    // Вікно спереду займає весь основний монітор (повноекранна гра, відео, презентація).
    public static bool ForegroundIsFullscreen()
    {
        var fg = Win32.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        var cls = Win32.ClassName(fg);
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        if (!GetWindowRect(fg, out var r)) return false;
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(MonitorFromWindow(fg, MONITOR_DEFAULTTOPRIMARY), ref info) || (info.dwFlags & MONITORINFOF_PRIMARY) == 0) return false;
        var m = info.rcMonitor;
        return r.Left <= m.Left && r.Top <= m.Top && r.Right >= m.Right && r.Bottom >= m.Bottom;
    }
}
