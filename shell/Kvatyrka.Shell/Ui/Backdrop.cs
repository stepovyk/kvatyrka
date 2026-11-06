using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Kvatyrka.Shell.Native;
using Kvatyrka.Shell.Services;

namespace Kvatyrka.Shell.Ui;

// Розмиті шпалери під склом острова й панелей. Острів і панелі завжди стоять над шпалерами,
// тож розмиту копію досить зробити раз – це не коштує жодного кадру під час роботи.
public static class Backdrop
{
    private const int Width = 960; // ширина розмитої копії: більше не видно після розмиття
    public static BitmapSource? Image { get; private set; }
    public static event Action? Changed;

    public static void Rebuild()
    {
        Image = null;
        try
        {
            var sb = new StringBuilder(1024);
            if (!Win32.SystemParametersInfo(Win32.SPI_GETDESKWALLPAPER, (uint)sb.Capacity, sb, 0) || sb.Length == 0 || !File.Exists(sb.ToString()))
            {
                Changed?.Invoke();
                return;
            }
            var screen = Windows.ShellWindow.Screen;
            var height = (int)Math.Round(Width * screen.Height / screen.Width);

            var src = new BitmapImage();
            src.BeginInit();
            src.UriSource = new Uri(sb.ToString());
            src.DecodePixelWidth = Width * 2;
            src.CacheOption = BitmapCacheOption.OnLoad;
            src.EndInit();
            src.Freeze();

            // «Заповнення»: шпалери покривають екран, зайве обрізається по центру.
            var scale = Math.Max(Width / (double)src.PixelWidth, height / (double)src.PixelHeight);
            var w = src.PixelWidth * scale;
            var h = src.PixelHeight * scale;
            // Радіус ядра WPF ≈ 3σ, а CSS blur(28px) – це σ = 28 пікселів екрана.
            var dv = new DrawingVisual { Effect = new BlurEffect { Radius = 3 * 28.0 * Width / screen.Width, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality } };
            using (var dc = dv.RenderOpen())
            {
                // Запас по краях, щоб розмиття не темніло біля меж.
                dc.DrawImage(src, new Rect((Width - w) / 2 - 24, (height - h) / 2 - 24, w + 48, h + 48));
            }
            var rtb = new RenderTargetBitmap(Width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            Image = Saturate(rtb, 1.4);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException or InvalidOperationException or ArgumentException)
        {
            Log.Write($"Шпалери не прочитались, скло буде суцільним: {ex.Message}");
        }
        Changed?.Invoke();
    }

    // saturate(140%) з дизайн-системи – та сама формула, що й у CSS.
    private static BitmapSource Saturate(BitmapSource src, double s)
    {
        var stride = src.PixelWidth * 4;
        var px = new byte[stride * src.PixelHeight];
        src.CopyPixels(px, stride, 0);
        double a = 0.213 + 0.787 * s, b = 0.715 - 0.715 * s, c = 0.072 - 0.072 * s;
        double d = 0.213 - 0.213 * s, e = 0.715 + 0.285 * s, f = 0.072 - 0.072 * s;
        double g = 0.213 - 0.213 * s, h = 0.715 - 0.715 * s, i = 0.072 + 0.928 * s;
        for (var p = 0; p < px.Length; p += 4)
        {
            double B = px[p], G = px[p + 1], R = px[p + 2];
            px[p + 2] = Clamp(a * R + b * G + c * B);
            px[p + 1] = Clamp(d * R + e * G + f * B);
            px[p] = Clamp(g * R + h * G + i * B);
        }
        var result = BitmapSource.Create(src.PixelWidth, src.PixelHeight, 96, 96, PixelFormats.Pbgra32, null, px, stride);
        result.Freeze();
        return result;
    }

    private static byte Clamp(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);

    // Шматок розмитих шпалер під прямокутником екрана (у DIP).
    public static Brush? For(Rect screenRect)
    {
        if (Image is null) return null;
        var k = Image.PixelWidth / Windows.ShellWindow.Screen.Width;
        var brush = new ImageBrush(Image)
        {
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(screenRect.X * k, screenRect.Y * k, screenRect.Width * k, screenRect.Height * k),
            Stretch = Stretch.Fill,
        };
        brush.Freeze();
        return brush;
    }
}
