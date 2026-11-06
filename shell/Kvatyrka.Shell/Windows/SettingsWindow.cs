using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kvatyrka.Shell.Services;
using Kvatyrka.Shell.Ui;

namespace Kvatyrka.Shell.Windows;

// Налаштування Кватирки: усе, що людина може захотіти змінити, без редагування файлів.
public sealed class SettingsWindow : Window
{
    private const double NavWidth = 215;
    private readonly StackPanel _nav = new() { Margin = new Thickness(11, 0, 11, 11) };
    private readonly StackPanel _content = new() { Margin = new Thickness(8) };
    private readonly TextBlock _sectionTitle = new();
    private string _section = "Персоналізація";

    private static readonly (string Name, IconKind Icon)[] Sections =
    {
        ("Персоналізація", IconKind.Palette), ("Острів", IconKind.Apps), ("Система", IconKind.Display),
    };

    public SettingsWindow()
    {
        Title = "Налаштування Кватирки";
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

        var frame = new Border { Width = 860, Height = 560, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), Margin = new Thickness(ShellWindow.ShadowMargin) };
        frame.SetResourceReference(Border.BackgroundProperty, "window");
        frame.SetResourceReference(Border.BorderBrushProperty, "border");
        frame.SetResourceReference(EffectProperty, "shadow-window");

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
        root.RowDefinitions.Add(new RowDefinition());
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(NavWidth) });
        root.ColumnDefinitions.Add(new ColumnDefinition());

        // Верхній рядок: назва програми над навігацією, назва розділу над вмістом, кнопки вікна праворуч.
        var appTitle = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(21, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        appTitle.Children.Add(new Ui.Icon { Kind = IconKind.Sliders });
        appTitle.Children.Add(new TextBlock { Text = "Налаштування", Style = (Style)FindResource("title"), Margin = new Thickness(12, 0, 0, 0) });
        root.Children.Add(appTitle);

        var bar = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 7, 0) };
        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        controls.Children.Add(WindowButton(IconKind.Min, "Згорнути", () => WindowState = WindowState.Minimized));
        controls.Children.Add(WindowButton(IconKind.Close, "Закрити", Close));
        DockPanel.SetDock(controls, Dock.Right);
        bar.Children.Add(controls);
        _sectionTitle.Style = (Style)FindResource("title");
        _sectionTitle.Margin = new Thickness(8 + 8, 0, 0, 0);
        bar.Children.Add(_sectionTitle);
        Grid.SetColumn(bar, 1);
        root.Children.Add(bar);

        Grid.SetRow(_nav, 1);
        root.Children.Add(_nav);

        var panel = new Border { CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 7, 7) };
        panel.SetResourceReference(Border.BackgroundProperty, "panel");
        panel.Child = new ScrollViewer { Content = _content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(panel, 1);
        Grid.SetColumn(panel, 1);
        root.Children.Add(panel);

        frame.Child = root;
        Content = frame;

        // Перетягування вікна за верхній рядок.
        root.MouseLeftButtonDown += (_, e) => { if (e.GetPosition(root).Y < 56 && e.ClickCount == 1) DragMove(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Theme.Changed += OnThemeChanged;
        Closed += (_, _) => Theme.Changed -= OnThemeChanged;
        Render();
    }

    private void OnThemeChanged() => Dispatcher.BeginInvoke(Render);

    private ShellButton WindowButton(IconKind kind, string name, Action click)
    {
        var b = new ShellButton { Width = 40, Height = 36, CornerRadius = new CornerRadius(10), Content = new Ui.Icon { Kind = kind }, ToolTip = name };
        b.SetResourceReference(ForegroundProperty, "text-secondary");
        System.Windows.Automation.AutomationProperties.SetName(b, name);
        b.Click += (_, _) => click();
        return b;
    }

    private void Render()
    {
        _nav.Children.Clear();
        foreach (var (name, icon) in Sections)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Ui.Icon { Kind = icon });
            row.Children.Add(new TextBlock { Text = name, Style = (Style)FindResource("body"), Margin = new Thickness(12, 0, 0, 0) });
            var item = new ShellButton { Content = row, Width = double.NaN, Height = 40, CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 0, 10, 0), HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 2) };
            if (name == _section) item.SetResourceReference(BackgroundProperty, "selected");
            item.Click += (_, _) => { _section = name; Render(); };
            _nav.Children.Add(item);
        }
        _sectionTitle.Text = _section;
        _content.Children.Clear();
        switch (_section)
        {
            case "Персоналізація": Personalization(); break;
            case "Острів": Island(); break;
            default: SystemSection(); break;
        }
        // Останній рядок без лінії знизу.
        if (_content.Children.Count > 0 && _content.Children[^1] is Border last && last.Tag is "row") last.BorderThickness = new Thickness(0);
    }

    private static Settings S => App.Current.Settings;

    private static void Save() => App.Current.SaveSettings();

    private void Row(string label, UIElement control, string? hint = null)
    {
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("body") });
        if (hint is not null) text.Children.Add(new TextBlock { Text = hint, Style = (Style)FindResource("caption"), Margin = new Thickness(0, 6, 0, 0) });
        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(control, Dock.Right);
        if (control is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Center;
        dock.Children.Add(control);
        dock.Children.Add(text);
        var border = new Border { MinHeight = 64, Padding = new Thickness(6, 12, 6, 12), BorderThickness = new Thickness(0, 0, 0, 1), Child = dock, Tag = "row" };
        border.SetResourceReference(Border.BorderBrushProperty, "line");
        _content.Children.Add(border);
    }

    private static Switch Toggle(bool value, Action<bool> set)
    {
        var sw = new Switch { IsChecked = value };
        sw.Click += (_, _) => set(sw.IsChecked == true);
        return sw;
    }

    private void Personalization()
    {
        var seg = new Border { Height = 36, CornerRadius = new CornerRadius(10), Padding = new Thickness(2) };
        seg.SetResourceReference(Border.BackgroundProperty, "fill-field");
        var segRow = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (title, mode) in new[] { ("Світла", Appearance.Light), ("Темна", Appearance.Dark), ("Автоматично", Appearance.Auto) })
        {
            var b = new Segment { Content = title, GroupName = "theme", IsChecked = S.Theme == mode };
            b.Checked += (_, _) => { if (S.Theme != mode) { S.Theme = mode; Save(); } };
            segRow.Children.Add(b);
        }
        seg.Child = segRow;
        Row("Тема", seg, "«Автоматично» – як у Windows");

        var swatches = new StackPanel { Orientation = Orientation.Horizontal };
        for (var i = 0; i < Settings.Accents.Length; i++)
        {
            var hex = Settings.Accents[i];
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var selected = string.Equals(S.Accent, hex, StringComparison.OrdinalIgnoreCase);
            var ring = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(selected ? 2 : 0), BorderBrush = new SolidColorBrush(color) };
            ring.Child = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = new SolidColorBrush(color) };
            var b = new ShellButton { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Content = ring, ToolTip = Settings.AccentNames[i], Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0) };
            System.Windows.Automation.AutomationProperties.SetName(b, Settings.AccentNames[i]);
            b.Click += (_, _) => { S.Accent = hex; Save(); };
            swatches.Children.Add(b);
        }
        Row("Акцент", swatches);

        Row("Прозорість", Toggle(S.Transparency, v => { S.Transparency = v; Save(); }), "Скло острова й панелей");
        Row("Шпалери", TextButton("Змінити", () => SystemState.OpenWindowsSettings("personalization-background")));
    }

    private void Island()
    {
        var pins = S.Pinned;
        for (var i = 0; i < pins.Count; i++)
        {
            var pin = pins[i];
            var index = i;
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(IconButton(IconKind.Left, "Лівіше", index > 0, () => Move(index, -1)));
            actions.Children.Add(IconButton(IconKind.Right, "Правіше", index < pins.Count - 1, () => Move(index, 1)));
            var remove = TextButton("Відкріпити", () => { pins.Remove(pin); Save(); });
            remove.Margin = new Thickness(8, 0, 0, 0);
            actions.Children.Add(remove);

            var visual = Icons.Parse(pin.Glyph) is var glyph && glyph != IconKind.None
                ? (UIElement)new Ui.Icon { Kind = glyph }
                : new Image { Width = 24, Height = 24, Source = AppCatalog.Instance.Icon(pin.Target, ShellWindow.IconPixels) };
            var name = new StackPanel { Orientation = Orientation.Horizontal };
            name.Children.Add(visual);
            name.Children.Add(new TextBlock { Text = pin.Name, Style = (Style)FindResource("body"), Margin = new Thickness(12, 0, 0, 0) });
            var dock = new DockPanel();
            DockPanel.SetDock(actions, Dock.Right);
            dock.Children.Add(actions);
            dock.Children.Add(name);
            var border = new Border { MinHeight = 64, Padding = new Thickness(6, 12, 6, 12), BorderThickness = new Thickness(0, 0, 0, 1), Child = dock, Tag = "row" };
            border.SetResourceReference(Border.BorderBrushProperty, "line");
            _content.Children.Add(border);
        }
        if (pins.Count == 0) Row("Нічого не закріплено", new Border());
        _content.Children.Add(new TextBlock
        {
            Text = "Закріпити програму: права кнопка на ній у меню або на острові.",
            Style = (Style)FindResource("caption"), Margin = new Thickness(6, 16, 6, 8),
        });
        Row("Клавіша Win відкриває меню", Toggle(S.WinKeyOpensMenu, v => { S.WinKeyOpensMenu = v; Save(); }), "Діє після перезапуску Кватирки");
    }

    private static void Move(int index, int delta)
    {
        var pins = S.Pinned;
        var item = pins[index];
        pins.RemoveAt(index);
        pins.Insert(index + delta, item);
        Save();
    }

    private void SystemSection()
    {
        Row("Замінити панель завдань Windows", Toggle(S.ReplaceTaskbar, v => { S.ReplaceTaskbar = v; Save(); }), "Вимкніть, щоб повернути звичну панель");
        Row("Запускати разом із Windows", Toggle(S.StartWithWindows, v => { S.StartWithWindows = v; Save(); }));
        Row("Усі параметри Windows", TextButton("Відкрити", () => SystemState.OpenWindowsSettings()));
        Row("Файл налаштувань", TextButton("Показати", () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{Settings.FilePath}\"") { UseShellExecute = true })), "Для тих, хто хоче змінити все вручну");
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        Row($"Кватирка {version?.ToString(3)}", TextButton("Вийти з Кватирки", () => App.Current.Quit()));
    }

    private static ShellButton TextButton(string text, Action click)
    {
        var b = new ShellButton
        {
            Content = new TextBlock { Text = text, Style = (Style)Application.Current.Resources["label"] },
            Width = double.NaN, Height = 36, CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 0, 14, 0),
        };
        b.SetResourceReference(BackgroundProperty, "fill-field");
        b.Click += (_, _) => click();
        return b;
    }

    private static ShellButton IconButton(IconKind kind, string name, bool enabled, Action click)
    {
        var b = new ShellButton { Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Content = new Ui.Icon { Kind = kind }, ToolTip = name, IsEnabled = enabled };
        System.Windows.Automation.AutomationProperties.SetName(b, name);
        b.Click += (_, _) => click();
        return b;
    }
}
