using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Kvatyrka.Shell.Windows;

namespace Kvatyrka.Shell;

// «Kvatyrka.exe --snapshot <папка>»: показує вікно Кватирки, острів і кожну панель та зберігає їх у PNG для перевірки вигляду.
// Нічого в системі не змінює.
public static class Snapshot
{
    public static void Run(string dir)
    {
        Directory.CreateDirectory(dir);
        // Знімки не мають висіти вічно: якщо щось зациклилось, видно, на якому кроці.
        var watchdog = new Timer(_ =>
        {
            Services.Log.Write($"Знімки зависли на кроці: {_step}");
            try { File.WriteAllText(Path.Combine(dir, "hang.txt"), _step); } catch (IOException) { }
            Environment.Exit(3);
        }, null, TimeSpan.FromMinutes(2), Timeout.InfiniteTimeSpan);
        foreach (var theme in new[] { Services.Appearance.Dark, Services.Appearance.Light })
        {
            App.Current.Settings.Theme = theme;
            Ui.Theme.Apply(App.Current.Settings);
            var t = theme.ToString().ToLowerInvariant();
            var settings = new SettingsWindow();
            settings.Show();
            var sections = theme == Services.Appearance.Dark
                ? new[] { SettingsWindow.HomeSection, "start", "privacy", "system", SettingsWindow.AppsSection, SettingsWindow.IslandSection }
                : new[] { SettingsWindow.HomeSection, "start" };
            foreach (var s in sections)
            {
                Step($"вікно {s} {t}");
                settings.Section = s;
                Settle();
                // Список програм читається у фоні – чекаємо, доки з'явиться.
                if (s == SettingsWindow.AppsSection) { Thread.Sleep(1500); Settle(); }
                Save(settings, Path.Combine(dir, $"window-{s}-{t}.png"));
            }
            settings.Close();
        }

        App.Current.Settings.Theme = Services.Appearance.Dark;
        Ui.Theme.Apply(App.Current.Settings);
        Step("острів: створення");
        var island = new IslandWindow();
        island.Show();
        Step("острів: показ");
        Settle();
        Save(island, Path.Combine(dir, "island.png"));
        foreach (var (name, popup) in new[] { ("menu", island.Menu), ("quick", island.Quick), ("clock", island.Clock) })
        {
            Step($"панель {name}");
            popup.Toggle();
            Settle();
            Save(popup, Path.Combine(dir, $"{name}.png"));
            popup.HidePopup();
            Settle();
        }
        Step("острів: закриття");
        island.Retire();
        watchdog.Dispose();
    }

    private static string _step = "початок";

    private static void Step(string name)
    {
        _step = name;
        Services.Log.Write($"Знімок: {name}");
    }

    // Дочекатися, доки вікно домалюється. Якщо програма не затихає (напр. хтось безперервно міняє вікна), – не довше 3 с.
    private static void Settle()
    {
        var limit = TimeSpan.FromSeconds(3);
        for (var i = 0; i < 3; i++) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle, CancellationToken.None, limit);
        Thread.Sleep(150);
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle, CancellationToken.None, limit);
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
