using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace Kvatyrka.Core;

public enum Hive { Machine, User }

// Перші три – те, що може писати каталог; решту рушій лише зберігає й повертає, якщо так було до Кватирки.
public enum RegType { DWord, String, ExpandString, QWord, Binary, MultiString }

public sealed record RegValue(RegType Type, object Value);

// Шлях у реєстрі: «HKLM\…» або «HKCU\…».
public readonly record struct RegPath(Hive Hive, string Key)
{
    public static RegPath Parse(string path)
    {
        if (path.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase)) return new(Hive.Machine, path[5..]);
        if (path.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase)) return new(Hive.User, path[5..]);
        throw new CatalogException($"Шлях має починатися з HKLM\\ або HKCU\\: {path}");
    }

    public override string ToString() => (Hive == Hive.Machine ? @"HKLM\" : @"HKCU\") + Key;
}

public sealed class CatalogException(string message) : Exception(message);

// Один запис у реєстрі: що ставити, коли покращення увімкнене, і що повернути, коли вимкнене.
public sealed class RegOp
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = "dword";
    public JsonElement Value { get; init; }
    public JsonElement Off { get; init; }
    public string? RevertKey { get; init; }

    [JsonIgnore] public RegPath Path { get; private set; }
    [JsonIgnore] public RegType Kind { get; private set; }
    [JsonIgnore] public object? OnValue { get; private set; }
    [JsonIgnore] public object? OffValue { get; private set; }
    [JsonIgnore] public RegPath? RevertPath { get; private set; }

    internal void Prepare(string owner)
    {
        Path = RegPath.Parse(Key);
        Kind = Type switch
        {
            "dword" => RegType.DWord,
            "string" => RegType.String,
            "expand" => RegType.ExpandString,
            _ => throw new CatalogException($"{owner}: невідомий тип «{Type}»"),
        };
        OnValue = Convert(Value, owner);
        OffValue = Off.ValueKind == JsonValueKind.Undefined ? null : Convert(Off, owner);
        if (RevertKey is not null)
        {
            var r = RegPath.Parse(RevertKey);
            // Видаляти дозволено лише один власний ключ {GUID} користувача в Classes\CLSID і лише той, у якому лежить сам запис.
            if (r.Hive != Hive.User || !ClsidKey.IsMatch(r.Key)
                || !(Path.Key.Equals(r.Key, StringComparison.OrdinalIgnoreCase) || Path.Key.StartsWith(r.Key + @"\", StringComparison.OrdinalIgnoreCase)))
                throw new CatalogException($"{owner}: revertKey має бути ключем HKCU\\Software\\Classes\\CLSID\\{{GUID}}, у якому лежить запис");
            RevertPath = r;
        }
    }

    private static readonly Regex ClsidKey = new(@"^Software\\Classes\\CLSID\\\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}$", RegexOptions.IgnoreCase);

    private object? Convert(JsonElement e, string owner) => (Kind, e.ValueKind) switch
    {
        (_, JsonValueKind.Null) => null,
        (RegType.DWord, JsonValueKind.Number) => e.GetInt32(),
        (RegType.String or RegType.ExpandString, JsonValueKind.String) => e.GetString(),
        _ => throw new CatalogException($"{owner}: значення {Name} не відповідає типу {Type}"),
    };

    public static RegOp For(Choice c, JsonElement value, string owner)
    {
        var op = new RegOp { Key = c.Key, Name = c.Name, Type = c.Type, Value = value };
        op.Prepare(owner);
        return op;
    }
}

public sealed class ChoiceOption
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public JsonElement Value { get; init; }
    [JsonIgnore] public RegOp Op { get; internal set; } = null!;
}

// Вибір з кількох варіантів одного значення, напр. вигляд пошуку на панелі задач.
public sealed class Choice
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = "dword";
    public string Windows { get; init; } = string.Empty;
    public List<ChoiceOption> Options { get; init; } = new();
}

public sealed class RunCommands
{
    public string On { get; init; } = string.Empty;
    public string Off { get; init; } = string.Empty;
}

public enum Effect { Now, Explorer, Signin, Restart }

