using System.Windows;
using System.Windows.Media;
using Kvatyrka.Shell.Services;

namespace Kvatyrka.Shell.Ui;

// Токени дизайн-системи Кватирки (design/design_system.py) як ресурси WPF. Назви ті самі, що й у tokens.json.
public static class Theme
{
    private static readonly Dictionary<string, (string Dark, string Light)> Colors = new()
    {
        ["text"] = ("#F4F6F8", "#16181C"),
        ["text-secondary"] = ("#ADF4F6F8", "#9E16181C"),
        ["text-disabled"] = ("#5CF4F6F8", "#5716181C"),
        ["glass"] = ("#C7121418", "#CCFAFAFC"),
        ["popover"] = ("#CC121418", "#D6FAFAFC"),
        ["border"] = ("#17FFFFFF", "#14000000"),
        ["divider"] = ("#1FFFFFFF", "#1F000000"),
        ["line"] = ("#14FFFFFF", "#14000000"),
        ["fill-active"] = ("#1AFFFFFF", "#12000000"),
        ["fill-tile"] = ("#14FFFFFF", "#0F000000"),
        ["fill-field"] = ("#0FFFFFFF", "#0B000000"),
        ["window"] = ("#16181C", "#FFFFFF"),
        ["panel"] = ("#1C1F24", "#F4F5F7"),
        ["selected"] = ("#295B9BFF", "#212F7BF5"),
        ["track"] = ("#29FFFFFF", "#24000000"),
        ["knob"] = ("#F4F6F8", "#FFFFFF"),
        ["on-accent"] = ("#0E1420", "#0E1420"),
        ["solid-glass"] = ("#1B1E23", "#F2F3F5"),
    };

    private static readonly Dictionary<string, (double Opacity, double Depth, double Blur, double DarkAlpha, double LightAlpha)> Shadows = new()
    {
        ["shadow-window"] = (1, 24, 64, 0.45, 0.18),
        ["shadow-island"] = (1, 16, 40, 0.30, 0.12),
        ["shadow-popover"] = (1, 24, 64, 0.40, 0.16),
    };

    public static bool IsDark { get; private set; } = true;
    public static event Action? Changed;

    public static void Apply(Settings s)
    {
        IsDark = s.Theme switch { Appearance.Dark => true, Appearance.Light => false, _ => SystemState.SystemDark };
        var res = Application.Current.Resources;
        foreach (var (name, (dark, light)) in Colors)
        {
            var c = (Color)ColorConverter.ConvertFromString(IsDark ? dark : light);
            res[name] = Frozen(new SolidColorBrush(c));
            res[name + "-color"] = c;
        }
        var accent = (Color)ColorConverter.ConvertFromString(s.Accent);
        res["accent"] = Frozen(new SolidColorBrush(accent));
        res["accent-color"] = accent;
        foreach (var (name, v) in Shadows)
        {
            var effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors_Black, Direction = 270, ShadowDepth = v.Depth, BlurRadius = v.Blur,
                Opacity = IsDark ? v.DarkAlpha : v.LightAlpha, RenderingBias = System.Windows.Media.Effects.RenderingBias.Quality,
            };
            effect.Freeze();
            res[name] = effect;
        }
        res["transparency"] = s.Transparency;
        Changed?.Invoke();
    }

    private static readonly Color Colors_Black = Color.FromRgb(0, 0, 0);

    private static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }

    public static Brush Brush(string name) => (Brush)Application.Current.Resources[name];
}
