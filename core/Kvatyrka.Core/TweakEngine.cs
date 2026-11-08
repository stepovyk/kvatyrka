namespace Kvatyrka.Core;

public interface IRegistry
{
    // Значення з типом або null, якщо його немає.
    RegValue? GetValue(Hive hive, string key, string name);
    void Set(Hive hive, string key, string name, RegType type, object value);
    void DeleteValue(Hive hive, string key, string name);
    void DeleteKey(Hive hive, string key);
    bool KeyExists(Hive hive, string key);

    // Порожній – без значень і підключів.
    bool IsEmptyKey(Hive hive, string key);

    // Чи можна писати сюди (у відкритому образі гілка Classes користувача недоступна).
    bool CanWrite(Hive hive, string key) => true;

    // int для DWORD, string для рядків; решта типів для порівнянь – як відсутні.
    object? Get(Hive hive, string key, string name) => GetValue(hive, key, name) is { Type: RegType.DWord or RegType.String or RegType.ExpandString } v ? v.Value : null;
}

[Flags]
public enum Scope { User = 1, Machine = 2, All = User | Machine }

public enum TweakState { Off, Partial, On }

public sealed record ApplyResult(bool Changed, IReadOnlyList<string> Commands, IReadOnlyList<string> Skipped);

// Стан, застосування й повернення покращень. Вибір людини запам'ятовується в реєстрі,
// щоб охоронець після оновлень Windows повертав саме його. Перед першою зміною рушій зберігає,
// як було, і при вимкненні повертає рівно те саме – навіть нестандартні значення.
public sealed class TweakEngine(Catalog catalog, IRegistry registry)
{
    public const string UserChoices = @"Software\Kvatyrka\Tweaks";
    public const string MachineChoices = @"SOFTWARE\Kvatyrka\Tweaks";
    public const string UserBackup = @"Software\Kvatyrka\Backup";
    public const string MachineBackup = @"SOFTWARE\Kvatyrka\Backup";

    public Catalog Catalog { get; } = catalog;

    private static bool Same(object? current, object? target) => (current, target) switch
    {
        (null, null) => true,
        (int a, int b) => a == b,
        (string a, string b) => string.Equals(a, b, StringComparison.Ordinal),
        _ => false,
    };

    private object? Read(RegOp op) => registry.Get(op.Path.Hive, op.Path.Key, op.Name);

    private bool IsSet(RegOp op) => Same(Read(op), op.OnValue);

    public TweakState State(Tweak t)
    {
        if (t.IsChoice) return Selected(t) is { } s && !t.IsWindowsDefault(s) ? TweakState.On : TweakState.Off;
        var on = t.Set.Count(IsSet);
        return on == 0 ? TweakState.Off : on == t.Set.Count ? TweakState.On : TweakState.Partial;
    }

    // Поточний варіант вибору; немає значення – як у Windows; невідоме значення – null.
    public string? Selected(Tweak t)
    {
        var c = t.Choice ?? throw new ArgumentException($"{t.Id} – не вибір");
        var current = Read(c.Options[0].Op);
        if (current is null) return registry.GetValue(c.Options[0].Op.Path.Hive, c.Options[0].Op.Path.Key, c.Name) is null ? c.Windows : null;
        return c.Options.FirstOrDefault(o => Same(current, o.Op.OnValue))?.Id;
    }

    // Поточне значення для інтерфейсу: «on»/«off» або id варіанта.
    public string? Current(Tweak t) => t.IsChoice ? Selected(t) : State(t) == TweakState.On ? "on" : "off";

    private static (Hive Hive, string Key) ChoiceStore(Tweak t) => t.Owner == Hive.Machine ? (Hive.Machine, MachineChoices) : (Hive.User, UserChoices);

    public string? Recorded(Tweak t)
    {
        var (hive, key) = ChoiceStore(t);
        return registry.Get(hive, key, t.Id) is string v && t.IsValid(v) ? v : null;
    }

    // Записи, які мають бути для значення, з ознакою «увімкнено».
    private static IEnumerable<(RegOp Op, bool On)> Target(Tweak t, string value)
    {
        if (!t.IsChoice) return t.Set.Select(op => (op, value == "on"));
        var c = t.Choice!;
        if (t.IsWindowsDefault(value)) return new[] { (c.Options[0].Op, false) };
        return new[] { (c.Options.First(o => o.Id == value).Op, true) };
    }