public sealed class Tweak
{
    public string Id { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public JsonElement Default { get; init; }
    [JsonPropertyName("effect")] public string EffectName { get; init; } = "now";
    public string? Conflicts { get; init; }
    public string? Refresh { get; init; }
    public List<RegOp> Set { get; init; } = new();
    public Choice? Choice { get; init; }
    public RunCommands? Run { get; init; }

    [JsonIgnore] public bool IsChoice => Choice is not null;
    [JsonIgnore] public Effect Effect { get; private set; }

    // Рекомендоване значення: «on», «off» або id варіанта.
    [JsonIgnore] public string Recommended { get; private set; } = "off";

    // Усі записи покращення (для вибору – записи всіх варіантів).
    [JsonIgnore] public IEnumerable<RegOp> AllOps => IsChoice ? Choice!.Options.Select(o => o.Op) : Set;

    [JsonIgnore] public bool HasMachine => AllOps.Any(o => o.Path.Hive == Hive.Machine || o.RevertPath?.Hive == Hive.Machine);

    [JsonIgnore] public bool HasUser => AllOps.Any(o => o.Path.Hive == Hive.User);

    // Вибір людини зберігається там, де зберігаються самі зміни: спільні – для всього ПК, особисті – у профілі.
    [JsonIgnore] public Hive Owner => HasMachine ? Hive.Machine : Hive.User;

    public bool IsValid(string value) => IsChoice ? Choice!.Options.Any(o => o.Id == value) : value is "on" or "off";

    // Значення, яке означає «як у Windows»: вибір людини тоді не зберігається.
    public bool IsWindowsDefault(string value) => IsChoice ? value == Choice!.Windows : value == "off";

    internal void Prepare()
    {
        if (Id.Length == 0 || Name.Length == 0 || Description.Length == 0) throw new CatalogException($"Покращенню «{Id}» бракує id, назви чи опису");
        Effect = EffectName switch
        {
            "now" => Effect.Now,
            "explorer" => Effect.Explorer,
            "signin" => Effect.Signin,
            "restart" => Effect.Restart,
            _ => throw new CatalogException($"{Id}: невідомий effect «{EffectName}»"),
        };
        if (IsChoice == (Set.Count > 0)) throw new CatalogException($"{Id}: потрібне або set, або choice");
        if (IsChoice)
        {
            var c = Choice!;
            if (c.Options.Count < 2) throw new CatalogException($"{Id}: у виборі мало варіантів");
            foreach (var o in c.Options) o.Op = RegOp.For(c, o.Value, Id);
            if (c.Options.All(o => o.Id != c.Windows)) throw new CatalogException($"{Id}: «windows» має бути одним з варіантів");
            if (c.Options.Select(o => o.Id).Distinct().Count() != c.Options.Count) throw new CatalogException($"{Id}: варіанти повторюються");
            Recommended = Default.ValueKind == JsonValueKind.String ? Default.GetString()! : throw new CatalogException($"{Id}: default вибору – id варіанта");
            if (!IsValid(Recommended)) throw new CatalogException($"{Id}: немає варіанта «{Recommended}»");
        }
        else
        {
            foreach (var op in Set) op.Prepare(Id);
            Recommended = Default.ValueKind switch
            {
                JsonValueKind.True => "on",
                JsonValueKind.False => "off",
                _ => throw new CatalogException($"{Id}: default перемикача – true або false"),
            };
        }
        if (Conflicts is not null and not "insider") throw new CatalogException($"{Id}: невідомий конфлікт «{Conflicts}»");
        if (Refresh is not null and not "mouse") throw new CatalogException($"{Id}: невідоме оновлення «{Refresh}»");
        if (Run is not null && (Run.On.Length == 0 || Run.Off.Length == 0 || !HasMachine)) throw new CatalogException($"{Id}: команди мають бути для обох станів і лише для спільних змін");
    }
}

public sealed class Category
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
}

// Нав'язана програма зі Store, яку можна прибрати.
public sealed class AppEntry
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public List<string> Packages { get; init; } = new();
    public bool Default { get; init; }
    public string? With { get; init; }

    public bool Matches(string packageName) => Packages.Any(p => Wildcard.Match(p, packageName));
}

