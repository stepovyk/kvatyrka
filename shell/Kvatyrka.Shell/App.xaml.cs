using System.IO;
using System.Windows;
using System.Windows.Threading;
using Kvatyrka.Shell.Services;
using Kvatyrka.Shell.Ui;
using Kvatyrka.Shell.Windows;
using Microsoft.Win32;

namespace Kvatyrka.Shell;

// Kvatyrka.exe:
//   без аргументів       – вікно Кватирки (якщо вона вже працює – показує його там);
//   --startup            – при вході: охоронець повертає особисті налаштування, далі острів, якщо увімкнений;
//   --enforce-machine    – охоронець від імені системи після запуску ПК;
//   --elevated …         – доручення з правами адміністратора (лише покращення з каталогу);
//   --image <профіль> <папка> – збирач застосовує профіль до відкритого образу;
//   --preview, --snapshot <папка>, --restore – перегляд острова, знімки для перевірки, аварійне повернення панелі.
public partial class App : Application
{
    public static new App Current => (App)Application.Current;

    private const string MutexName = "Stepovyk.Kvatyrka";
    private const string ShowEventName = "Stepovyk.Kvatyrka.Show";

    public Settings Settings { get; private set; } = new();
    public bool Preview { get; private set; }
    public bool SettingsOpen => _settingsWindow?.IsVisible == true;
    public bool IslandRunning => _island is not null;

    private Mutex? _single;
    private EventWaitHandle? _showEvent;
    private IslandWindow? _island;
    private WinKey? _winKey;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = e.Args.Select(a => a.ToLowerInvariant()).ToArray();

        if (args.Contains("--restore"))
        {
            TaskbarTakeover.Restore(force: true);
            Shutdown();
            return;
        }
        if (args.Contains("--elevated")) { Shutdown(Improvements.RunElevated(e.Args)); return; }
        if (args.Contains("--enforce-machine")) { Shutdown(Improvements.EnforceMachine()); return; }
        if (args.Contains("--image")) { Shutdown(ImageMode.Run(e.Args)); return; }

        var snapshot = args.Contains("--snapshot");
        var startup = args.Contains("--startup");
        _single = new Mutex(true, MutexName, out var first);
        if (!first && !snapshot)
        {
            // Кватирка вже працює: просимо її показати вікно (при вході – нічого не робимо).
            if (!startup && EventWaitHandle.TryOpenExisting(ShowEventName, out var show)) { show.Set(); show.Dispose(); }
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnCrash;
        AppDomain.CurrentDomain.UnhandledException += (_, _) => TaskbarTakeover.Restore();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TaskbarTakeover.Restore();

        Preview = args.Contains("--preview") || snapshot;
        Settings = Settings.Load();
        Theme.Apply(Settings);
        Backdrop.Rebuild();

        if (snapshot)
        {
            var dir = e.Args.SkipWhile(a => !a.Equals("--snapshot", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault() ?? "snapshots";
            Snapshot.Run(Path.GetFullPath(dir));
            Shutdown();
            return;
        }

        // Спершу слухаємо другий запуск, щоб він не загубився, поки охоронець працює.
        ListenForShow();
        if (startup) Improvements.EnforceUser();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.BeginInvoke(() => { Backdrop.Rebuild(); _island?.Place(); ApplyTakeover(); });

        if (Settings.Island || Preview) StartIsland();
        if (!startup) ShowSettings();
        else if (_island is null) Shutdown();
    }

    // Другий запуск Kvatyrka.exe показує вікно цієї копії.
    private void ListenForShow()
    {
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var thread = new Thread(() =>
        {
            while (_showEvent.WaitOne())
            {
                if (ShuttingDown) return;
                Dispatcher.BeginInvoke(ShowSettings);
            }
        }) { IsBackground = true, Name = "Kvatyrka.Show" };
        thread.Start();
    }

    private void StartIsland()
    {
        if (_island is not null) return;
        _island = new IslandWindow();
        _island.Show();
        ApplyTakeover();
        if (Settings.WinKeyOpensMenu) _winKey = new WinKey(() => _island?.ToggleMenu());
    }

    private void StopIsland()
    {
        _winKey?.Dispose();
        _winKey = null;
        TaskbarTakeover.Restore();
        _island?.Retire();
        _island = null;
    }

    // Вмикає чи вимикає острів одразу, без перезапуску.
    public void SetIsland(bool on)
    {
        Settings.Island = on;
        Settings.Save();
        if (on) StartIsland(); else StopIsland();
        Improvements.UpdateAutostart();
    }

    public void ApplyTakeover()
    {
        if (_island is null) return;
        if (Settings.Island && !Preview) TaskbarTakeover.Apply(_island.ReservedPixels);
        else TaskbarTakeover.Restore();
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (e.Category == UserPreferenceCategory.Desktop) Backdrop.Rebuild();
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color && Settings.Theme == Appearance.Auto) Theme.Apply(Settings);
        });
    }

    public void SaveSettings()
    {
        Settings.Save();
        Theme.Apply(Settings);
        if (_island is not null && Settings.WinKeyOpensMenu != (_winKey is not null))
        {
            _winKey?.Dispose();
            _winKey = Settings.WinKeyOpensMenu ? new WinKey(() => _island?.ToggleMenu()) : null;
        }
        _island?.Rebuild();
    }

    public void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) =>
            {
                _settingsWindow = null;
                // Без острова Кватирці нема чого працювати у фоні.
                if (_island is null) Shutdown();
            };
        }
        _settingsWindow.Show();
        if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }

    public bool ShuttingDown { get; private set; }

    public void Quit()
    {
        ShuttingDown = true;
        _winKey?.Dispose();
        TaskbarTakeover.Restore();
        Shutdown();
    }

    private void OnCrash(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Write($"Збій: {e.Exception}");
        TaskbarTakeover.Restore();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ShuttingDown = true;
        _winKey?.Dispose();
        TaskbarTakeover.Restore();
        _showEvent?.Set();
        _showEvent?.Dispose();
        _single?.Dispose();
        base.OnExit(e);
    }
}
