using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kvatyrka.Core;
using TweakEffect = Kvatyrka.Core.Effect;
using Kvatyrka.Shell.Services;
using Kvatyrka.Shell.Ui;

namespace Kvatyrka.Shell.Windows;

// Вікно Кватирки: усі покращення Windows перемикачами, нав'язані програми, острів і сама Кватирка.
public sealed class SettingsWindow : Window
{
    private const double NavWidth = 216, FrameWidth = 960, TitleHeight = 56;
    public const string HomeSection = "home", AppsSection = "apps", IslandSection = "island";

    private readonly StackPanel _nav = new() { Margin = new Thickness(11, 0, 11, 11) };
    private readonly StackPanel _content = new() { Margin = new Thickness(8, 8, 8, 8) };
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _sectionTitle = new();
    private readonly Border _effectBar = new() { Padding = new Thickness(14, 10, 10, 10), BorderThickness = new Thickness(0, 1, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Grid _overlay = new() { Visibility = Visibility.Collapsed };
    private string _section = HomeSection;
    private TweakEffect? _pending;
    private bool _busy;
    private List<AppEntry>? _junk;
    private bool _junkLoading, _junkFailed;
    private string? _notice;

    private static TweakEngine E => Improvements.Engine;
    private static Catalog C => Improvements.Catalog;
    private static Settings S => App.Current.Settings;

    private static IEnumerable<(string Id, string Name, IconKind Icon)> Sections =>
        new[] { (HomeSection, "Головне", IconKind.Home) }
            .Concat(C.Categories.Select(c => (c.Id, c.Name, Enum.Parse<IconKind>(c.Icon))))
            .Concat(new[] { (AppsSection, "Програми", IconKind.Trash), (IslandSection, "Острів", IconKind.Island) });

    public SettingsWindow()
    {
        Title = "Кватирка";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        FontFamily = (FontFamily)Application.Current.Resources["font-sans"];
        SetResourceReference(ForegroundProperty, "text");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/Kvatyrka.ico"));

        // Вікно вміщається в робочу область навіть на маленьких екранах.
        var height = Math.Min(640, Math.Floor(SystemParameters.WorkArea.Height - 32));
        var frame = new Border { Width = FrameWidth, Height = height, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), Margin = new Thickness(ShellWindow.ShadowMargin) };
        frame.SetResourceReference(Border.BackgroundProperty, "window");
        frame.SetResourceReference(Border.BorderBrushProperty, "border");
        frame.SetResourceReference(EffectProperty, "shadow-window");

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TitleHeight) });
        root.RowDefinitions.Add(new RowDefinition());
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(NavWidth) });
        root.ColumnDefinitions.Add(new ColumnDefinition());

        var appTitle = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(21, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        appTitle.Children.Add(new Mark());
        appTitle.Children.Add(new TextBlock { Text = "Кватирка", Style = (Style)FindResource("title"), Margin = new Thickness(12, 0, 0, 0) });
        root.Children.Add(appTitle);

        var bar = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 7, 0) };
        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        controls.Children.Add(WindowButton(IconKind.Min, "Згорнути", () => WindowState = WindowState.Minimized));
        controls.Children.Add(WindowButton(IconKind.Close, "Закрити", Close));
        DockPanel.SetDock(controls, Dock.Right);
        bar.Children.Add(controls);
        _sectionTitle.Style = (Style)FindResource("title");
        _sectionTitle.Margin = new Thickness(16, 0, 0, 0);
        bar.Children.Add(_sectionTitle);
        Grid.SetColumn(bar, 1);
        root.Children.Add(bar);

        Grid.SetRow(_nav, 1);
        root.Children.Add(_nav);

        var panel = new Border { CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 7, 7), ClipToBounds = true };
        panel.SetResourceReference(Border.BackgroundProperty, "panel");
        var dock = new DockPanel();
        _effectBar.SetResourceReference(Border.BorderBrushProperty, "line");
        DockPanel.SetDock(_effectBar, Dock.Bottom);
        dock.Children.Add(_effectBar);
        _scroll = new ScrollViewer { Content = _content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        dock.Children.Add(_scroll);
        panel.Child = dock;
        Grid.SetRow(panel, 1);
        Grid.SetColumn(panel, 1);
        root.Children.Add(panel);

        // Підтвердження поверх усього вікна.
        Grid.SetRowSpan(_overlay, 2);
        Grid.SetColumnSpan(_overlay, 2);
        root.Children.Add(_overlay);

        frame.Child = root;
        Content = frame;

        root.MouseLeftButtonDown += (_, e) => { if (e.GetPosition(root).Y < TitleHeight && e.ClickCount == 1 && _overlay.Visibility != Visibility.Visible) DragMove(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            if (_overlay.Visibility == Visibility.Visible) HideConfirm(); else Close();
            e.Handled = true;
        };
        Theme.Changed += OnThemeChanged;
        Closed += (_, _) => Theme.Changed -= OnThemeChanged;
        Render();
    }

    // Розділ, який показати (для знімків і переходів).
    public string Section
    {
        get => _section;
        set { _section = value; _scroll.ScrollToTop(); Render(); }
    }

    private void OnThemeChanged() => Dispatcher.BeginInvoke(Render);

    private ShellButton WindowButton(IconKind kind, string name, Action click)
    {
        var b = new ShellButton { Width = 40, Height = 36, CornerRadius = new CornerRadius(10), Content = new Ui.Icon { Kind = kind }, ToolTip = name };
        b.SetResourceReference(ForegroundProperty, "text-secondary");
        AutomationProperties.SetName(b, name);
        b.Click += (_, _) => click();
        return b;
    }

    private void Render()
    {
        _nav.Children.Clear();
        foreach (var (id, name, icon) in Sections)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Ui.Icon { Kind = icon });
            row.Children.Add(new TextBlock { Text = name, Style = (Style)FindResource("body"), Margin = new Thickness(12, 0, 0, 0) });
            var item = new ShellButton { Content = row, Width = double.NaN, Height = 40, CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 0, 10, 0), HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 2) };
            AutomationProperties.SetName(item, name);
            if (id == _section) item.SetResourceReference(BackgroundProperty, "selected");
            item.Click += (_, _) => Section = id;
            _nav.Children.Add(item);
        }
        _sectionTitle.Text = Sections.First(s => s.Id == _section).Name;
        _content.Children.Clear();
        switch (_section)
        {
            case HomeSection: Home(); break;
            case AppsSection: Apps(); break;
            case IslandSection: Island(); break;
            default: foreach (var t in C.Tweaks.Where(t => t.Category == _section)) TweakRow(t); break;
        }
        // Останній рядок без лінії знизу.
        if (_content.Children.Count > 0 && _content.Children[^1] is Border last && last.Tag is "row") last.BorderThickness = new Thickness(0);
        RenderEffect();
    }

    // ---------- рядки ----------

    private void Row(string label, UIElement? control, string? hint = null)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("body"), TextWrapping = TextWrapping.Wrap, LineHeight = 20 });
        if (hint is not null) text.Children.Add(new TextBlock { Text = hint, Style = (Style)FindResource("note"), Margin = new Thickness(0, 4, 0, 0) });
        grid.Children.Add(text);
        if (control is FrameworkElement fe)
        {
            fe.VerticalAlignment = VerticalAlignment.Center;
            fe.Margin = new Thickness(24, 0, 0, 0);
            Grid.SetColumn(fe, 1);
            grid.Children.Add(fe);
        }
        var border = new Border { MinHeight = 64, Padding = new Thickness(8, 14, 8, 14), BorderThickness = new Thickness(0, 0, 0, 1), Child = grid, Tag = "row" };
        border.SetResourceReference(Border.BorderBrushProperty, "line");
        _content.Children.Add(border);
    }

    // Великий блок угорі розділу: заголовок, пояснення й дії.
    private void Hero(string heading, string text, params UIElement[] actions)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = heading, Style = (Style)FindResource("heading") });
        stack.Children.Add(new TextBlock { Text = text, Style = (Style)FindResource("note"), Margin = new Thickness(0, 8, 0, 0), MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left });
        if (actions.Length > 0)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
            for (var i = 0; i < actions.Length; i++)
            {
                if (actions[i] is FrameworkElement fe && i > 0) fe.Margin = new Thickness(8, 0, 0, 0);
                row.Children.Add(actions[i]);
            }
            stack.Children.Add(row);
        }
        var border = new Border { Padding = new Thickness(8, 16, 8, 20), BorderThickness = new Thickness(0, 0, 0, 1), Child = stack, Tag = "row" };
        border.SetResourceReference(Border.BorderBrushProperty, "line");
        _content.Children.Add(border);
    }

    private void TweakRow(Tweak t)
    {
        var conflict = SystemFacts.Conflict(t, Improvements.Registry);
        var hint = t.Description;
        UIElement control;
        if (t.IsChoice) control = ChoiceControl(t);
        else
        {
            var state = E.State(t);
            if (state == TweakState.Partial) hint += " Зараз діє частково: увімкніть, щоб довести до кінця.";
            var sw = new Switch { IsChecked = state == TweakState.On };
            AutomationProperties.SetName(sw, t.Name);
            sw.Click += async (_, _) => await Change(new[] { (t, sw.IsChecked == true ? "on" : "off") });
            control = sw;
        }
        if (conflict is not null) hint += " " + conflict;
        Row(t.Name, control, hint);
    }

    private UIElement ChoiceControl(Tweak t)
    {
        var current = E.Selected(t);
        var seg = new Border { Height = 36, CornerRadius = new CornerRadius(10), Padding = new Thickness(2) };
        seg.SetResourceReference(Border.BackgroundProperty, "fill-field");
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var o in t.Choice!.Options)
        {
            var b = new Segment { Content = o.Name, GroupName = t.Id, IsChecked = o.Id == current };
            AutomationProperties.SetName(b, $"{t.Name}: {o.Name}");
            b.Checked += async (_, _) => { if (o.Id != E.Selected(t)) await Change(new[] { (t, o.Id) }); };
            row.Children.Add(b);
        }
        seg.Child = row;
        return seg;
    }

    private async Task Change(IReadOnlyList<(Tweak Tweak, string Value)> changes)
    {
        if (_busy || changes.Count == 0) return;
        _busy = true;
        _content.IsEnabled = false;
        var before = changes.Select(c => E.Current(c.Tweak)).ToList();
        try
        {
            var outcome = await Improvements.ApplyAsync(changes);
            // Підказка про перезапуск – для кожного покращення, що справді змінилось, навіть якщо інші ні.
            for (var i = 0; i < changes.Count; i++)
                if (E.Current(changes[i].Tweak) != before[i]) Note(changes[i].Tweak.Effect);
            Report(outcome);
        }
        catch (Exception ex)
        {
            Log.Write($"Зміна налаштувань: {ex}");
            _notice = "Щось пішло не так, нічого не змінено. Подробиці – у журналі.";
        }
        finally
        {
            _busy = false;
            _content.IsEnabled = true;
            Render();
        }
    }

    // Людина завжди бачить, що не вдалося, а не лише перемикач, що відскочив.
    private void Report(Improvements.Outcome outcome)
    {
        if (outcome.Declined) _notice = "Без прав адміністратора нічого не змінено.";
        else if (outcome.Failed.Count > 0) _notice = $"Не вдалося змінити: {string.Join(", ", outcome.Failed)}. Подробиці – у журналі.";
        else _notice = null;
    }

    // ---------- коли зміни стануть видимі ----------

    private void Note(TweakEffect effect)
    {
        if (effect == TweakEffect.Now) return;
        if (_pending is null || effect > _pending) _pending = effect;
    }

    private void RenderEffect()
    {
        var stack = new StackPanel();
        if (_notice is not null)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new TextBlock { Text = _notice, Style = (Style)FindResource("body"), TextWrapping = TextWrapping.Wrap, LineHeight = 20 });
            var ok = TextButton("Зрозуміло", () => { _notice = null; RenderEffect(); }, plain: true);
            ok.Margin = new Thickness(16, 0, 0, 0);
            Grid.SetColumn(ok, 1);
            grid.Children.Add(ok);
            stack.Children.Add(grid);
        }
        if (_pending is { } p) stack.Children.Add(EffectRow(p, _notice is not null));
        _effectBar.Child = stack;
        _effectBar.Visibility = stack.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Grid EffectRow(TweakEffect p, bool spaced)
    {
        var (text, action, label) = p switch
        {
            TweakEffect.Explorer => ("Зміни з'являться після перезапуску Провідника.", (Action)RestartExplorer, "Перезапустити Провідник"),
            TweakEffect.Signin => ("Зміни з'являться після повторного входу в Windows.", () => Confirm("Вийти з акаунта?", "Збережіть відкриті документи: програми закриються.", "Вийти", SystemState.SignOut), "Вийти з акаунта"),
            _ => ("Зміни з'являться після перезавантаження ПК.", () => Confirm("Перезавантажити ПК?", "Збережіть відкриті документи: програми закриються.", "Перезавантажити", SystemState.Restart), "Перезавантажити"),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = text, Style = (Style)FindResource("body"), TextWrapping = TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 0, 0) };
        buttons.Children.Add(TextButton("Пізніше", () => { _pending = null; RenderEffect(); }, plain: true));
        var go = AccentButton(label, action);
        go.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(go);
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);
        if (spaced) grid.Margin = new Thickness(0, 10, 0, 0);
        return grid;
    }

    private async void RestartExplorer()
    {
        _effectBar.IsEnabled = false;
        var ok = await Task.Run(Improvements.RestartExplorer);
        _effectBar.IsEnabled = true;
        if (ok && _pending == TweakEffect.Explorer) _pending = null;
        if (!ok) _notice = "Не вдалося перезапустити Провідник. Вийдіть з акаунта й увійдіть знову.";
        RenderEffect();
    }

    // ---------- Головне ----------

    private void Home()
    {
        var recommended = C.Tweaks.Where(t => !t.IsWindowsDefault(t.Recommended) && SystemFacts.Conflict(t, Improvements.Registry) is null).ToList();
        var missing = recommended.Where(t => E.Current(t) != t.Recommended).ToList();
        var changed = C.Tweaks.Where(t => t.IsChoice ? E.Selected(t) != t.Choice!.Windows : E.State(t) != TweakState.Off).ToList();
        var done = recommended.Count - missing.Count;

        var apply = AccentButton(missing.Count == 0 ? "Усе рекомендоване увімкнено" : $"Увімкнути рекомендоване ({missing.Count})",
            async () => await Change(missing.Select(t => (t, t.Recommended)).ToList()));
        apply.IsEnabled = missing.Count > 0;
        var revert = TextButton("Повернути як у Windows", () => Confirm(
            "Повернути все як у Windows?",
            $"Кватирка вимкне всі покращення ({changed.Count}), і Windows стане таким, як після встановлення. Прибрані програми лишаться прибраними.",
            "Повернути",
            async () => await Change(changed.Select(t => (t, t.IsChoice ? t.Choice!.Windows : "off")).ToList())));
        revert.IsEnabled = changed.Count > 0;
        Hero("Windows без зайвого",
            $"Увімкнено {done} з {recommended.Count} рекомендованих покращень. Кожне вимикається окремим перемикачем, і тоді Windows знову такий, як був.",
            apply, revert);

        var guardian = new Switch { IsChecked = Improvements.GuardianInstalled };
        AutomationProperties.SetName(guardian, "Охоронець налаштувань");
        guardian.Click += async (_, _) =>
        {
            var on = guardian.IsChecked == true;
            _content.IsEnabled = false;
            try
            {
                var r = await Improvements.ElevateAsync(new ElevatedRequest { Guardian = on });
                Improvements.UpdateAutostart();
                _notice = r switch
                {
                    Improvements.Elevation.Declined => "Без прав адміністратора нічого не змінено.",
                    _ when Improvements.GuardianInstalled != on => $"Не вдалося {(on ? "увімкнути" : "вимкнути")} охоронця. Подробиці – у журналі.",
                    _ => null,
                };
            }
            finally
            {
                _content.IsEnabled = true;
                Render();
            }
        };
        Row("Охоронець налаштувань", guardian,
            "Після кожного оновлення Windows Кватирка повертає ваш вибір, якщо оновлення його скинуло. Що ви змінили самі в Параметрах, вона не чіпає. Для цього Кватирка встановлюється в Program Files.");

        var seg = new Border { Height = 36, CornerRadius = new CornerRadius(10), Padding = new Thickness(2) };
        seg.SetResourceReference(Border.BackgroundProperty, "fill-field");
        var segRow = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (title, mode) in new[] { ("Світла", Appearance.Light), ("Темна", Appearance.Dark), ("Як у Windows", Appearance.Auto) })
        {
            var b = new Segment { Content = title, GroupName = "theme", IsChecked = S.Theme == mode };
            b.Checked += (_, _) => { if (S.Theme != mode) { S.Theme = mode; App.Current.SaveSettings(); } };
            segRow.Children.Add(b);
        }
        seg.Child = segRow;
        Row("Тема Кватирки", seg);

        var swatches = new StackPanel { Orientation = Orientation.Horizontal };
        for (var i = 0; i < Settings.Accents.Length; i++)
        {
            var hex = Settings.Accents[i];
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var selected = string.Equals(S.Accent, hex, StringComparison.OrdinalIgnoreCase);
            var ring = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(selected ? 2 : 0), BorderBrush = new SolidColorBrush(color) };
            ring.Child = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = new SolidColorBrush(color) };
            var b = new ShellButton { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Content = ring, ToolTip = Settings.AccentNames[i], Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0) };
            AutomationProperties.SetName(b, Settings.AccentNames[i]);
            b.Click += (_, _) => { S.Accent = hex; App.Current.SaveSettings(); };
            swatches.Children.Add(b);
        }
        Row("Акцент", swatches);

        Row("Параметри Windows", TextButton("Відкрити", () => SystemState.OpenWindowsSettings()), "Усе інше – там, де звикли.");
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        Row($"Кватирка {version?.ToString(3)}", TextButton("Відкрити папку", () => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Settings.Folder}\"") { UseShellExecute = true })),
            "Журнал змін і файл налаштувань, якщо захочете щось поміняти вручну.");
    }

    // ---------- Програми ----------

    private async void LoadJunk()
    {
        if (_junkLoading) return;
        _junkLoading = true;
        _junkFailed = false;
        try { _junk = await Improvements.InstalledJunkAsync(); }
        catch (Exception ex) { Log.Write($"Список програм: {ex}"); _junk = null; }
        _junkFailed = _junk is null;
        _junkLoading = false;
        if (_section == AppsSection) Render();
    }

    private void Apps()
    {
        if (_junkFailed)
        {
            Hero("Нав'язані програми", "Не вдалося отримати список встановлених програм. Подробиці – у журналі.",
                TextButton("Спробувати ще", () => { _junkFailed = false; Render(); }));
            return;
        }
        if (_junk is null)
        {
            LoadJunk();
            Hero("Нав'язані програми", "Шукаю, що Windows встановила без вашого прохання…");
            return;
        }
        var recommended = _junk.Where(a => a.Default).ToList();
        if (_junk.Count == 0)
        {
            Hero("Нав'язаних програм немає", "Усе, що Windows ставить без прохання, вже прибрано. Потрібну програму завжди можна встановити з Microsoft Store.");
            return;
        }
        var all = AccentButton(recommended.Count == 0 ? "Рекомендовані вже прибрано" : $"Прибрати рекомендовані ({recommended.Count})", () => Confirm(
            $"Прибрати {recommended.Count} програм?",
            string.Join(", ", recommended.Select(a => a.Name)) + ". Повернути будь-яку можна з Microsoft Store.",
            "Прибрати", async () => await Remove(recommended)));
        all.IsEnabled = recommended.Count > 0;
        Hero("Нав'язані програми",
            $"Знайдено {_junk.Count}. Прибрані програми зникають для всіх користувачів і не з'являються в нових профілях. Повернути будь-яку можна з Microsoft Store.",
            all);
        foreach (var a in _junk.OrderByDescending(a => a.Default).ThenBy(a => a.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("uk-UA"), true)))
        {
            var hint = a.Default ? "Рекомендовано прибрати." : "Комусь потрібна, тож лишається, доки не приберете самі.";
            if (a.With is not null) hint += $" Разом увімкнеться «{C[a.With].Name}», щоб Windows не просила встановити її знову.";
            Row(a.Name, TextButton("Прибрати", async () => await Remove(new[] { a })), hint);
        }
    }

    private async Task Remove(IReadOnlyList<AppEntry> apps)
    {
        if (_busy) return;
        _busy = true;
        _content.IsEnabled = false;
        try { Report(await Improvements.RemoveAppsAsync(apps)); }
        catch (Exception ex)
        {
            Log.Write($"Прибирання програм: {ex}");
            _notice = "Щось пішло не так. Подробиці – у журналі.";
        }
        finally
        {
            _busy = false;
            _content.IsEnabled = true;
            _junk = null;
            Render();
        }
    }

    // ---------- Острів ----------

    private void Island()
    {
        var island = new Switch { IsChecked = S.Island };
        AutomationProperties.SetName(island, "Острів замість панелі задач");
        island.Click += (_, _) => { App.Current.SetIsland(island.IsChecked == true); Render(); };
        Row("Острів замість панелі задач", island,
            "Капсула Кватирки внизу по центру: програми, швидкі налаштування й годинник. Вимкніть – і повернеться звична панель Windows.");
        if (!S.Island) return;

        Row("Прозорість", Toggle(S.Transparency, v => { S.Transparency = v; App.Current.SaveSettings(); }), "Скло острова й панелей.");
        Row("Клавіша Win відкриває меню острова", Toggle(S.WinKeyOpensMenu, v => { S.WinKeyOpensMenu = v; App.Current.SaveSettings(); }));

        var pins = S.Pinned;
        for (var i = 0; i < pins.Count; i++)
        {
            var pin = pins[i];
            var index = i;
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(IconButton(IconKind.Left, "Лівіше", index > 0, () => Move(index, -1)));
            actions.Children.Add(IconButton(IconKind.Right, "Правіше", index < pins.Count - 1, () => Move(index, 1)));
            var remove = TextButton("Відкріпити", () => { pins.Remove(pin); App.Current.SaveSettings(); Render(); });
            remove.Margin = new Thickness(8, 0, 0, 0);
            actions.Children.Add(remove);

            var visual = Icons.Parse(pin.Glyph) is var glyph && glyph != IconKind.None
                ? (UIElement)new Ui.Icon { Kind = glyph }
                : new Image { Width = 24, Height = 24, Source = AppCatalog.Instance.Icon(pin.Target, ShellWindow.IconPixels) };
            var name = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(visual);
            name.Children.Add(new TextBlock { Text = pin.Name, Style = (Style)FindResource("body"), Margin = new Thickness(12, 0, 0, 0) });
            var dock = new DockPanel();
            DockPanel.SetDock(actions, Dock.Right);
            dock.Children.Add(actions);
            dock.Children.Add(name);
            var border = new Border { MinHeight = 64, Padding = new Thickness(8, 14, 8, 14), BorderThickness = new Thickness(0, 0, 0, 1), Child = dock, Tag = "row" };
            border.SetResourceReference(Border.BorderBrushProperty, "line");
            _content.Children.Add(border);
        }
        if (pins.Count == 0) Row("Нічого не закріплено", null);
        _content.Children.Add(new TextBlock
        {
            Text = "Закріпити програму: права кнопка на ній у меню або на острові.",
            Style = (Style)FindResource("note"), Margin = new Thickness(8, 16, 8, 8),
        });
    }

    private void Move(int index, int delta)
    {
        var pins = S.Pinned;
        var item = pins[index];
        pins.RemoveAt(index);
        pins.Insert(index + delta, item);
        App.Current.SaveSettings();
        Render();
    }

    // ---------- підтвердження ----------

    private void Confirm(string title, string text, string confirm, Action action)
    {
        _overlay.Children.Clear();
        var dim = new Border { CornerRadius = new CornerRadius(15), Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)) };
        dim.MouseLeftButtonDown += (_, _) => HideConfirm();
        _overlay.Children.Add(dim);
        var card = new Border { Width = 440, CornerRadius = new CornerRadius(14), Padding = new Thickness(24), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        card.SetResourceReference(Border.BackgroundProperty, "window");
        card.SetResourceReference(Border.BorderBrushProperty, "border");
        card.SetResourceReference(EffectProperty, "shadow-popover");
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("heading"), TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(new TextBlock { Text = text, Style = (Style)FindResource("note"), Margin = new Thickness(0, 8, 0, 0) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 24, 0, 0) };
        buttons.Children.Add(TextButton("Скасувати", HideConfirm));
        var ok = AccentButton(confirm, () => { HideConfirm(); action(); });
        ok.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(ok);
        stack.Children.Add(buttons);
        card.Child = stack;
        _overlay.Children.Add(card);
        _overlay.Visibility = Visibility.Visible;
        ok.Focus();
    }

    private void HideConfirm()
    {
        _overlay.Visibility = Visibility.Collapsed;
        _overlay.Children.Clear();
    }

    // ---------- елементи ----------

    private static Switch Toggle(bool value, Action<bool> set)
    {
        var sw = new Switch { IsChecked = value };
        sw.Click += (_, _) => set(sw.IsChecked == true);
        return sw;
    }

    private static ShellButton TextButton(string text, Action click, bool plain = false)
    {
        var label = new TextBlock { Text = text, Style = (Style)Application.Current.Resources["label"] };
        var b = new ShellButton { Content = label, Width = double.NaN, Height = 36, CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 0, 14, 0) };
        AutomationProperties.SetName(b, text);
        b.IsEnabledChanged += (_, _) => label.SetResourceReference(TextBlock.ForegroundProperty, b.IsEnabled ? "text" : "text-disabled");
        if (!plain) b.SetResourceReference(BackgroundProperty, "fill-field");
        b.Click += (_, _) => click();
        return b;
    }

    private static ShellButton AccentButton(string text, Action click)
    {
        var label = new TextBlock { Text = text, Style = (Style)Application.Current.Resources["label"] };
        label.SetResourceReference(TextBlock.ForegroundProperty, "on-accent");
        var b = new ShellButton { Content = label, Width = double.NaN, Height = 36, CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 0, 16, 0) };
        AutomationProperties.SetName(b, text);
        b.SetResourceReference(BackgroundProperty, "accent");
        b.IsEnabledChanged += (_, _) =>
        {
            if (b.IsEnabled) { b.SetResourceReference(BackgroundProperty, "accent"); label.SetResourceReference(TextBlock.ForegroundProperty, "on-accent"); }
            else { b.SetResourceReference(BackgroundProperty, "fill-field"); label.SetResourceReference(TextBlock.ForegroundProperty, "text-disabled"); }
        };
        b.Click += (_, _) => click();
        return b;
    }

    private static ShellButton IconButton(IconKind kind, string name, bool enabled, Action click)
    {
        var b = new ShellButton { Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Content = new Ui.Icon { Kind = kind }, ToolTip = name, IsEnabled = enabled };
        AutomationProperties.SetName(b, name);
        b.Click += (_, _) => click();
        return b;
    }
}
