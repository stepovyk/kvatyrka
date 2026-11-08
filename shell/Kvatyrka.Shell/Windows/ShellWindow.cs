using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Kvatyrka.Shell.Native;
using Kvatyrka.Shell.Ui;

namespace Kvatyrka.Shell.Windows;

// Основа всіх вікон оболонки: без рамки, прозоре довкола скла (там тінь), поверх усього, без кнопки на панелі завдань.
public abstract class ShellWindow : Window
{
    // Поле під тінь довкола вмісту. Прозорі пікселі пропускають кліки далі.
    public const double ShadowMargin = 64;
    public const double Gap = 16;           // space-16: від краю екрана й між островом і панелями
    public const double IslandHeight = 64;  // size-island

    protected ShellWindow(bool activatable)
    {
        Activatable = activatable;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        ShowActivated = activatable;
        FontFamily = (FontFamily)Application.Current.Resources["font-sans"];
        SetResourceReference(ForegroundProperty, "text");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
    }

    protected bool Activatable { get; }

    // Масштаб екрана (1 = 96 DPI) і розмір іконки 24 у фізичних пікселях.
    public static double Scale { get; set; } = 1;
    public static int IconPixels => (int)Math.Ceiling(24 * Scale);
    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Win32.AddExStyle(Handle, Win32.WS_EX_TOOLWINDOW | (Activatable ? 0 : Win32.WS_EX_NOACTIVATE));
    }

    // Обгортка вмісту склом із тінню та полем під неї.
    protected static Grid Frame(UIElement content, string tint, string shadow, double radius, Thickness padding, out Glass glass)
    {
        glass = new Glass { Tint = tint, CornerRadius = new CornerRadius(radius), Padding = padding, Child = content };
        glass.SetResourceReference(EffectProperty, shadow);
        var root = new Grid { Margin = new Thickness(ShadowMargin) };
        root.Children.Add(glass);
        return root;
    }

    // Основний монітор у DIP за поточним масштабом острова (а не за масштабом, з яким запущено процес).
    public static Size Screen
    {
        get
        {
            var (w, h) = Native.Screens.PrimaryPixels();
            return new Size(w / Scale, h / Scale);
        }
    }

    // Поставити вміст (без поля тіні) так, щоб його лівий нижній кут був у заданій точці екрана.
    protected void PlaceContent(double left, double bottom)
    {
        UpdateLayout();
        Left = Math.Round(left - ShadowMargin);
        Top = Math.Round(bottom - (ActualHeight - ShadowMargin));
    }
}

// Спливна панель над островом: закривається, щойно фокус пішов деінде або натиснуто Esc.
public abstract class PopupWindow : ShellWindow
{
    private DateTime _closedAt = DateTime.MinValue;

    protected PopupWindow() : base(true)
    {
        Deactivated += (_, _) => HidePopup();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { HidePopup(); e.Handled = true; } };
    }

    public bool IsOpen => IsVisible;

    // Alt+F4 лише ховає панель: закрите вікно WPF не можна показати знову.
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!App.Current.ShuttingDown && !Closable)
        {
            e.Cancel = true;
            HidePopup();
        }
        base.OnClosing(e);
    }
    public event Action? OpenChanged;

    // Острів вимикають: панелі закриваються по-справжньому, а не ховаються.
    internal bool Closable { get; set; }

    public void Toggle()
    {
        if (IsVisible) { HidePopup(); return; }
        // Клік по кнопці острова спершу знімає фокус із панелі й закриває її – не відкриваємо знову тим самим кліком.
        if ((DateTime.UtcNow - _closedAt).TotalMilliseconds < 250) return;
        OnOpening();
        // Показуємо прозорою, ставимо на місце й лише тоді проявляємо – без стрибка з кута екрана.
        Opacity = 0;
        Show();
        Position();
        Opacity = 1;
        Activate();
        OnOpened();
        OpenChanged?.Invoke();
    }

    public void HidePopup()
    {
        if (!IsVisible) return;
        _closedAt = DateTime.UtcNow;
        Hide();
        OpenChanged?.Invoke();
    }

    protected abstract void Position();
    protected virtual void OnOpening() { }
    protected virtual void OnOpened() { }
}
