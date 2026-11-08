using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using Kvatyrka.Core;
using Microsoft.Win32;
using Windows.Management.Deployment;

namespace Kvatyrka.Shell.Services;

// Покращення Windows із каталогу: читання стану, застосування, права адміністратора, охоронець, програми.
public static class Improvements
{
    public static Catalog Catalog { get; } = Core.Catalog.Builtin();
    public static IRegistry Registry { get; } = new WindowsRegistry();
    public static TweakEngine Engine { get; } = new(Catalog, Registry);

    public static string InstalledExe => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Kvatyrka", "Kvatyrka.exe");
    private const string TaskName = "Kvatyrka";
    private const string MarkerKey = @"SOFTWARE\Kvatyrka";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsElevated
    {
        get
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    // ---------- застосування з інтерфейсу ----------

    public enum Elevation { Ok, Declined, Failed }

    // Підсумок для інтерфейсу: що не вдалося й чи людина відмовила в правах.
    public sealed record Outcome(bool Declined, IReadOnlyList<string> Failed)
    {
        public bool Ok => !Declined && Failed.Count == 0;
    }

    // Особисті зміни – одразу, спільні для ПК – одним запитом прав адміністратора на весь набір.
    // Якщо людина відмовила в правах, не змінюється нічого, щоб покращення не лишилися навпіл.
    public static async Task<Outcome> ApplyAsync(IReadOnlyList<(Tweak Tweak, string Value)> changes)
    {
        var request = new ElevatedRequest();
        foreach (var (t, v) in changes)
            if (Engine.NeedsMachine(t, v)) request.Tweaks.Add((t.Id, v));
        if (!request.IsEmpty && await ElevateAsync(request) == Elevation.Declined) return new Outcome(true, Array.Empty<string>());
        var failed = new List<string>();
        foreach (var (t, v) in changes)
        {
            try { Engine.Apply(t, v, Scope.User); }
            catch (Exception ex)
            {
                Log.Write($"{t.Id}: не вдалося змінити особисті налаштування: {ex}");
            }
            // Перевіряємо підсумок, а не лише свою половину: спільну міг не застосувати процес адміністратора.
            // «Як у Windows» – це будь-що, крім значень Кватирки (могло повернутися й нестандартне значення, що було до неї).
            var done = t.IsWindowsDefault(v) ? Engine.State(t) == TweakState.Off : Engine.Current(t) == v;
            if (!done) failed.Add(t.Name);
        }
        if (changes.Any(c => c.Tweak.Refresh == "mouse")) RefreshMouse();
        Broadcast();
        UpdateAutostart();
        return new Outcome(false, failed);
    }

    // Запускає копію Кватирки з правами адміністратора й чекає завершення.
    public static Task<Elevation> ElevateAsync(ElevatedRequest request) => Task.Run(() =>
    {
        var exe = Environment.ProcessPath;
        if (exe is null) { Log.Write("Невідомий шлях до Kvatyrka.exe"); return Elevation.Failed; }
        // Аргументи – лише id з каталогу, без пробілів і лапок, тож їх можна просто з'єднати.
        var psi = new ProcessStartInfo(exe, string.Join(' ', request.ToArgs())) { UseShellExecute = true, Verb = "runas" };
        try
        {
            using var p = Process.Start(psi);
            if (p is null) { Log.Write("Кватирка з правами адміністратора не запустилась"); return Elevation.Failed; }
            p.WaitForExit();
            if (p.ExitCode != 0) Log.Write($"Кватирка з правами адміністратора завершилась з кодом {p.ExitCode}, подробиці – у журналі {MachineLog}");
            return p.ExitCode == 0 ? Elevation.Ok : Elevation.Failed;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return Elevation.Declined; // людина натиснула «Ні»
        }
        catch (Win32Exception ex)
        {
            Log.Write($"Не вдалося запустити Кватирку з правами адміністратора: {ex.Message}");
            return Elevation.Failed;
        }
    });

    // ---------- процес з правами адміністратора ----------

    // Журнал процесів адміністратора й системи – спільний для ПК.
    public static string MachineLog => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Kvatyrka", "kvatyrka.log");

    public static int RunElevated(string[] args)
    {
        Log.Path = MachineLog;
        ElevatedRequest r;
        try { r = ElevatedRequest.Parse(args, Catalog); }
        catch (CatalogException ex) { Log.Write($"Відхилене доручення: {ex.Message}"); return 2; }
        var failed = false;
        foreach (var (id, value) in r.Tweaks) failed |= !ApplyMachine(Catalog[id], value);
        if (r.Apps.Count > 0) failed |= !Guard("прибирання програм", () => RemoveApps(r.Apps));
        if (r.Guardian is { } g) failed |= !Guard("охоронець", () => g ? InstallGuardian() : RemoveGuardian());
        Broadcast();
        return failed ? 1 : 0;
    }

    private static bool Guard(string what, Func<bool> action)
    {
        try { return action(); }
        catch (Exception ex)
        {
            Log.Write($"{what}: {ex}");
            return false;
        }
    }

    // Спершу команда (напр. powercfg), потім реєстр і запам'ятований вибір: якщо команда не вдасться, стан лишиться чесним.
    private static bool ApplyMachine(Tweak t, string value) => Guard(t.Id, () =>
    {
        if (Engine.CommandFor(t, value) is { } command && !RunCommand(command)) return false;
        Engine.Apply(t, value, Scope.Machine);
        return true;
    });

    // Збірка Windows разом з номером оновлення: змінилась – отже, було оновлення.
    private static string OsBuild =>
        $"{Registry.Get(Hive.Machine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild")}.{Registry.Get(Hive.Machine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "UBR")}";

    // Охоронець працює лише після оновлення Windows (чи вперше): те, що людина сама змінила в Параметрах, він не чіпає.
    private static string CheckKey(Hive hive) => hive == Hive.Machine ? MarkerKey : @"Software\Kvatyrka";

    private static bool UpdatedSinceLastCheck(Hive hive) => Registry.Get(hive, CheckKey(hive), "CheckedBuild") as string != OsBuild;

    // Позначка ставиться лише після вдалої перевірки, інакше охоронець спробує знову при наступному запуску.
    private static void MarkChecked(Hive hive) => Registry.Set(hive, CheckKey(hive), "CheckedBuild", RegType.String, OsBuild);

    // Охоронець від імені системи: повертає спільні налаштування після оновлень Windows.
    public static int EnforceMachine()
    {
        Log.Path = MachineLog;
        var failed = false;
        try
        {
            if (!UpdatedSinceLastCheck(Hive.Machine)) return 0;
            foreach (var (t, value) in Engine.Drifted(Scope.Machine))
            {
                var ok = ApplyMachine(t, value);
                Log.Write(ok ? $"Охоронець повернув: {t.Name}" : $"Охоронець не зміг повернути: {t.Name}");
                failed |= !ok;
            }
            if (!failed) MarkChecked(Hive.Machine);
        }
        catch (Exception ex)
        {
            Log.Write($"Охоронець: {ex}");
            return 1;
        }
        return failed ? 1 : 0;
    }

    // Під час входу людини: повертає особисті налаштування.
    public static void EnforceUser()
    {
        try
        {
            if (!UpdatedSinceLastCheck(Hive.User)) return;
            var fixedUp = Engine.Enforce(Scope.User);
            foreach (var (t, _) in fixedUp) Log.Write($"Охоронець повернув: {t.Name}");
            if (fixedUp.Any(f => f.Tweak.Refresh == "mouse")) RefreshMouse();
            if (fixedUp.Count > 0) Broadcast();
            MarkChecked(Hive.User);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            Log.Write($"Охоронець: {ex.Message}");
        }
    }

    private static bool RunCommand(string command)
    {
        var space = command.IndexOf(' ');
        var exe = space < 0 ? command : command[..space];
        var args = space < 0 ? string.Empty : command[(space + 1)..];
        var path = Path.Combine(Environment.SystemDirectory, exe);
        try
        {
            using var p = Process.Start(new ProcessStartInfo(File.Exists(path) ? path : exe, args) { UseShellExecute = false, CreateNoWindow = true });
            p?.WaitForExit();
            var ok = p?.ExitCode == 0;
            if (!ok) Log.Write($"Команда «{command}» завершилась з кодом {p?.ExitCode}");
            return ok;
        }
        catch (Win32Exception ex)
        {
            Log.Write($"Не вдалося виконати «{command}»: {ex.Message}");
            return false;
        }
    }

    // ---------- охоронець ----------

    // Позначку ставить лише успішне встановлення; якщо задачу прибрали вручну, охоронець вважається вимкненим.
    public static bool GuardianInstalled
    {
        get
        {
            if (Registry.Get(Hive.Machine, MarkerKey, "Guardian") is not 1) return false;
            try
            {
                using var tree = Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\{TaskName}");
                return tree is not null;
            }
            catch (SecurityException)
            {
                return true; // перелік задач закритий для читання – довіряємо позначці
            }
        }
    }

    private static bool InstallGuardian()
    {
        var exe = InstalledExe;
        var current = Environment.ProcessPath!;
        if (!string.Equals(Path.GetFullPath(current), Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
            try { File.Copy(current, exe, true); }
            catch (IOException ex)
            {
                if (!File.Exists(exe)) { Log.Write($"Не вдалося встановити Кватирку: {ex.Message}"); return false; }
                // Встановлена копія зараз працює (острів): лишаємо її, оновиться після закриття острова.
                Log.Write($"Встановлену Кватирку не оновлено, бо вона працює: {ex.Message}");
            }
        }
        // Задача з опису: стартує й від батареї, після запуску ПК від імені системи.
        var xml = Path.Combine(Path.GetTempPath(), $"kvatyrka-task-{Environment.ProcessId}.xml");
        File.WriteAllText(xml, TaskXml(exe), System.Text.Encoding.Unicode);
        bool ok;
        try { ok = RunCommand($"schtasks.exe /create /tn {TaskName} /xml \"{xml}\" /f"); }
        finally { File.Delete(xml); }
        if (!ok) return false;
        CreateShortcut(exe);
        Registry.Set(Hive.Machine, MarkerKey, "Guardian", RegType.DWord, 1);
        Log.Write("Охоронець увімкнений");
        return true;
    }

    private static string TaskXml(string exe) => $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo>
            <Description>Кватирка: повертає вибрані покращення, якщо оновлення Windows їх скинуло.</Description>
          </RegistrationInfo>
          <Triggers>
            <BootTrigger><Enabled>true</Enabled></BootTrigger>
          </Triggers>
          <Principals>
            <Principal id="System"><UserId>S-1-5-18</UserId><RunLevel>HighestAvailable</RunLevel></Principal>
          </Principals>
          <Settings>
            <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
            <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
            <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
            <StartWhenAvailable>true</StartWhenAvailable>
            <ExecutionTimeLimit>PT10M</ExecutionTimeLimit>
            <Enabled>true</Enabled>
          </Settings>
          <Actions Context="System">
            <Exec><Command>"{SecurityElement.Escape(exe)}"</Command><Arguments>--enforce-machine</Arguments></Exec>
          </Actions>
        </Task>
        """;

    private static bool RemoveGuardian()
    {
        // Задачу могли прибрати вручну – тоді вимикати вже нічого.
        if (!RunCommand($"schtasks.exe /delete /tn {TaskName} /f") && RunCommand($"schtasks.exe /query /tn {TaskName}")) return false;
        Registry.DeleteValue(Hive.Machine, MarkerKey, "Guardian");
        Log.Write("Охоронець вимкнений");
        return true;
    }

    // Ярлик у Пуску для всіх, щоб Кватирку легко знайти пошуком.
    private static void CreateShortcut(string exe)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms));
        var link = Path.Combine(dir, "Кватирка.lnk");
        try
        {
            var shell = Type.GetTypeFromProgID("WScript.Shell") ?? throw new COMException("Немає WScript.Shell");
            dynamic ws = Activator.CreateInstance(shell)!;
            dynamic sc = ws.CreateShortcut(link);
            sc.TargetPath = exe;
            sc.WorkingDirectory = Path.GetDirectoryName(exe);
            sc.Description = "Налаштування Кватирки";
            sc.Save();
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            Log.Write($"Не вдалося створити ярлик: {ex.Message}");
        }
    }

    // Автозапуск при вході: потрібен охоронцю й острову. Запис – у профілі людини, тож робиться без прав адміністратора.
    public static void UpdateAutostart()
    {
        var want = GuardianInstalled || App.Current.Settings.Island;
        var exe = File.Exists(InstalledExe) ? InstalledExe : Environment.ProcessPath;
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (want && exe is not null) k.SetValue("Kvatyrka", $"\"{exe}\" --startup");
            else k.DeleteValue("Kvatyrka", false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            Log.Write($"Не вдалося змінити автозапуск: {ex.Message}");
        }
    }

    // ---------- оновлення вигляду ----------

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, int[] value, uint winIni);

    // Повідомляє Windows і програмам, що налаштування змінились.
    public static void Broadcast()
    {
        foreach (var area in new[] { "Policy", "TraySettings", "Environment" })
            SendMessageTimeout((IntPtr)0xFFFF, 0x001A /* WM_SETTINGCHANGE */, IntPtr.Zero, area, 0x0002 /* SMTO_ABORTIFHUNG */, 1000, out _);
    }

    // Прискорення миші діє одразу, без повторного входу.
    private static void RefreshMouse()
    {
        using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse");
        int Read(string n) => int.TryParse(k?.GetValue(n) as string, out var v) ? v : 0;
        SystemParametersInfo(0x0004 /* SPI_SETMOUSE */, 0, new[] { Read("MouseThreshold1"), Read("MouseThreshold2"), Read("MouseSpeed") }, 0x01 | 0x02);
    }

    // true – Провідник справді перезапустився.
    public static bool RestartExplorer()
    {
        var session = Process.GetCurrentProcess().SessionId;
        var ok = true;
        foreach (var p in Process.GetProcessesByName("explorer").Where(p => p.SessionId == session))
        {
            try { p.Kill(); ok &= p.WaitForExit(5000); }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { ok = false; Log.Write($"Провідник не закрився: {ex.Message}"); }
            finally { p.Dispose(); }
        }
        // Провідник зазвичай сам запускається знову; якщо ні – запускаємо.
        Thread.Sleep(1500);
        if (Process.GetProcessesByName("explorer").Any(p => p.SessionId == session)) return ok;
        try
        {
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { UseShellExecute = true });
            return ok;
        }
        catch (Win32Exception ex)
        {
            Log.Write($"Провідник не запустився: {ex.Message}");
            return false;
        }
    }

    // ---------- нав'язані програми ----------

    // Прибирає програми одним запитом прав; разом вмикає потрібні їм покращення (напр. без Game Bar – без запису ігор).
    public static async Task<Outcome> RemoveAppsAsync(IReadOnlyList<AppEntry> apps)
    {
        var request = new ElevatedRequest();
        request.Apps.AddRange(apps.Select(a => a.Id));
        var with = apps.Where(a => a.With is not null).Select(a => Catalog[a.With!]).Distinct().ToList();
        foreach (var t in with) request.Tweaks.Add((t.Id, "on"));
        var elevation = await ElevateAsync(request);
        if (elevation == Elevation.Declined) return new Outcome(true, Array.Empty<string>());
        // Особиста половина супутніх покращень – навіть якщо частину програм прибрати не вдалося.
        var tweaks = with.Count > 0 ? await ApplyAsync(with.Select(t => (t, "on")).ToList()) : new Outcome(false, Array.Empty<string>());
        var left = (await InstalledJunkAsync())?.Where(a => apps.Any(x => x.Id == a.Id)).Select(a => a.Name).ToList() ?? apps.Select(a => a.Name).ToList();
        return new Outcome(false, left.Concat(tweaks.Failed).ToList());
    }

    // Програми з каталогу, встановлені в цього користувача; null – список не прочитався.
    public static Task<List<AppEntry>?> InstalledJunkAsync() => Task.Run(() =>
    {
        try
        {
            var names = new PackageManager().FindPackagesForUser(string.Empty).Select(p => p.Id.Name).ToList();
            return Catalog.Apps.Where(a => names.Any(a.Matches)).ToList();
        }
        catch (Exception ex)
        {
            Log.Write($"Не вдалося отримати список програм: {ex.Message}");
            return null;
        }
    });

    // Прибирає програми для всіх користувачів і з образу нових профілів. Повернути можна з Microsoft Store.
    private static bool RemoveApps(IEnumerable<string> ids)
    {
        var pm = new PackageManager();
        var ok = true;
        var apps = Catalog.Apps.Where(a => ids.Contains(a.Id)).ToList();
        foreach (var p in pm.FindPackages().Where(p => apps.Any(a => a.Matches(p.Id.Name))).ToList())
        {
            try
            {
                var r = pm.RemovePackageAsync(p.Id.FullName, RemovalOptions.RemoveForAllUsers).AsTask().GetAwaiter().GetResult();
                if (r.ExtendedErrorCode is not null) { ok = false; Log.Write($"{p.Id.Name}: {r.ErrorText}"); }
                else Log.Write($"Прибрано: {p.Id.Name}");
            }
            catch (Exception ex) { ok = false; Log.Write($"{p.Id.Name}: {ex.Message}"); }
        }
        // Лише те, що справді лежить в образі нових профілів, – будь-яка помилка тут уже справжня.
        foreach (var family in pm.FindProvisionedPackages().Where(p => apps.Any(a => a.Matches(p.Id.Name))).Select(p => p.Id.FamilyName).Distinct().ToList())
        {
            try
            {
                pm.DeprovisionPackageForAllUsersAsync(family).AsTask().GetAwaiter().GetResult();
                Log.Write($"Прибрано з нових профілів: {family}");
            }
            catch (Exception ex) { ok = false; Log.Write($"{family}: {ex.Message}"); }
        }
        return ok;
    }
}