public static class Wildcard
{
    // Шаблон з «*» на початку, в кінці чи посередині; без урахування регістру.
    public static bool Match(string pattern, string text)
    {
        var parts = pattern.Split('*');
        if (parts.Length == 1) return string.Equals(pattern, text, StringComparison.OrdinalIgnoreCase);
        if (!text.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase)) return false;
        var pos = parts[0].Length;
        for (var i = 1; i < parts.Length - 1; i++)
        {
            var at = text.IndexOf(parts[i], pos, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return false;
            pos = at + parts[i].Length;
        }
        var last = parts[^1];
        return text.Length - pos >= last.Length && text.EndsWith(last, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class Catalog
{
    public int Version { get; init; }
    public List<Category> Categories { get; init; } = new();
    public List<Tweak> Tweaks { get; init; } = new();
    public List<AppEntry> Apps { get; init; } = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    // Id передаються в командному рядку процесу адміністратора, тож без пробілів і лапок.
    private static readonly Regex SafeId = new("^[A-Za-z0-9._-]+$");

    public Tweak this[string id] => Tweaks.FirstOrDefault(t => t.Id == id) ?? throw new CatalogException($"Немає покращення «{id}»");

    public Tweak? Find(string id) => Tweaks.FirstOrDefault(t => t.Id == id);

    public static Catalog Parse(string json)
    {
        Catalog c;
        try { c = JsonSerializer.Deserialize<Catalog>(json, Options) ?? throw new CatalogException("Порожній каталог"); }
        catch (JsonException ex) { throw new CatalogException($"Каталог пошкоджений: {ex.Message}"); }
        c.Validate();
        return c;
    }

    // Каталог, вбудований у програму.
    public static Catalog Builtin()
    {
        using var s = typeof(Catalog).Assembly.GetManifestResourceStream("Kvatyrka.catalog.json") ?? throw new CatalogException("Немає вбудованого каталогу");
        using var r = new StreamReader(s);
        return Parse(r.ReadToEnd());
    }

    private void Validate()
    {
        if (Version != 1) throw new CatalogException($"Невідома версія каталогу {Version}");
        var categories = Categories.Select(c => c.Id).ToHashSet();
        if (categories.Count != Categories.Count) throw new CatalogException("Розділи каталогу повторюються");
        var ids = new HashSet<string>();
        foreach (var t in Tweaks)
        {
            if (!ids.Add(t.Id)) throw new CatalogException($"Покращення «{t.Id}» повторюється");
            if (!categories.Contains(t.Category)) throw new CatalogException($"{t.Id}: немає розділу «{t.Category}»");
            t.Prepare();
        }
        // Один запис реєстру належить лише одному покращенню, інакше вони перетиратимуть одне одного.
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in Tweaks)
            foreach (var op in t.AllOps.DistinctBy(o => $"{o.Path}\\{o.Name}"))
                if (!owners.TryAdd($"{op.Path}\\{op.Name}", t.Id)) throw new CatalogException($"{op.Path}\\{op.Name} є і в {owners[$"{op.Path}\\{op.Name}"]}, і в {t.Id}");
        // Ключ, який покращення видаляє при поверненні, не може містити записів іншого покращення.
        foreach (var t in Tweaks)
            foreach (var rk in t.AllOps.Where(o => o.RevertPath is not null).Select(o => o.RevertPath!.Value.ToString()))
                foreach (var other in Tweaks.Where(x => x != t))
                    if (other.AllOps.Any(o => o.Path.ToString().Equals(rk, StringComparison.OrdinalIgnoreCase) || o.Path.ToString().StartsWith(rk + @"\", StringComparison.OrdinalIgnoreCase)))
                        throw new CatalogException($"{t.Id} видаляє {rk}, а там записи {other.Id}");
        foreach (var id in Tweaks.Select(t => t.Id).Concat(Tweaks.Where(t => t.IsChoice).SelectMany(t => t.Choice!.Options.Select(o => o.Id))).Concat(Apps.Select(a => a.Id)))
            if (!SafeId.IsMatch(id)) throw new CatalogException($"Недопустимий id «{id}»: лише латиниця, цифри, крапка, дефіс і підкреслення");
        var apps = new HashSet<string>();
        foreach (var a in Apps)
        {
            if (!apps.Add(a.Id)) throw new CatalogException($"Програма «{a.Id}» повторюється");
            if (a.Name.Length == 0 || a.Packages.Count == 0) throw new CatalogException($"Програмі «{a.Id}» бракує назви чи пакунків");
            if (a.With is not null && Find(a.With) is null) throw new CatalogException($"{a.Id}: немає покращення «{a.With}»");
            // Шаблон має однозначно називати видавця чи програму, а не зачіпати все підряд.
            foreach (var pkg in a.Packages)
                if (pkg.Replace("*", "").Length < 6 || pkg.Contains("**")) throw new CatalogException($"{a.Id}: надто широкий шаблон «{pkg}»");
        }
    }
}
