using System.IO;
using System.Text;
using Kvatyrka.Core;
using Microsoft.Win32;

namespace Kvatyrka.Shell.Services;

// «Kvatyrka.exe --image <профіль.json> <папка>»: збирач уже відкрив реєстр образу як HKLM\KV_*,
// а Кватирка застосовує до нього вибір із профілю тим самим рушієм, що й на живій системі.
// У папку пише report.txt (що зроблено) і firstrun.txt (команди для першого запуску).
public static class ImageMode
{
    public static int Run(string[] args)
    {
        var rest = args.SkipWhile(a => !a.Equals("--image", StringComparison.OrdinalIgnoreCase)).Skip(1).ToArray();
        if (rest.Length != 2)
        {
            WriteFallback($"Потрібно: Kvatyrka.exe --image <профіль.json> <папка>, а отримано: {string.Join(' ', args)}");
            return 64;
        }
        var (profilePath, outDir) = (rest[0], rest[1]);
        var report = new List<string>();
        try
        {
            Directory.CreateDirectory(outDir);
            using (var probe = Registry.LocalMachine.OpenSubKey(@"KV_SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                if (probe is null) throw new InvalidOperationException("Реєстр образу не відкритий (немає HKLM\\KV_SOFTWARE)");
            var profile = ImageProfile.Parse(File.ReadAllText(profilePath, Encoding.UTF8));
            var (lines, commands) = profile.Apply(Improvements.Catalog, new ImageRegistry(Improvements.Registry));
            report.AddRange(lines);
            File.WriteAllLines(Path.Combine(outDir, "firstrun.txt"), commands, new UTF8Encoding(false));
            File.WriteAllLines(Path.Combine(outDir, "report.txt"), report, new UTF8Encoding(false));
            return 0;
        }
        catch (Exception ex)
        {
            report.Add("ПОМИЛКА: " + ex.GetBaseException().Message);
            try { File.WriteAllLines(Path.Combine(outDir, "report.txt"), report, new UTF8Encoding(false)); }
            catch (Exception write) { WriteFallback($"{string.Join(Environment.NewLine, report)}{Environment.NewLine}Звіт не записався в {outDir}: {write.Message}"); }
            return 1;
        }
    }

    // Якщо папка звіту недоступна – звіт у тимчасовій папці, щоб причину було де знайти.
    private static void WriteFallback(string text)
    {
        try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "kvatyrka-image-report.txt"), text, new UTF8Encoding(false)); }
        catch (Exception) { Log.Write(text); }
    }
}
