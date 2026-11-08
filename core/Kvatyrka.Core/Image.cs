using System.Text.Json;

namespace Kvatyrka.Core;

// Реєстр образу Windows, відкритий збирачем як HKLM\KV_SOFTWARE, HKLM\KV_SYSTEM і HKLM\KV_USER.
public sealed class ImageRegistry(IRegistry inner) : IRegistry
{
    private static (Hive, string)? Map(Hive hive, string key)
    {
        if (hive == Hive.User)
            return key.StartsWith(@"Software\Classes\", StringComparison.OrdinalIgnoreCase) || key.Equals(@"Software\Classes", StringComparison.OrdinalIgnoreCase)
                ? null : (Hive.Machine, @"KV_USER\" + key);
        if (key.StartsWith(@"SOFTWARE\", StringComparison.OrdinalIgnoreCase)) return (Hive.Machine, @"KV_SOFTWARE\" + key[9..]);
        if (key.StartsWith(@"SYSTEM\CurrentControlSet\", StringComparison.OrdinalIgnoreCase)) return (Hive.Machine, @"KV_SYSTEM\ControlSet001\" + key[25..]);
        return null;
    }

    private static (Hive, string) Need(Hive hive, string key) => Map(hive, key) ?? throw new InvalidOperationException($"У образі немає гілки для {new RegPath(hive, key)}");

    public bool CanWrite(Hive hive, string key) => Map(hive, key) is not null;

    public RegValue? GetValue(Hive hive, string key, string name) => Map(hive, key) is { } m ? inner.GetValue(m.Item1, m.Item2, name) : null;

    public bool KeyExists(Hive hive, string key) => Map(hive, key) is { } m && inner.KeyExists(m.Item1, m.Item2);

    public bool IsEmptyKey(Hive hive, string key) => Map(hive, key) is { } m && inner.IsEmptyKey(m.Item1, m.Item2);

    public void Set(Hive hive, string key, string name, RegType type, object value) { var (h, k) = Need(hive, key); inner.Set(h, k, name, type, value); }

    public void DeleteValue(Hive hive, string key, string name) { var (h, k) = Need(hive, key); inner.DeleteValue(h, k, name); }

    public void DeleteKey(Hive hive, string key) { var (h, k) = Need(hive, key); inner.DeleteKey(h, k); }
}

// Вибір покращень у профілі збирача: рекомендовані плюс власні зміни.
public sealed class ProfileTweaks
{
    public bool Recommended { get; init; } = true;
    public List<string> On { get; init; } = new();
    public List<string> Off { get; init; } = new();
    public Dictionary<string, string> Choices { get; init; } = new();

    public Dictionary<string, string> Resolve(Catalog catalog)
    {
        var result = new Dictionary<string, string>();
        if (Recommended)
            foreach (var t in catalog.Tweaks)
                if (!t.IsWindowsDefault(t.Recommended)) result[t.Id] = t.Recommended;
        foreach (var id in On)
        {
            var t = catalog[id];
            if (t.IsChoice) throw new CatalogException($"{id} – вибір, його задають у «choices»");
            result[id] = "on";
        }
        foreach (var id in Off)
        {
            if (On.Contains(id)) throw new CatalogException($"{id} одночасно в «on» і «off»");
            if (catalog[id].IsChoice) throw new CatalogException($"{id} – вибір, його задають у «choices»");
            result.Remove(id);
        }
        foreach (var (id, value) in Choices)
        {
            var t = catalog[id];
            if (!t.IsChoice || !t.IsValid(value)) throw new CatalogException($"{id}: немає варіанта «{value}»");
            if (t.IsWindowsDefault(value)) result.Remove(id); else result[id] = value;
        }
        return result;
    }
}

// Додатковий запис реєстру з профілю (напр. швидкість миші чи тема): ставиться один раз, без охоронця.
public sealed class ProfileValue
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = "dword";
    public JsonElement Value { get; init; }
}

public sealed class ImageProfile
{
    public ProfileTweaks Tweaks { get; init; } = new();
    public List<ProfileValue> Registry { get; init; } = new();

    // Помилка в назві поля профілю – помилка, а не тихо проігнорований вибір.
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };

    // З профілю беремо лише «tweaks» і «registry», решту полів читає сам збирач.
    public static ImageProfile Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tweaks = root.TryGetProperty("tweaks", out var t) ? t.Deserialize<ProfileTweaks>(Options) ?? new() : throw new CatalogException("У профілі немає поля «tweaks»");
            var registry = root.TryGetProperty("registry", out var r) ? r.Deserialize<List<ProfileValue>>(Options) ?? new() : new();
            return new ImageProfile { Tweaks = tweaks, Registry = registry };
        }
        catch (JsonException ex) { throw new CatalogException($"Профіль пошкоджений: {ex.Message}"); }
    }

    // Застосовує профіль до образу. Повертає звіт і команди для першого запуску.
    public (List<string> Report, List<string> Commands) Apply(Catalog catalog, IRegistry image)
    {
        var engine = new TweakEngine(catalog, image);
        var report = new List<string>();
        var commands = new List<string>();
        foreach (var (id, value) in Tweaks.Resolve(catalog))
        {
            var t = catalog[id];
            var r = engine.Apply(t, value, Scope.All);
            commands.AddRange(r.Commands);
            var label = t.IsChoice ? $"{t.Name}: {t.Choice!.Options.First(o => o.Id == value).Name}" : t.Name;
            report.Add(r.Skipped.Count > 0 ? $"{label} (частину застосує Кватирка при першому вході)" : label);
        }
        foreach (var v in Registry)
        {
            var op = RegOp.For(new Choice { Key = v.Key, Name = v.Name, Type = v.Type }, v.Value, "registry");
            if (op.OnValue is null) throw new CatalogException($"registry: для {v.Key}\\{v.Name} потрібне значення");
            if (!image.CanWrite(op.Path.Hive, op.Path.Key)) throw new CatalogException($"registry: {v.Key} не можна змінити в образі");
            image.Set(op.Path.Hive, op.Path.Key, op.Name, op.Kind, op.OnValue);
            report.Add($"{op.Path}\\{op.Name} = {op.OnValue}");
        }
        return (report, commands);
    }
}
