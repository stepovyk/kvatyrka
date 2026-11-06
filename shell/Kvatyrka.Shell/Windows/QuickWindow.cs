using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Kvatyrka.Shell.Native;
using Kvatyrka.Shell.Services;
using Kvatyrka.Shell.Ui;

namespace Kvatyrka.Shell.Windows;

// Швидкі налаштування: чотири справжні перемикачі й гучність.
public sealed class QuickWindow : PopupWindow
{
    public const double PanelWidth = 408; // size-side-panel

    private readonly QuickTile _network = new() { Content = "Мережа", Kind = IconKind.Wifi };
    private readonly QuickTile _game = new() { Content = "Ігровий режим", Kind = IconKind.Gamepad };
    private readonly QuickTile _dnd = new() { Content = "Не турбувати", Kind = IconKind.Moon };
    private readonly QuickTile _dark = new() { Content = "Темна тема", Kind = IconKind.Contrast };
    private readonly Slider _volume = new();
    private readonly ShellButton _mute = new() { Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Content = new Ui.Icon { Kind = IconKind.Volume } };
    private bool _loading;

    public QuickWindow()
    {
        Title = "Швидкі налаштування";
        var grid = new UniformGrid { Columns = 2, Rows = 2 };
        foreach (var t in new[] { _network, _game, _dnd, _dark }) grid.Children.Add(t);
        // Крок space-8 між плитками: праве й нижнє поле в лівих і верхніх.
        _network.Margin = new Thickness(0, 0, 4, 4);
        _game.Margin = new Thickness(4, 0, 0, 4);
        _dnd.Margin = new Thickness(0, 4, 4, 0);
        _dark.Margin = new Thickness(4, 4, 0, 0);

        _network.Click += (_, _) => { _network.IsChecked = SystemState.NetworkUp; HidePopup(); SystemState.OpenWindowsSettings("network-status"); };
        _game.Click += (_, _) => { if (!_loading) SystemState.GameMode = _game.IsChecked == true; };
        _dnd.Click += (_, _) => { if (!_loading) SystemState.DoNotDisturb = _dnd.IsChecked == true; };
        _dark.Click += (_, _) =>
        {
            if (_loading) return;
            SystemState.SystemDark = _dark.IsChecked == true;
            if (App.Current.Settings.Theme == Appearance.Auto) Theme.Apply(App.Current.Settings);
        };

        _volume.Style = (Style)Application.Current.Resources["VolumeSlider"];
        _volume.SmallChange = 0.02;
        _volume.LargeChange = 0.1;
        _volume.ValueChanged += (_, e) => { if (!_loading) Audio.Volume = e.NewValue; };
        System.Windows.Automation.AutomationProperties.SetName(_volume, "Гучність");
        _mute.Click += (_, _) => { Audio.Muted = !Audio.Muted; Load(); };
        System.Windows.Automation.AutomationProperties.SetName(_mute, "Вимкнути звук");

        var volumeRow = new DockPanel { Height = 44, Margin = new Thickness(10, 16, 16, 0) };
        DockPanel.SetDock(_mute, Dock.Left);
        volumeRow.Children.Add(_mute);
        _volume.Margin = new Thickness(6, 0, 0, 0);
        _volume.VerticalAlignment = VerticalAlignment.Center;
        volumeRow.Children.Add(_volume);

        var stack = new StackPanel();
        stack.Children.Add(grid);
        stack.Children.Add(volumeRow);
        Content = Frame(stack, "popover", "shadow-popover", 22, new Thickness(23), out var glass);
        glass.Width = PanelWidth;
    }

    private void Load()
    {
        _loading = true;
        _network.IsChecked = SystemState.NetworkUp;
        _network.ToolTip = SystemState.NetworkName;
        _game.IsChecked = SystemState.GameMode;
        _dnd.IsChecked = SystemState.DoNotDisturb;
        _dark.IsChecked = SystemState.SystemDark;
        var v = Audio.Volume;
        _volume.IsEnabled = v is not null;
        _volume.Value = Audio.Muted ? 0 : v ?? 0;
        _mute.Opacity = Audio.Muted ? 0.5 : 1;
        _loading = false;
    }

    protected override void OnOpening() => Load();

    protected override void Position() => PlaceContent(IslandWindow.Bounds.Right - PanelWidth, IslandWindow.Bounds.Top - Gap);
}
