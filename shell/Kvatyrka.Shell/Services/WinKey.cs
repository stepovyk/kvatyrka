using System.Runtime.InteropServices;
using System.Windows.Threading;
using Kvatyrka.Shell.Native;

namespace Kvatyrka.Shell.Services;

// Окреме натискання Win відкриває меню Кватирки замість «Пуску». Поєднання Win+E, Win+D тощо працюють як раніше.
// Перехоплення живе в окремому потоці зі своїм циклом повідомлень: зайнятий інтерфейс не може його загальмувати,
// інакше Windows мовчки знімає перехоплення після тайм-ауту.
public sealed class WinKey : IDisposable
{
    private const ushort DummyKey = 0xE8; // незайнятий код клавіші: гасить «Пуск», нічого не робить
    private const uint Injected = 0x10;
    private const int WM_QUIT = 0x0012;

    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint threadId, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }

    private readonly Win32.LowLevelKeyboardProc _proc;
    private readonly Action _onPress;
    private readonly Dispatcher _dispatcher;
    private readonly Thread _thread;
    private IntPtr _hook;
    private uint _threadId;
    private bool _winDown, _combo;

    public WinKey(Action onPress)
    {
        _onPress = onPress;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _proc = Hook;
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            _hook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _proc, Win32.GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero) Log.Write($"Не вдалося перехопити клавішу Win: {Marshal.GetLastWin32Error()}");
            ready.Set();
            while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
            if (_hook != IntPtr.Zero) Win32.UnhookWindowsHookEx(_hook);
        }) { IsBackground = true, Name = "Kvatyrka.WinKey" };
        _thread.Start();
        ready.Wait();
    }

    private IntPtr Hook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0) return Win32.CallNextHookEx(_hook, code, wParam, lParam);
        var k = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
        if ((k.flags & Injected) != 0) return Win32.CallNextHookEx(_hook, code, wParam, lParam);
        var msg = wParam.ToInt32();
        var isWin = k.vkCode is Win32.VK_LWIN or Win32.VK_RWIN;
        var down = msg is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN;
        var up = msg is Win32.WM_KEYUP or Win32.WM_SYSKEYUP;
        if (isWin && down)
        {
            // Усередині перехоплення стан клавіші ще не оновлено: «не натиснута» означає свіже натискання.
            // Так стан не залипає, якщо відпускання пішло на захищений стіл (Win+L).
            if (!_winDown || (GetAsyncKeyState((int)k.vkCode) & 0x8000) == 0) { _winDown = true; _combo = false; }
        }
        else if (isWin && up)
        {
            var alone = _winDown && !_combo;
            _winDown = false;
            // Як AutoHotkey: ковтаємо відпускання Win, вставляємо порожню клавішу й відпускаємо Win самі – «Пуск» не відкривається.
            if (alone && Win32.TapKeyThenRelease(DummyKey, (ushort)k.vkCode))
            {
                _dispatcher.BeginInvoke(_onPress);
                return 1;
            }
        }
        else if (down && _winDown)
        {
            _combo = true;
        }
        return Win32.CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_threadId != 0) PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _threadId = 0;
    }
}
