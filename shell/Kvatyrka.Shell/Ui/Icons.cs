using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Kvatyrka.Shell.Ui;

public static partial class Icons
{
    private static readonly Dictionary<IconKind, (Geometry? Stroke, Geometry? Fill)> Cache = new();

    public static (Geometry? Stroke, Geometry? Fill) Get(IconKind kind)
    {
        if (Cache.TryGetValue(kind, out var g)) return g;
        if (!Data.TryGetValue(kind, out var d)) return Cache[kind] = (null, null);
        static Geometry? Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var geo = Geometry.Parse(s);
            geo.Freeze();
            return geo;
        }
        return Cache[kind] = (Parse(d.Stroke), Parse(d.Fill));
    }

    public static IconKind Parse(string? name) =>
        name is not null && Enum.TryParse<IconKind>(name, true, out var k) ? k : IconKind.None;
}

// Іконка Кватирки: завжди 24×24, лінія 1,5, колір – як у тексту навколо.
public sealed class Icon : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(IconKind), typeof(Icon),
        new FrameworkPropertyMetadata(IconKind.None, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(Icon),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public IconKind Kind { get => (IconKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public Icon()
    {
        Width = 24;
        Height = 24;
        SnapsToDevicePixels = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var (stroke, fill) = Icons.Get(Kind);
        var pen = new Pen(Foreground, 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        if (stroke is not null) dc.DrawGeometry(null, pen, stroke);
        if (fill is not null) dc.DrawGeometry(Foreground, null, fill);
    }
}
