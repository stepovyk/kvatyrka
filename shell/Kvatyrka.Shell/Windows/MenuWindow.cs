using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kvatyrka.Shell.Services;
using Kvatyrka.Shell.Ui;

namespace Kvatyrka.Shell.Windows;

// Меню Кватирки: пошук і всі програми; знизу – користувач і вимкнення.
public sealed class MenuWindow : PopupWindow
{
    private const double TileHeight = 92, TileGap = 4, Pad = 23, MinTile = 100;
    private const int VisibleRows = 4;

    private readonly TextBox _search = new() { Style = (Style)Application.Current.Resources["BareInput"] };
    private readonly TextBlock _hint = new() { Text = "Пошук програм", Style = (Style)Application.Current.Resources["secondary"], IsHitTestVisible = false };
    private readonly ListBox _list = new();
    private readonly TextBlock _empty = new() { Text = "Нічого не знайдено", Style = (Style)Application.Current.Resources["secondary"], HorizontalAlignment = HorizontalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly Grid _body = new();
    private double _width = 700;

    public MenuWindow()
    {
        Title = "Меню Кватирки";
        var stack = new StackPanel();

        // Велике поле пошуку 52 px на fill-tile.
        var field = new Border { Height = 52, CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 0, 14, 0) };
        field.SetResourceReference(Border.BackgroundProperty, "fill-tile");
        var fieldGrid = new Grid();
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition());
        var searchIcon = new Ui.Icon { Kind = IconKind.Search };
        searchIcon.SetResourceReference(Ui.Icon.ForegroundProperty, "text-secondary");
        var inputHost = new Grid { Margin = new Thickness(12, 0, 0, 0) };
        _search.FontSize = 15;
        _hint.FontSize = 15;
        _hint.LineHeight = 15;
        inputHost.Children.Add(_hint);
        inputHost.Children.Add(_search);
        Grid.SetColumn(inputHost, 1);
        fieldGrid.Children.Add(searchIcon);
        fieldGrid.Children.Add(inputHost);
        field.Child = fieldGrid;
        System.Windows.Automation.AutomationProperties.SetName(_search, "Пошук програм");
        _search.TextChanged += (_, _) => { _hint.Visibility = _search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; Filter(); };
        _search.PreviewKeyDown += OnSearchKey;

