namespace Kvatyrka.Core;

// Доручення процесу з правами адміністратора. Приймається лише те, що є в каталозі, –
// тож навіть підроблений виклик не зробить нічого поза описаними покращеннями.
public sealed class ElevatedRequest
{
    public List<(string Id, string Value)> Tweaks { get; } = new();
    public List<string> Apps { get; } = new();
    public bool? Guardian { get; set; }

    public bool IsEmpty => Tweaks.Count == 0 && Apps.Count == 0 && Guardian is null;

    public IEnumerable<string> ToArgs()
    {
        yield return "--elevated";
        foreach (var (id, value) in Tweaks) yield return $"tweak:{id}={value}";
        foreach (var id in Apps) yield return $"app:{id}";
        if (Guardian is { } g) yield return g ? "guardian:on" : "guardian:off";
    }

    public static ElevatedRequest Parse(IEnumerable<string> args, Catalog catalog)
    {
        var r = new ElevatedRequest();
        foreach (var a in args)
        {
            if (a.Equals("--elevated", StringComparison.OrdinalIgnoreCase)) continue;
            if (a.StartsWith("tweak:", StringComparison.Ordinal))
            {
                var body = a[6..];
                var eq = body.IndexOf('=');
                if (eq <= 0) throw new CatalogException($"Незрозуміле доручення: {a}");
                var (id, value) = (body[..eq], body[(eq + 1)..]);
                var t = catalog.Find(id) ?? throw new CatalogException($"Немає покращення «{id}»");
                if (!t.IsValid(value)) throw new CatalogException($"{id}: недопустиме значення «{value}»");
                r.Tweaks.Add((id, value));
            }
            else if (a.StartsWith("app:", StringComparison.Ordinal))
            {
                var id = a[4..];
                if (catalog.Apps.All(x => x.Id != id)) throw new CatalogException($"Немає програми «{id}»");
                r.Apps.Add(id);
            }
            else if (a is "guardian:on" or "guardian:off") r.Guardian = a == "guardian:on";
            else throw new CatalogException($"Незрозуміле доручення: {a}");
        }
        return r;
    }
}

public static class SystemFacts
{
    // Програма попередньої оцінки Windows: їй потрібна повна діагностика.
    public static bool IsInsider(IRegistry r)
    {
        const string key = @"SOFTWARE\Microsoft\WindowsSelfHost\Applicability";
        return r.Get(Hive.Machine, key, "BranchName") is string { Length: > 0 }
               || r.Get(Hive.Machine, key, "EnablePreviewBuilds") is int and > 0;
    }

    // Чи заважає щось у системі цьому покращенню.
    public static string? Conflict(Tweak t, IRegistry r) =>
        t.Conflicts == "insider" && IsInsider(r) ? "Ви в програмі Windows Insider, а їй потрібна повна діагностика, тож Кватирка це не вмикає." : null;
}
