using System.Runtime.InteropServices;
using Kvatyrka.Shell.Native;

namespace Kvatyrka.Shell.Services;

// Ховає панель завдань Windows і залишає місце під острів. Усе повертається при виході.
public static class TaskbarTakeover
{
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    private const int SM_CXSCREEN = 0, SM_CYSCREEN = 1;

    public static readonly int TaskbarCreatedMessage = Win32.RegisterWindowMessage("TaskbarCreated");
    public static bool Active { get; private set; }

    // Лише головна панель: острів живе на основному моніторі, а на інших лишаються панелі Windows.
    private static IEnumerable<IntPtr> Taskbars()
    {
        var main = Win32.FindWindow("Shell_TrayWnd", null);
        if (main != IntPtr.Zero) yield return main;
    }

    // reservedPx – висота смуги знизу під острів у фізичних пікселях.
    public static void Apply(int reservedPx)
    {
        foreach (var t in Taskbars()) Win32.ShowWindow(t, Win32.SW_HIDE);
        var rect = new Win32.RECT { Left = 0, Top = 0, Right = GetSystemMetrics(SM_CXSCREEN), Bottom = GetSystemMetrics(SM_CYSCREEN) - reservedPx };
        if (!Win32.SystemParametersInfo(Win32.SPI_SETWORKAREA, 0, ref rect, Win32.SPIF_SENDCHANGE))
            Log.Write($"Не вдалося задати робочу область: {Marshal.GetLastWin32Error()}");
        Active = true;
    }

    public static void Restore(bool force = false)
    {
        if (!Active && !force) return;
        foreach (var t in Taskbars()) Win32.ShowWindow(t, Win32.SW_SHOW);
        // Показана панель сама перераховує робочу область; для певності віддаємо весь екран мінус її висоту.
        var bar = Win32.FindWindow("Shell_TrayWnd", null);
        var rect = new Win32.RECT { Left = 0, Top = 0, Right = GetSystemMetrics(SM_CXSCREEN), Bottom = GetSystemMetrics(SM_CYSCREEN) };
        if (bar != IntPtr.Zero && GetWindowRect(bar, out var tb) && tb.Top > 0) rect.Bottom = tb.Top;
        Win32.SystemParametersInfo(Win32.SPI_SETWORKAREA, 0, ref rect, Win32.SPIF_SENDCHANGE);
        Active = false;
    }

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out Win32.RECT rect);
}