    // Різні записи покращення (у виборі всі варіанти пишуть одне значення).
    private static List<RegOp> Slots(Tweak t) => t.IsChoice ? new List<RegOp> { t.Choice!.Options[0].Op } : t.Set;

    private static bool InScope(Hive hive, Scope scope) => scope.HasFlag(hive == Hive.Machine ? Scope.Machine : Scope.User);

    // Чи запис уже в потрібному стані. «Вимкнено» – це будь-що, крім значення Кватирки:
    // чуже чи нестандартне значення не наше, і повернення його не чіпає.
    private bool Matches(Tweak t, RegOp op, bool on)
    {
        if (on) return IsSet(op);
        if (t.IsChoice) return !t.Choice!.Options.Where(o => o.Id != t.Choice.Windows).Any(o => IsSet(o.Op));
        return !IsSet(op);
    }

    // ---------- як було до Кватирки ----------

    private static string BackupKey(Tweak t, Hive hive) => (hive == Hive.Machine ? MachineBackup : UserBackup) + @"\" + t.Id;

    private bool HasBackup(Tweak t, Hive hive) => registry.Get(hive, BackupKey(t, hive), "saved") is 1;

    private static string? Parent(string key) => key.LastIndexOf('\\') is var at and > 0 ? key[..at] : null;

    // Найглибший наявний ключ на шляху (сам ключ, якщо він є): усе нижче створила б Кватирка.
    private string DeepestExisting(Hive hive, string key)
    {
        for (string? k = key; k is not null; k = Parent(k))
            if (registry.KeyExists(hive, k)) return k;
        return string.Empty;
    }

    private void SaveBackup(Tweak t, Hive hive)
    {
        var key = BackupKey(t, hive);
        var slots = Slots(t);
        for (var i = 0; i < slots.Count; i++)
        {
            var op = slots[i];
            if (op.Path.Hive != hive) continue;
            registry.Set(hive, key, $"a{i}", RegType.String, DeepestExisting(hive, op.RevertPath?.Key ?? op.Path.Key));
            // Значення Кватирки, що вже стояло (з іншої програми чи вручну), – не «як було»: при вимкненні тут буде значення Windows.
            if (OursAlready(t, op)) registry.Set(hive, key, $"n{i}", RegType.DWord, 1);
            else if (registry.GetValue(hive, op.Path.Key, op.Name) is { } v) registry.Set(hive, key, $"v{i}", v.Type, v.Value);
        }
        registry.Set(hive, key, "saved", RegType.DWord, 1);
    }

    private bool OursAlready(Tweak t, RegOp op) =>
        t.IsChoice ? t.Choice!.Options.Where(o => o.Id != t.Choice.Windows).Any(o => IsSet(o.Op)) : IsSet(op);

    private void RestoreBackup(Tweak t, Hive hive)
    {
        var key = BackupKey(t, hive);
        var slots = Slots(t);
        for (var i = 0; i < slots.Count; i++)
        {
            var op = slots[i];
            if (op.Path.Hive != hive) continue;
            var target = op.RevertPath?.Key ?? op.Path.Key;
            var existing = registry.Get(hive, key, $"a{i}") as string ?? target;
            var existed = existing.Equals(target, StringComparison.OrdinalIgnoreCase);
            if (registry.Get(hive, key, $"n{i}") is 1) WriteOff(op);
            else if (registry.GetValue(hive, key, $"v{i}") is { } v) registry.Set(hive, op.Path.Key, op.Name, v.Type, v.Value);
            else if (op.RevertPath is { } rk && !existed) registry.DeleteKey(rk.Hive, rk.Key);
            else registry.DeleteValue(hive, op.Path.Key, op.Name);
            // Ключі, які створила Кватирка, прибираємо знизу вгору, доки вони порожні.
            for (string? k = op.Path.Key; k is not null && k.Length > existing.Length; k = Parent(k))
            {
                if (!registry.KeyExists(hive, k)) continue;
                if (!registry.IsEmptyKey(hive, k)) break;
                registry.DeleteKey(hive, k);
            }
        }
        registry.DeleteKey(hive, key);
    }

    // Без збереженого стану (покращення було ввімкнене ще до Кватирки чи в образі) – значення Windows з каталогу.
    private void WriteOff(RegOp op)
    {
        if (op.RevertPath is { } rk) registry.DeleteKey(rk.Hive, rk.Key);
        else if (op.OffValue is null) registry.DeleteValue(op.Path.Hive, op.Path.Key, op.Name);
        else registry.Set(op.Path.Hive, op.Path.Key, op.Name, op.Kind, op.OffValue);
    }

