using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Kvatyrka.Shell.Native;
using Kvatyrka.Shell.Services;
using Kvatyrka.Shell.Ui;

namespace Kvatyrka.Shell.Windows;

// Острів: єдина панель Кватирки внизу по центру екрана.
public sealed class IslandWindow : ShellWindow
{
    public static Rect Bounds { get; private set; }

    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly MenuWindow _menu = new();
    private readonly QuickWindow _quick = new();
    private readonly ClockWindow _clock = new();

    internal PopupWindow Menu => _menu;
    internal PopupWindow Quick => _quick;
    internal PopupWindow Clock => _clock;
    private readonly ShellButton _menuButton, _quickButton, _langButton, _clockButton;
    private readonly TextBlock _lang = new(), _time = new(), _date = new();
    private readonly Dictionary<string, ShellButton> _appButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Glass _glass;
    private List<AppWindow> _windows = new();
    private string _layoutKey = string.Empty;

    public IslandWindow() : base(false)
    {
        Title = "Кватирка";
        Left = -10000; // до першого розміщення – за межами екрана
        _menuButton = Cell(new Mark(), "Меню Кватирки", () => _menu.Toggle());
        var quickIcons = new Grid { Width = 96 };
        quickIcons.ColumnDefinitions.Add(new ColumnDefinition());
        quickIcons.ColumnDefinitions.Add(new ColumnDefinition());
        var wifi = new Ui.Icon { Kind = IconKind.Wifi, HorizontalAlignment = HorizontalAlignment.Center };
        var vol = new Ui.Icon { Kind = IconKind.Volume, HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetColumn(vol, 1);
        quickIcons.Children.Add(wifi);
        quickIcons.Children.Add(vol);
        _quickButton = Cell(quickIcons, "Швидкі налаштування", () => _quick.Toggle(), 96);
        _lang.Style = (Style)FindResource("label");
        _langButton = Cell(_lang, "Мова введення", SystemState.NextInputLanguage, 56);
        _time.Style = (Style)FindResource("title");
        _time.HorizontalAlignment = HorizontalAlignment.Right;
        _date.Style = (Style)FindResource("caption");
        _date.HorizontalAlignment = HorizontalAlignment.Right;
        _date.Margin = new Thickness(0, 4, 0, 0);
        var clockStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        clockStack.Children.Add(_time);
        clockStack.Children.Add(_date);
        _clockButton = Cell(clockStack, "Календар і сповіщення", () => _clock.Toggle(), 126);
        _clockButton.HorizontalContentAlignment = HorizontalAlignment.Right;
        _clockButton.Padding = new Thickness(0, 0, 12, 0);

        _menu.OpenChanged += () => _menuButton.IsActive = _menu.IsOpen;
        _quick.OpenChanged += () => _quickButton.IsActive = _quick.IsOpen;
        _clock.OpenChanged += () => _clockButton.IsActive = _clock.IsOpen;

        Content = Frame(_row, "glass", "shadow-island", 22, new Thickness(7, 0, 7, 0), out _glass);
        _glass.Height = IslandHeight;
        _timer.Tick += (_, _) => Tick();
        Loaded += (_, _) =>
        {
            Rebuild();
            _timer.Start();
            AppCatalog.Instance.WarmUp(IconPixels);
        };
        DpiChanged += (_, e) =>
        {
            Scale = e.NewDpi.DpiScaleX;
            _layoutKey = string.Empty;
            Backdrop.Rebuild();
            Rebuild();
            Place();
            App.Current.ApplyTakeover();
        };
        SizeChanged += (_, _) => Place();
    }

    public int ReservedPixels
    {
        get
        {
            var scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
            return (int)Math.Ceiling((Gap + IslandHeight + Gap) * scale);
        }
    }

    public void ToggleMenu() => _menu.Toggle();

    // Острів вимкнули в налаштуваннях: зупиняємо оновлення й закриваємо всі його вікна.
    public void Retire()
    {
        _timer.Stop();
        foreach (var p in new PopupWindow[] { _menu, _quick, _clock }) { p.Closable = true; p.Close(); }
        Close();
    }

    private ShellButton Cell(UIElement content, string name, Action click, double width = 48)
    {
        var b = new ShellButton { Content = content, Width = width, ToolTip = name };
        System.Windows.Automation.AutomationProperties.SetName(b, name);
        b.Click += (_, _) => click();
        return b;
    }

    private static Border Divider()
    {
        var d = new Border { Width = 1, Height = 28 };
        d.SetResourceReference(Border.BackgroundProperty, "divider");
        return d;
    }

    // Порядок: знак, розділювач, закріплені, запущені, розділювач, швидкі налаштування, мова, годинник.
    public void Rebuild() => Rebuild(WindowTracker.Snapshot());

    // Без рекурсії: знімок вікон береться один раз, і перебудова не викликає сама себе знову,
    // навіть якщо якась програма безперервно показує й ховає вікна.
    private void Rebuild(List<AppWindow> windows)
    {
        _windows = windows;
        var pins = App.Current.Settings.Pinned;
        var running = _windows
            .Where(w => !pins.Any(p => WindowTracker.Matches(p, w)))
            .GroupBy(w => w.GroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        var key = string.Join("|", pins.Select(p => p.Target)) + "#" + string.Join("|", running.Select(r => r.GroupKey)) + "#" + Theme.IsDark;
        if (key != _layoutKey)
        {
            _layoutKey = key;
            _row.Children.Clear();
            _appButtons.Clear();
            Add(_menuButton);
            Add(Divider());
            foreach (var pin in pins) Add(AppCell(pin.Target, pin.Name, Icons.Parse(pin.Glyph), pin));
            foreach (var w in running) Add(AppCell(w.GroupKey, WindowTracker.AppName(w), IconKind.None, null, w));
            Add(Divider());
            Add(_quickButton);
            Add(_langButton);
            Add(_clockButton);
        }
        UpdateStates();
        UpdateClock();
    }

    // Між комірками space-4; розділювач має ще по 6 з кожного боку. Поля задаються щоразу заново, не накопичуються.
    private void Add(FrameworkElement e)
    {
        var isDivider = e is Border { Width: 1 };
        var afterDivider = _row.Children.Count > 0 && _row.Children[^1] is Border { Width: 1 };
        var left = _row.Children.Count == 0 ? 0 : 4 + (isDivider || afterDivider ? 6 : 0);
        e.Margin = new Thickness(left, 0, 0, 0);
        _row.Children.Add(e);
    }

    private ShellButton AppCell(string key, string name, IconKind glyph, PinnedApp? pin, AppWindow? window = null)
    {
        UIElement visual = glyph != IconKind.None
            ? new Ui.Icon { Kind = glyph }
            : new Image
            {
                Width = 24, Height = 24,
                Source = AppCatalog.Instance.Icon(pin?.Target ?? window?.Aumid ?? window?.ExePath ?? key, IconPixels),
            };
        if (visual is Image img) RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        var b = Cell(visual, name, () => ActivateApp(pin, key, window));
        var menu = new ContextMenu();
        var pinItem = new MenuItem { Header = pin is null ? "Закріпити на острові" : "Відкріпити від острова" };
        pinItem.Click += (_, _) => TogglePin(pin, window);
        menu.Items.Add(pinItem);
        var close = new MenuItem { Header = "Закрити вікна" };
        close.Click += (_, _) => { foreach (var w in WindowsFor(pin, key)) Win32.PostMessage(w.Handle, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero); };
        menu.Opened += (_, _) => close.Visibility = WindowsFor(pin, key).Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        menu.Items.Add(close);
        b.ContextMenu = menu;
        _appButtons[key] = b;
        return b;
    }


    private List<AppWindow> WindowsFor(PinnedApp? pin, string key) =>
        pin is not null ? _windows.Where(w => WindowTracker.Matches(pin, w)).ToList()
                        : _windows.Where(w => string.Equals(w.GroupKey, key, StringComparison.OrdinalIgnoreCase)).ToList();

    private void ActivateApp(PinnedApp? pin, string key, AppWindow? window)
    {
        _windows = WindowTracker.Snapshot();
        var target = pin?.Target ?? window?.Aumid ?? window?.ExePath ?? key;
        if (target == Defaults.SettingsTarget) { App.Current.ShowSettings(); return; }
        WindowTracker.Toggle(WindowsFor(pin, key), () => AppCatalog.Launch(target));
    }

    private static void TogglePin(PinnedApp? pin, AppWindow? window)
    {
        var s = App.Current.Settings;
        if (pin is not null) s.Pinned.Remove(pin);
        else if (window is not null)
            s.Pinned.Add(new PinnedApp
            {
                Name = WindowTracker.AppName(window),
                Target = window.Aumid ?? window.ExePath ?? string.Empty,
                Exe = window.Aumid is null ? window.ExeName : null,
            });
        App.Current.SaveSettings();
    }

    private void UpdateStates()
    {
        var pins = App.Current.Settings.Pinned;
        foreach (var (key, button) in _appButtons)
        {
            var pin = pins.FirstOrDefault(p => string.Equals(p.Target, key, StringComparison.OrdinalIgnoreCase));
            button.IsActive = pin?.Target == Defaults.SettingsTarget ? App.Current.SettingsOpen : WindowsFor(pin, key).Count > 0;
        }
    }

    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    // Повноекранна гра чи відео: острів ховається повністю й повертається, щойно вийти з повного екрана.
    private void UpdateFullscreen()
    {
        var fullscreen = Native.Screens.ForegroundIsFullscreen();
        var target = fullscreen ? Visibility.Hidden : Visibility.Visible;
        if (Visibility != target) Visibility = target;
    }

    private void Tick()
    {
        UpdateClock();
        var snapshot = WindowTracker.Snapshot();
        if (!snapshot.Select(w => w.Handle).ToHashSet().SetEquals(_windows.Select(w => w.Handle))) Rebuild(snapshot);
        else
        {
            _windows = snapshot;
            UpdateStates();
        }
    }

    private void UpdateClock()
    {
        UpdateFullscreen();
        var now = DateTime.Now;
        _time.Text = now.ToString("HH:mm", Uk);
        _date.Text = $"{Uk.DateTimeFormat.GetAbbreviatedDayName(now.DayOfWeek).ToLower(Uk)}, {now.ToString("d MMMM", Uk)}";
        _lang.Text = SystemState.InputLanguage();
    }

    // Острів по центру внизу, на space-16 від краю екрана.
    public void Place()
    {
        var content = new Size(ActualWidth - 2 * ShadowMargin, ActualHeight - 2 * ShadowMargin);
        var left = Math.Round((Screen.Width - content.Width) / 2);
        var bottom = Screen.Height - Gap;
        PlaceContent(left, bottom);
        Bounds = new Rect(left, bottom - content.Height, content.Width, content.Height);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        Backdrop.Rebuild();
        HwndSource.FromHwnd(Handle)?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Провідник перезапустився й знову показав свою панель – ховаємо її знову.
        if (msg == TaskbarTakeover.TaskbarCreatedMessage) Dispatcher.BeginInvoke(App.Current.ApplyTakeover);
        return IntPtr.Zero;
    }
}