        // Сітка програм.
        _list.Style = null;
        _list.Background = Brushes.Transparent;
        _list.BorderThickness = new Thickness(0);
        _list.Padding = new Thickness(0);
        _list.FocusVisualStyle = null;
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(_list, ScrollBarVisibility.Auto);
        _list.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel)));
        _list.ItemContainerStyle = TileStyle();
        _list.ItemTemplate = TileTemplate();
        _list.MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Left && (e.OriginalSource as DependencyObject)?.FindParent<ListBoxItem>() is { DataContext: AppInfo a }) Launch(a); };
        _list.KeyDown += (_, e) => { if (e.Key == Key.Enter && _list.SelectedItem is AppInfo a) Launch(a); };
        _list.ContextMenuOpening += OnTileMenu;
        _list.ContextMenu = new ContextMenu();

        _body.Margin = new Thickness(0, 20, 0, 0);
        _body.Children.Add(_list);
        _body.Children.Add(_empty);

        var top = new StackPanel { Margin = new Thickness(Pad, Pad, Pad, 19) };
        top.Children.Add(field);
        top.Children.Add(_body);
        stack.Children.Add(top);

        var line = new Border { Height = 1 };
        line.SetResourceReference(Border.BackgroundProperty, "line");
        stack.Children.Add(line);
        stack.Children.Add(Footer());

        Content = Frame(stack, "popover", "shadow-popover", 22, new Thickness(0), out _);
    }

    private UIElement Footer()
    {
        var footer = new DockPanel { Margin = new Thickness(Pad, 11, Pad, 11), LastChildFill = false };
        var avatar = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16) };
        avatar.SetResourceReference(Border.BackgroundProperty, "accent");
        var initial = new TextBlock { Text = Environment.UserName.Length > 0 ? Environment.UserName[..1].ToUpperInvariant() : "?", Style = (Style)FindResource("title"), HorizontalAlignment = HorizontalAlignment.Center };
        initial.SetResourceReference(TextBlock.ForegroundProperty, "on-accent");
        avatar.Child = initial;
        var userRow = new StackPanel { Orientation = Orientation.Horizontal };
        userRow.Children.Add(avatar);
        userRow.Children.Add(new TextBlock { Text = Environment.UserName, Style = (Style)FindResource("label"), Margin = new Thickness(12, 0, 0, 0) });
        var user = new ShellButton { Content = userRow, Width = double.NaN, Padding = new Thickness(8, 0, 14, 0), HorizontalContentAlignment = HorizontalAlignment.Left, ToolTip = "Акаунт у параметрах Windows" };
        user.Click += (_, _) => { HidePopup(); SystemState.OpenWindowsSettings("accounts"); };
        DockPanel.SetDock(user, Dock.Left);
        footer.Children.Add(user);

        var power = new ShellButton { Content = new Ui.Icon { Kind = IconKind.Power }, ToolTip = "Вимкнення" };
        System.Windows.Automation.AutomationProperties.SetName(power, "Вимкнення");
        var menu = new ContextMenu { Placement = System.Windows.Controls.Primitives.PlacementMode.Top, PlacementTarget = power };
        foreach (var (title, action) in new (string, Action)[]
        {
            ("Заблокувати", SystemState.Lock), ("Сон", SystemState.Sleep), ("Вийти з акаунта", SystemState.SignOut),
            ("Перезавантажити", SystemState.Restart), ("Вимкнути", SystemState.Shutdown),
        })
        {
            var item = new MenuItem { Header = title };
            item.Click += (_, _) => { HidePopup(); action(); };
            menu.Items.Add(item);
        }
        power.Click += (_, _) => menu.IsOpen = true;
        DockPanel.SetDock(power, Dock.Right);
        footer.Children.Add(power);
        return footer;
    }

    private static Style TileStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(FocusVisualStyleProperty, null));
        var template = new ControlTemplate(typeof(ListBoxItem));
        var bg = new FrameworkElementFactory(typeof(Border), "Bg");
        bg.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
        bg.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        bg.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        template.VisualTree = bg;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("fill-field"), "Bg"));
        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("fill-active"), "Bg"));
        template.Triggers.Add(hover);
        template.Triggers.Add(selected);
        style.Setters.Add(new Setter(TemplateProperty, template));
        return style;
    }

    private DataTemplate TileTemplate()
    {
        var t = new DataTemplate(typeof(AppInfo));
        var root = new FrameworkElementFactory(typeof(TileView));
        root.SetBinding(TileView.AppProperty, new System.Windows.Data.Binding());
        t.VisualTree = root;
        return t;
    }

    protected override void OnOpening()
    {
        _search.Text = string.Empty;
        _width = Math.Max(IslandWindow.Bounds.Width, 640);
        var inner = _width - 2 * (Pad + 1);
        var cols = Math.Max(1, (int)((inner + TileGap) / (MinTile + TileGap)));
        TileView.TileWidth = Math.Floor((inner - TileGap * (cols - 1)) / cols);
        // Кожна плитка має поле 2 з боків, тож сітка рівно по центру з кроком space-4.
        _list.Width = (TileView.TileWidth + TileGap) * cols;
        _list.Margin = new Thickness(-TileGap / 2, 0, -TileGap / 2, 0);
        _list.HorizontalAlignment = HorizontalAlignment.Center;
        _body.Height = TileHeight * VisibleRows + TileGap * (VisibleRows - 1);
        ((FrameworkElement)Content).Width = _width;
        Filter();
    }

    protected override void OnOpened() => _search.Focus();

    protected override void Position() => PlaceContent(IslandWindow.Bounds.Left + (IslandWindow.Bounds.Width - _width) / 2, IslandWindow.Bounds.Top - Gap);

    private void Filter()
    {
        var q = _search.Text.Trim();
        var all = AppCatalog.Instance.All;
        var items = q.Length == 0 ? all : all.Where(a => a.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(a => a.Name.StartsWith(q, StringComparison.CurrentCultureIgnoreCase) ? 0 : 1).ToList();
        _list.ItemsSource = items;
        _empty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (items.Count > 0) _list.SelectedIndex = 0;
    }

    private void OnSearchKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _list.SelectedItem is AppInfo a) { Launch(a); e.Handled = true; }
        else if (e.Key is Key.Down or Key.Tab && _list.Items.Count > 0)
        {
            (_list.ItemContainerGenerator.ContainerFromIndex(Math.Max(0, _list.SelectedIndex)) as ListBoxItem)?.Focus();
            e.Handled = true;
        }
    }

    private void Launch(AppInfo app)
    {
        HidePopup();
        AppCatalog.Launch(app.Target);
    }

    private void OnTileMenu(object sender, ContextMenuEventArgs e)
    {
        if ((e.OriginalSource as DependencyObject)?.FindParent<ListBoxItem>() is not { DataContext: AppInfo app }) { e.Handled = true; return; }
        var s = App.Current.Settings;
        var pinned = s.Pinned.FirstOrDefault(p => string.Equals(p.Target, app.Target, StringComparison.OrdinalIgnoreCase));
        var item = new MenuItem { Header = pinned is null ? "Закріпити на острові" : "Відкріпити від острова" };
        item.Click += (_, _) =>
        {
            if (pinned is null) s.Pinned.Add(new PinnedApp { Name = app.Name, Target = app.Target, Exe = app.Exe });
            else s.Pinned.Remove(pinned);
            App.Current.SaveSettings();
        };
        _list.ContextMenu!.Items.Clear();
        _list.ContextMenu.Items.Add(item);
    }
}

