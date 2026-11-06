using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Kvatyrka.Shell.Windows;

namespace Kvatyrka.Shell;

// «Kvatyrka.exe --snapshot <папка>»: показує острів і кожну панель та зберігає їх у PNG для перевірки вигляду.
public static class Snapshot
{
    public static void Run(string dir)
    {
        Directory.CreateDirectory(dir);
        var island = new IslandWindow();
        island.Show();
        Settle();
        Save(island, Path.Combine(dir, "island.png"));

        foreach (var (name, popup) in new[] { ("menu", island.Menu), ("quick", island.Quick), ("clock", island.Clock) })
        {
            popup.Toggle();
            Settle();
            Save(popup, Path.Combine(dir, $"{name}.png"));
            popup.HidePopup();
            Settle();
        }

        foreach (var theme in new[] { Services.Appearance.Dark, Services.Appearance.Light })
        {
            App.Current.Settings.Theme = theme;
            Ui.Theme.Apply(App.Current.Settings);
            var settings = new SettingsWindow();
            settings.Show();
            Settle();
            Save(settings, Path.Combine(dir, $"settings-{theme.ToString().ToLowerInvariant()}.png"));
            settings.Close();
            island.Rebuild();
            Settle();
            Save(island, Path.Combine(dir, $"island-{theme.ToString().ToLowerInvariant()}.png"));
        }
        island.Close();
    }

    private static void Settle()
    {
        for (var i = 0; i < 3; i++) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Thread.Sleep(150);
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void Save(Window w, string path)
    {
        w.UpdateLayout();
        const double scale = 2;
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth * scale), (int)Math.Ceiling(w.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        rtb.Render(w);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        enc.Save(fs);
    }
}
