using System.Runtime.InteropServices;
using System.Windows.Threading;
using Kvatyrka.Shell.Native;

namespace Kvatyrka.Shell.Services;

// Окреме натискання Win відкриває меню Кватирки замість «Пуску». Поєднання Win+E, Win+D тощо працюють як раніше.
public sealed class WinKey : IDisposable
{
    private const ushort DummyKey = 0xE8; // незайнятий код клавіші: гасить «Пуск», нічого не робить
    private const uint Injected = 0x10;
    private readonly Win32.LowLevelKeyboardProc _proc;
    private readonly Action _onPress;
    private readonly Dispatcher _dispatcher;
    private IntPtr _hook;
    private bool _winDown, _combo;

    public WinKey(Action onPress)
    {
        _onPress = onPress;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _proc = Hook;
        _hook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _proc, Win32.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) Log.Write($"Не вдалося перехопити клавішу Win: {Marshal.GetLastWin32Error()}");
    }

    private IntPtr Hook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var k = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
            if ((k.flags & Injected) != 0) return Win32.CallNextHookEx(_hook, code, wParam, lParam);
            var msg = wParam.ToInt32();
            var isWin = k.vkCode is Win32.VK_LWIN or Win32.VK_RWIN;
            var down = msg is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN;
            var up = msg is Win32.WM_KEYUP or Win32.WM_SYSKEYUP;
            if (isWin && down)
            {
                if (!_winDown) { _winDown = true; _combo = false; }
            }
            else if (isWin && up)
            {
                var alone = _winDown && !_combo;
                _winDown = false;
                if (alone)
                {
                    // Як AutoHotkey: ковтаємо відпускання Win, вставляємо порожню клавішу й відпускаємо Win самі – «Пуск» не відкривається.
                    Win32.TapKeyThenRelease(DummyKey, (ushort)k.vkCode);
                    _dispatcher.BeginInvoke(_onPress);
                    return 1;
                }
            }
            else if (down && _winDown)
            {
                _combo = true;
            }
        }
        return Win32.CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) { Win32.UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
    }
}