// Плитка програми: іконка 24 у квадраті 48 на fill-tile, під нею назва.
public sealed class TileView : StackPanel
{
    public static double TileWidth { get; set; } = 105;

    public static readonly DependencyProperty AppProperty = DependencyProperty.Register(nameof(App), typeof(AppInfo), typeof(TileView),
        new PropertyMetadata(null, (d, e) => ((TileView)d).Build((AppInfo?)e.NewValue)));

    public AppInfo? App { get => (AppInfo?)GetValue(AppProperty); set => SetValue(AppProperty, value); }

    public TileView()
    {
        Width = TileWidth;
        Height = 92;
        Margin = new Thickness(2, 0, 2, 4);
        VerticalAlignment = VerticalAlignment.Center;
    }

    private void Build(AppInfo? app)
    {
        Children.Clear();
        Width = TileWidth;
        if (app is null) return;
        var square = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(14), Margin = new Thickness(0, 12, 0, 10), HorizontalAlignment = HorizontalAlignment.Center };
        square.SetResourceReference(Border.BackgroundProperty, "fill-tile");
        var glyph = AppCatalog.GlyphFor(app);
        if (glyph != IconKind.None) square.Child = new Ui.Icon { Kind = glyph, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        else
        {
            var image = new Image { Width = 24, Height = 24, Source = AppCatalog.Instance.Icon(app.Target, ShellWindow.IconPixels) };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            square.Child = image;
        }
        Children.Add(square);
        Children.Add(new TextBlock
        {
            Text = app.Name, Style = (Style)Application.Current.Resources["body"], HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = TileWidth - 12, ToolTip = app.Name,
        });
        System.Windows.Automation.AutomationProperties.SetName(this, app.Name);
    }
}

internal static class TreeExtensions
{
    public static T? FindParent<T>(this DependencyObject d) where T : DependencyObject
    {
        while (d is not null and not T) d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d);
        return d as T;
    }
}
