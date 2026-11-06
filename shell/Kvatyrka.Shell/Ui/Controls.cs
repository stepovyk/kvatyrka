using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Kvatyrka.Shell.Ui;

// Кнопка оболонки: комірка острова, кнопка вікна, плитка меню. Стан «відкрито» – лише підкладка fill-active.
public class ShellButton : Button
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ShellButton));
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ShellButton), new PropertyMetadata(new CornerRadius(14)));

    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
}

// Плитка швидких налаштувань: увімкнена – акцент, вимкнена – fill-tile.
public class QuickTile : ToggleButton
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(IconKind), typeof(QuickTile));
    public IconKind Kind { get => (IconKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
}

// Перемикач 44×24.
public class Switch : ToggleButton
{
}

// Сегмент перемикача теми тощо.
public class Segment : RadioButton
{
}
