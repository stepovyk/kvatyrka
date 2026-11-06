using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Kvatyrka.Shell.Native;
using Kvatyrka.Shell.Ui;

namespace Kvatyrka.Shell.Windows;

// Сповіщення й календар – відкривається годинником.
public sealed class ClockWindow : PopupWindow
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");
    private readonly TextBlock _month = new();
    private readonly UniformGrid _days = new() { Columns = 7 };
    private DateTime _shown;

    public ClockWindow()
    {
        Title = "Календар";
        _month.Style = (Style)FindResource("title");

        // Сповіщення Windows живуть у центрі сповіщень – кнопка відкриває саме його.
        var notifications = new DockPanel { Margin = new Thickness(23, 15, 23, 15), LastChildFill = false };
        var title = new TextBlock { Text = "Сповіщення", Style = (Style)FindResource("title") };
        DockPanel.SetDock(title, Dock.Left);
        notifications.Children.Add(title);
        var open = new ShellButton
        {
            Content = new TextBlock { Text = "Відкрити", Style = (Style)FindResource("label") },
            Width = double.NaN, Height = 36, CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 0, 14, 0),
        };
        open.SetResourceReference(BackgroundProperty, "fill-field");
        open.Click += (_, _) => { HidePopup(); OpenNotificationCenter(); };
        DockPanel.SetDock(open, Dock.Right);
        notifications.Children.Add(open);

        var line = new Border { Height = 1 };
        line.SetResourceReference(Border.BackgroundProperty, "line");

        var header = new DockPanel { Height = 36, LastChildFill = false };
        DockPanel.SetDock(_month, Dock.Left);
        header.Children.Add(_month);
        var next = Nav(IconKind.Right, "Наступний місяць", 1);
        var prev = Nav(IconKind.Left, "Попередній місяць", -1);
        DockPanel.SetDock(next, Dock.Right);
        DockPanel.SetDock(prev, Dock.Right);
        next.Margin = new Thickness(4, 0, 0, 0);
        header.Children.Add(next);
        header.Children.Add(prev);

        var week = new UniformGrid { Columns = 7, Margin = new Thickness(-2, 8, -2, 0) };
        foreach (var d in new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд" })
            week.Children.Add(new TextBlock { Text = d, Style = (Style)FindResource("caption"), Height = 32, Padding = new Thickness(0, 10, 0, 0), TextAlignment = TextAlignment.Center });
        _days.Margin = new Thickness(-2, 0, -2, 0);

        var calendar = new StackPanel { Margin = new Thickness(23, 15, 23, 23) };
        calendar.Children.Add(header);
        calendar.Children.Add(week);
        calendar.Children.Add(_days);

        var stack = new StackPanel();
        stack.Children.Add(notifications);
        stack.Children.Add(line);
        stack.Children.Add(calendar);
        Content = Frame(stack, "popover", "shadow-popover", 22, new Thickness(0), out var glass);
        glass.Width = QuickWindow.PanelWidth;
    }

    private ShellButton Nav(IconKind kind, string name, int delta)
    {
        var b = new ShellButton { Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Content = new Ui.Icon { Kind = kind }, ToolTip = name };
        System.Windows.Automation.AutomationProperties.SetName(b, name);
        b.Click += (_, _) => { _shown = _shown.AddMonths(delta); Render(); };
        return b;
    }

    // Сітка місяця з понеділка; дні інших місяців не показуємо, сьогодні – акцентом.
    private void Render()
    {
        var month = Uk.DateTimeFormat.MonthNames[_shown.Month - 1];
        _month.Text = $"{char.ToUpper(month[0], Uk)}{month[1..]} {_shown.Year}";
        _days.Children.Clear();
        var first = new DateTime(_shown.Year, _shown.Month, 1);
        var offset = ((int)first.DayOfWeek + 6) % 7;
        for (var i = 0; i < offset; i++) _days.Children.Add(new Border());
        var today = DateTime.Today;
        for (var d = 1; d <= DateTime.DaysInMonth(_shown.Year, _shown.Month); d++)
        {
            var isToday = today.Year == _shown.Year && today.Month == _shown.Month && today.Day == d;
            var cell = new Border { Height = 40, Margin = new Thickness(2, 0, 2, 0), CornerRadius = new CornerRadius(12) };
            var text = new TextBlock { Text = d.ToString(Uk), Style = (Style)FindResource(isToday ? "title" : "body"), HorizontalAlignment = HorizontalAlignment.Center };
            if (isToday)
            {
                cell.SetResourceReference(Border.BackgroundProperty, "accent");
                text.SetResourceReference(TextBlock.ForegroundProperty, "on-accent");
            }
            cell.Child = text;
            _days.Children.Add(cell);
        }
        while (_days.Children.Count % 7 != 0) _days.Children.Add(new Border());
    }

    private static void OpenNotificationCenter()
    {
        // Win+N – той самий центр сповіщень Windows.
        var inputs = new[]
        {
            Key(Win32.VK_LWIN, false), Key(0x4E, false), Key(0x4E, true), Key(Win32.VK_LWIN, true),
        };
        Win32.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Win32.INPUT>());
    }

    private static Win32.INPUT Key(int vk, bool up) => new()
    {
        type = Win32.INPUT_KEYBOARD,
        u = new Win32.InputUnion { ki = new Win32.KEYBDINPUT { wVk = (ushort)vk, dwFlags = up ? Win32.KEYEVENTF_KEYUP : 0 } },
    };

    protected override void OnOpening()
    {
        _shown = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        Render();
    }

    protected override void Position() => PlaceContent(IslandWindow.Bounds.Right - QuickWindow.PanelWidth, IslandWindow.Bounds.Top - Gap);
}
