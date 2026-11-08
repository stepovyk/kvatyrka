using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using Kvatyrka.Shell.Native;
using Microsoft.Win32;

namespace Kvatyrka.Shell.Services;

// Справжні системні перемикачі: усе читається й пишеться в тих самих місцях, що й у параметрах Windows.
public static class SystemState
{
    private const string GameBarKey = @"Software\Microsoft\GameBar";
    private const string ToastsKey = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static int? ReadDword(string key, string name)
    {
        using var k = Registry.CurrentUser.OpenSubKey(key);
        return k?.GetValue(name) is int v ? v : null;
    }

    private static void WriteDword(string key, string name, int value)
    {
        using var k = Registry.CurrentUser.CreateSubKey(key, true);
        k.SetValue(name, value, RegistryValueKind.DWord);
    }

    // Ігровий режим Windows: за замовчуванням увімкнений, якщо значення немає.
    public static bool GameMode
    {
        get => ReadDword(GameBarKey, "AutoGameModeEnabled") != 0;
        set => WriteDword(GameBarKey, "AutoGameModeEnabled", value ? 1 : 0);
    }

    // «Не турбувати»: вимикає спливні сповіщення так само, як перемикач у Параметрах.
    public static bool DoNotDisturb
    {
        get => ReadDword(ToastsKey, "NOC_GLOBAL_SETTING_TOASTS_ENABLED") == 0;
        set => WriteDword(ToastsKey, "NOC_GLOBAL_SETTING_TOASTS_ENABLED", value ? 0 : 1);
    }

    // Темна тема програм і Windows.
    public static bool SystemDark
    {
        get => ReadDword(PersonalizeKey, "AppsUseLightTheme") == 0;
        set
        {
            WriteDword(PersonalizeKey, "AppsUseLightTheme", value ? 0 : 1);
            WriteDword(PersonalizeKey, "SystemUsesLightTheme", value ? 0 : 1);
        }
    }

    public static bool NetworkUp => NetworkInterface.GetIsNetworkAvailable();

    public static string NetworkName
    {
        get
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 1 : 0)
                .FirstOrDefault(n => n.GetIPProperties().GatewayAddresses.Count > 0);
            return nic is null ? "Немає мережі" : nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : "Мережа";
        }
    }

    // Мова введення активного вікна: «УКР», «ENG» тощо.
    public static string InputLanguage()
    {
        var fg = Win32.GetForegroundWindow();
        var tid = Win32.GetWindowThreadProcessId(fg, out _);
        var langId = (int)(Win32.GetKeyboardLayout(tid).ToInt64() & 0xFFFF);
        try
        {
            var c = CultureInfo.GetCultureInfo(langId);
            return c.TwoLetterISOLanguageName switch
            {
                "uk" => "УКР",
                "en" => "ENG",
                _ => c.ThreeLetterWindowsLanguageName.Length > 0 ? c.ThreeLetterWindowsLanguageName : c.TwoLetterISOLanguageName.ToUpperInvariant(),
            };
        }
        catch (CultureNotFoundException) { return "—"; }
    }

    public static void NextInputLanguage()
    {
        var fg = Win32.GetForegroundWindow();
        if (fg != IntPtr.Zero) Win32.PostMessage(fg, Win32.WM_INPUTLANGCHANGEREQUEST, (IntPtr)Win32.INPUTLANGCHANGE_FORWARD, IntPtr.Zero);
    }

    public static void Sleep() => Win32.SetSuspendState(false, false, false);
    public static void Restart() => Run("shutdown.exe", "/r /t 0");
    public static void Shutdown() => Run("shutdown.exe", "/s /t 0");
    public static void SignOut() => Run("shutdown.exe", "/l");
    public static void Lock() => Run("rundll32.exe", "user32.dll,LockWorkStation");
    public static void OpenWindowsSettings(string page = "") => Run("explorer.exe", $"ms-settings:{page}");

    private static void Run(string exe, string args)
    {
        try
        {
            var path = Path.Combine(Environment.SystemDirectory, exe);
            Process.Start(new ProcessStartInfo(File.Exists(path) ? path : exe, args) { UseShellExecute = false, CreateNoWindow = true });
        }
        catch (System.ComponentModel.Win32Exception ex) { Log.Write($"Не вдалося виконати {exe}: {ex.Message}"); }
    }
}
