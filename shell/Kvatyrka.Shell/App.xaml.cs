using System.IO;
using System.Windows;
using System.Windows.Threading;
using Kvatyrka.Shell.Services;
using Kvatyrka.Shell.Ui;
using Kvatyrka.Shell.Windows;
using Microsoft.Win32;

namespace Kvatyrka.Shell;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;

    public Settings Settings { get; private set; } = new();
    public bool Preview { get; private set; }
    public bool SettingsOpen => _settingsWindow?.IsVisible == true;

    private Mutex? _single;
    private IslandWindow? _island;
    private WinKey? _winKey;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = e.Args.Select(a => a.ToLowerInvariant()).ToArray();

        // Аварійне повернення панелі Windows: «Kvatyrka.exe --restore».
        if (args.Contains("--restore"))
        {
            TaskbarTakeover.Restore(force: true);
            Shutdown();
            return;
        }

        _single = new Mutex(true, "Stepovyk.Kvatyrka.Shell", out var first);
        if (!first && !args.Contains("--snapshot"))
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnCrash;
        AppDomain.CurrentDomain.UnhandledException += (_, _) => TaskbarTakeover.Restore();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TaskbarTakeover.Restore();

        Preview = args.Contains("--preview") || args.Contains("--snapshot");
        Settings = Settings.Load();
        Theme.Apply(Settings);
        Backdrop.Rebuild();

        if (args.Contains("--snapshot"))
        {
            var dir = e.Args.SkipWhile(a => !a.Equals("--snapshot", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault() ?? "snapshots";
            Snapshot.Run(Path.GetFullPath(dir));
            Shutdown();
            return;
        }

        _island = new IslandWindow();
        _island.Show();
        ApplyTakeover();

        if (Settings.WinKeyOpensMenu) _winKey = new WinKey(() => _island.ToggleMenu());
        SystemState.StartWithWindows = Settings.StartWithWindows && !Preview;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.BeginInvoke(() => { Backdrop.Rebuild(); _island?.Place(); ApplyTakeover(); });
    }

    public void ApplyTakeover()
    {
        if (_island is null) return;
        if (Settings.ReplaceTaskbar && !Preview) TaskbarTakeover.Apply(_island.ReservedPixels);
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
        SystemState.StartWithWindows = Settings.StartWithWindows && !Preview;
        ApplyTakeover();
        _island?.Rebuild();
    }

    public void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
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
        _single?.Dispose();
        base.OnExit(e);
    }
}