    // ---------- застосування ----------

    // Чи доведеться щось змінювати в спільних налаштуваннях ПК (потрібні права адміністратора).
    public bool NeedsMachine(Tweak t, string value) =>
        t.HasMachine && (Target(t, value).Any(x => x.Op.Path.Hive == Hive.Machine && !Matches(t, x.Op, x.On))
                         || (t.IsWindowsDefault(value) && HasBackup(t, Hive.Machine))
                         || (t.Owner == Hive.Machine && Recorded(t) != (t.IsWindowsDefault(value) ? null : value))
                         || CommandFor(t, value) is not null);

    public ApplyResult Apply(Tweak t, string value, Scope scope)
    {
        if (!t.IsValid(value)) throw new ArgumentException($"{t.Id}: недопустиме значення «{value}»");
        var off = t.IsWindowsDefault(value);
        var commands = CommandFor(t, value) is { } c && scope.HasFlag(Scope.Machine) ? new[] { c } : Array.Empty<string>();
        var changed = false;
        var skipped = new List<string>();
        foreach (var hive in new[] { Hive.Machine, Hive.User })
        {
            if (!InScope(hive, scope)) continue;
            var ops = Target(t, value).Where(x => x.Op.Path.Hive == hive).ToList();
            if (ops.Count == 0) continue;
            var writable = ops.Where(x => registry.CanWrite(hive, (!x.On && x.Op.RevertPath is { } rk ? rk : x.Op.Path).Key)).ToList();
            skipped.AddRange(ops.Except(writable).Select(x => $"{x.Op.Path}\\{x.Op.Name}"));
            if (writable.Count == 0) continue;
            if (off && HasBackup(t, hive))
            {
                RestoreBackup(t, hive);
                changed = true;
                continue;
            }
            var todo = writable.Where(x => !Matches(t, x.Op, x.On)).ToList();
            if (todo.Count == 0) continue;
            if (!off && !HasBackup(t, hive)) SaveBackup(t, hive);
            foreach (var (op, on) in todo)
            {
                if (on) registry.Set(op.Path.Hive, op.Path.Key, op.Name, op.Kind, op.OnValue!);
                else WriteOff(op);
            }
            changed = true;
        }
        if (InScope(t.Owner, scope)) Record(t, value);
        return new ApplyResult(changed, commands, skipped);
    }

    private void Record(Tweak t, string value)
    {
        var (hive, key) = ChoiceStore(t);
        if (t.IsWindowsDefault(value)) registry.DeleteValue(hive, key, t.Id);
        else if (!Same(registry.Get(hive, key, t.Id), value)) registry.Set(hive, key, t.Id, RegType.String, value);
    }

    // Команда, яку треба виконати, щоб перейти до значення (напр. powercfg для гібернації), або null.
    // Виконується до запису в реєстр: якщо вона не вдасться, стан лишиться чесним.
    public string? CommandFor(Tweak t, string value) =>
        t.Run is null || t.IsChoice || value == Current(t) ? null : value == "on" ? t.Run.On : t.Run.Off;

    // Запам'ятовані вибори, які оновлення Windows чи інша програма змінили в межах scope.
    // Те, з чим зараз щось несумісне (напр. Windows Insider), охоронець не чіпає.
    public IReadOnlyList<(Tweak Tweak, string Value)> Drifted(Scope scope) =>
        Catalog.Tweaks
            .Select(t => (Tweak: t, Value: Recorded(t)))
            .Where(x => x.Value is not null && SystemFacts.Conflict(x.Tweak, registry) is null
                        && !Target(x.Tweak, x.Value).All(o => !InScope(o.Op.Path.Hive, scope) || Matches(x.Tweak, o.Op, o.On)))
            .Select(x => (x.Tweak, x.Value!))
            .ToList();

    // Повертає запам'ятований вибір, якщо його змінили. Команди тут не виконуються – їх виконує той, хто викликає.
    public IReadOnlyList<(Tweak Tweak, ApplyResult Result)> Enforce(Scope scope)
    {
        var fixedUp = new List<(Tweak, ApplyResult)>();
        foreach (var (t, value) in Drifted(scope))
        {
            var r = Apply(t, value, scope);
            if (r.Changed) fixedUp.Add((t, r));
        }
        return fixedUp;
    }
}
