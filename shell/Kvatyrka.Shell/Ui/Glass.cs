using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kvatyrka.Shell.Ui;

// Скло: розмиті шпалери під елементом + напівпрозорий колір із токена + рамка border.
// Коли прозорість вимкнена, лишається суцільний колір solid-glass.
public sealed class Glass : Border
{
    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(nameof(Tint), typeof(string), typeof(Glass),
        new PropertyMetadata("glass", (d, _) => ((Glass)d).Refresh()));

    // Назва колірного токена: «glass» для острова, «popover» для панелей.
    public string Tint { get => (string)GetValue(TintProperty); set => SetValue(TintProperty, value); }

    private Window? _window;
    private Rect _last = Rect.Empty;

    public Glass()
    {
        SetResourceReference(BorderBrushProperty, "border");
        BorderThickness = new Thickness(1);
        SnapsToDevicePixels = true;
        Loaded += (_, _) =>
        {
            _window = Window.GetWindow(this);
            if (_window is not null) _window.LocationChanged += (_, _) => Refresh();
            Backdrop.Changed += Refresh;
            Theme.Changed += ForceRefresh;
            Refresh();
        };
        Unloaded += (_, _) => { Backdrop.Changed -= Refresh; Theme.Changed -= ForceRefresh; };
        SizeChanged += (_, _) => Refresh();
    }

    private void ForceRefresh() { _last = Rect.Empty; Refresh(); }

    private void Refresh()
    {
        if (!IsLoaded || ActualWidth <= 0 || PresentationSource.FromVisual(this) is not { CompositionTarget: { } ct }) return;
        var topLeft = ct.TransformFromDevice.Transform(PointToScreen(new Point(0, 0)));
        var rect = new Rect(topLeft, new Size(ActualWidth, ActualHeight));
        if (rect == _last) return;
        _last = rect;

        var res = Application.Current.Resources;
        var transparent = res["transparency"] is true;
        var tint = (Brush)res[transparent ? Tint : "solid-glass"];
        if (!transparent || Backdrop.For(rect) is not { } image)
        {
            Background = transparent ? (Brush)res["solid-glass"] : tint;
            return;
        }
        var area = new Rect(0, 0, rect.Width, rect.Height);
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(image, null, new RectangleGeometry(area)));
        group.Children.Add(new GeometryDrawing(tint, null, new RectangleGeometry(area)));
        var brush = new DrawingBrush(group) { Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = area };
        brush.Freeze();
        Background = brush;
    }
}

// Знак Кватирки: рамка й хрестовина кольором тексту, відчинена шибка – акцент.
public sealed class Mark : FrameworkElement
{
    private static readonly Geometry Frame = Freeze(Geometry.Parse("M7.5,3.5 H16.5 A4,4 0 0 1 20.5,7.5 V16.5 A4,4 0 0 1 16.5,20.5 H7.5 A4,4 0 0 1 3.5,16.5 V7.5 A4,4 0 0 1 7.5,3.5 Z M12,3.5 V20.5 M3.5,12 H20.5"));
    private static readonly Geometry Pane = Freeze(Geometry.Parse("M13.5,5 H16.5 A2.5,2.5 0 0 1 19,7.5 V10.5 H13.5 Z"));

    public Mark() { Width = 24; Height = 24; SnapsToDevicePixels = true; Theme.Changed += InvalidateVisual; }

    private static Geometry Freeze(Geometry g) { g.Freeze(); return g; }

    protected override void OnRender(DrawingContext dc)
    {
        var res = Application.Current.Resources;
        dc.DrawGeometry(null, new Pen((Brush)res["text"], 1.5) { LineJoin = PenLineJoin.Round }, Frame);
        dc.DrawGeometry((Brush)res["accent"], null, Pane);
    }
}
