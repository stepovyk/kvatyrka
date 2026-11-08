using System.Text.RegularExpressions;
using Kvatyrka.Core;

namespace Kvatyrka.Core.Tests;

// Реєстр у пам'яті: ключі й значення з типом, як у Windows (ключ існує окремо від значень).
public sealed class FakeRegistry : IRegistry
{
    public readonly Dictionary<string, RegValue> Values = new(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase);
    public Func<Hive, string, bool> Writable { get; set; } = (_, _) => true;

    private static string K(Hive h, string key) => (h == Hive.Machine ? "HKLM\\" : "HKCU\\") + key.TrimEnd('\\');
    private static string V(Hive h, string key, string name) => K(h, key) + "|" + name;

    public RegValue? GetValue(Hive hive, string key, string name) => Values.TryGetValue(V(hive, key, name), out var v) ? v : null;

    public object? Get(Hive hive, string key, string name) => GetValue(hive, key, name) is { Type: RegType.DWord or RegType.String or RegType.ExpandString } v ? v.Value : null;

    public void Set(Hive hive, string key, string name, RegType type, object value)
    {
        if (!Writable(hive, key)) throw new InvalidOperationException("запис туди заборонено");
        // Як CreateSubKey: створюються й усі батьківські ключі.
        var parts = K(hive, key).Split('\\');
        for (var n = 2; n <= parts.Length; n++) Keys.Add(string.Join('\\', parts[..n]));
        Values[V(hive, key, name)] = new RegValue(type, value);
    }

    public void DeleteValue(Hive hive, string key, string name) => Values.Remove(V(hive, key, name));

    public void DeleteKey(Hive hive, string key)
    {
        if (string.IsNullOrWhiteSpace(key.Trim('\\'))) throw new ArgumentException("Порожній ключ");
        var prefix = K(hive, key);
        foreach (var k in Values.Keys.Where(k => k.StartsWith(prefix + "|", StringComparison.OrdinalIgnoreCase) || k.StartsWith(prefix + "\\", StringComparison.OrdinalIgnoreCase)).ToList()) Values.Remove(k);
        Keys.RemoveWhere(k => k.Equals(prefix, StringComparison.OrdinalIgnoreCase) || k.StartsWith(prefix + "\\", StringComparison.OrdinalIgnoreCase));
    }

    public bool KeyExists(Hive hive, string key) => Keys.Contains(K(hive, key));

    public bool IsEmptyKey(Hive hive, string key)
    {
        var prefix = K(hive, key);
        return Keys.Contains(prefix)
               && !Values.Keys.Any(k => k.StartsWith(prefix + "|", StringComparison.OrdinalIgnoreCase))
               && !Keys.Any(k => k.StartsWith(prefix + "\\", StringComparison.OrdinalIgnoreCase));
    }

    public bool CanWrite(Hive hive, string key) => Writable(hive, key);

    // Знімок разом із ключами й типами значень – «рівно як було» означає все це.
    public string Snapshot() =>
        string.Join("\n", Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).Select(k => "K " + k)
            .Concat(Values.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase).Select(v => $"V {v.Key}={v.Value.Type}:{Show(v.Value.Value)}")));

    private static string Show(object o) => o switch { byte[] b => Convert.ToHexString(b), string[] m => string.Join("|", m), _ => o.ToString() ?? "" };
}

public class EngineTests
{
    private static readonly Catalog Catalog = Catalog.Builtin();

    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string here = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", ".."));

    [Fact]
    public void BuiltinCatalogIsConsistent()
    {
        Assert.NotEmpty(Catalog.Tweaks);
        Assert.All(Catalog.Categories, c => Assert.Contains(Catalog.Tweaks, t => t.Category == c.Id));
        Assert.All(Catalog.Tweaks, t => Assert.DoesNotContain("—", t.Name + t.Description));
        Assert.All(Catalog.Tweaks, t => Assert.EndsWith(".", t.Description));
    }

    [Fact]
    public void CategoryIconsExistInShell()
    {
        var enumLine = File.ReadAllLines(Path.Combine(Root(), "shell", "Kvatyrka.Shell", "Ui", "Icons.g.cs")).First(l => l.Contains("enum IconKind"));
        var kinds = Regex.Matches(enumLine, @"\b[A-Z]\w*\b").Select(m => m.Value).ToHashSet();
        Assert.All(Catalog.Categories, c => Assert.Contains(c.Icon, kinds));
    }

    // Що могло лежати в реєстрі до Кватирки: нічого, значення Windows, чуже значення, інший тип, порожній ключ.
    public enum Prior { Absent, WindowsValue, Foreign, OtherType, EmptyKey }

    private static void Seed(FakeRegistry reg, Tweak t, Prior prior)
    {
        foreach (var op in t.IsChoice ? new List<RegOp> { t.Choice!.Options[0].Op } : t.Set)
        {
            var (h, k, n) = (op.Path.Hive, op.Path.Key, op.Name);
            switch (prior)
            {
                case Prior.WindowsValue when op.OffValue is not null: reg.Set(h, k, n, op.Kind, op.OffValue); break;
                case Prior.Foreign: reg.Set(h, k, n, op.Kind, op.Kind == RegType.DWord ? 7 : "чуже"); break;
                case Prior.OtherType: reg.Set(h, k, n, RegType.QWord, 5L); break;
                case Prior.EmptyKey: reg.Set(h, k, "сусід", RegType.String, "лишається"); break;
            }
        }
        // Сусідній ключ, якого повернення не сміє торкнутися.
        reg.Set(Hive.User, @"Software\Classes\CLSID\{00000000-0000-0000-0000-000000000001}", "", RegType.String, "сусід");
    }

    private static string Off(Tweak t) => t.IsChoice ? t.Choice!.Windows : "off";

    private static string On(Tweak t) => t.IsChoice ? t.Choice!.Options.First(o => o.Id != t.Choice.Windows).Id : "on";

    // Увімкнути й вимкнути кожне покращення за будь-якого попереднього стану – реєстр має стати рівно таким, як був.
    [Theory]
    [InlineData(Prior.Absent)]
    [InlineData(Prior.WindowsValue)]
    [InlineData(Prior.Foreign)]
    [InlineData(Prior.OtherType)]
    [InlineData(Prior.EmptyKey)]
    public void EveryTweakRevertsExactly(Prior prior)
    {
        foreach (var t in Catalog.Tweaks)
        {
            var reg = new FakeRegistry();
            Seed(reg, t, prior);
            var before = reg.Snapshot();
            var e = new TweakEngine(Catalog, reg);
            Assert.True(e.Apply(t, On(t), Scope.All).Changed, t.Id);
            Assert.Equal(On(t), e.Current(t));
            Assert.Equal(On(t), e.Recorded(t));
            e.Apply(t, Off(t), Scope.All);
            Assert.Null(e.Recorded(t));
            Assert.Equal(before, StripKvatyrka(reg).Snapshot());
            Assert.Empty(e.Enforce(Scope.All));
        }
    }

    // Так, як працює вікно: спершу процес адміністратора (спільне), потім звичайний (особисте).
    [Theory]
    [InlineData(Prior.Absent)]
    [InlineData(Prior.Foreign)]
    public void EveryTweakRevertsExactlyThroughElevationSplit(Prior prior)
    {
        foreach (var t in Catalog.Tweaks)
        {
            var reg = new FakeRegistry();
            Seed(reg, t, prior);
            var before = reg.Snapshot();
            var e = new TweakEngine(Catalog, reg);
            foreach (var value in new[] { On(t), Off(t) })
            {
                if (e.NeedsMachine(t, value)) e.Apply(t, value, Scope.Machine);
                Assert.False(e.NeedsMachine(t, value), t.Id);
                e.Apply(t, value, Scope.User);
                Assert.Empty(e.Enforce(Scope.All));
            }
            Assert.Equal(before, StripKvatyrka(reg).Snapshot());
        }
    }

    // Службова гілка Кватирки не рахується: вибір і збережений стан живуть там законно.
    private static FakeRegistry StripKvatyrka(FakeRegistry reg)
    {
        foreach (var k in reg.Keys.Where(k => k.Contains(@"\Kvatyrka", StringComparison.OrdinalIgnoreCase)).ToList()) reg.Keys.Remove(k);
        foreach (var k in reg.Values.Keys.Where(k => k.Contains(@"\Kvatyrka", StringComparison.OrdinalIgnoreCase)).ToList()) reg.Values.Remove(k);
        foreach (var k in reg.Keys.Where(k => k.EndsWith(@"\Software", StringComparison.OrdinalIgnoreCase) || k.EndsWith(@"\SOFTWARE", StringComparison.OrdinalIgnoreCase)).ToList())
            if (!reg.Keys.Any(x => x.StartsWith(k + "\\", StringComparison.OrdinalIgnoreCase)) && !reg.Values.Keys.Any(v => v.StartsWith(k + "|", StringComparison.OrdinalIgnoreCase))) reg.Keys.Remove(k);
        return reg;
    }

    // Частину покращення вже ввімкнено кимось іншим: після «увімкнути – вимкнути» воно має бути вимкнене повністю.
    [Fact]
    public void PartialBeforeKvatyrkaRevertsFully()
    {
        foreach (var t in Catalog.Tweaks.Where(t => !t.IsChoice && t.Set.Count > 1))
        {
            var reg = new FakeRegistry();
            var first = t.Set[0];
            reg.Set(first.Path.Hive, first.Path.Key, first.Name, first.Kind, first.OnValue!);
            var e = new TweakEngine(Catalog, reg);
            Assert.Equal(TweakState.Partial, e.State(t));
            e.Apply(t, "on", Scope.All);
            e.Apply(t, "off", Scope.All);
            Assert.Equal(TweakState.Off, e.State(t));
        }
        var choice = Catalog["taskbar.search"];
        var r2 = new FakeRegistry();
        var e2 = new TweakEngine(Catalog, r2);
        r2.Set(Hive.User, @"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", RegType.DWord, 0);
        e2.Apply(choice, "icon", Scope.All);
        e2.Apply(choice, "box", Scope.All);
        Assert.Equal("box", e2.Selected(choice));
    }

    [Fact]
    public void AlreadyOnBeforeKvatyrkaRevertsToWindowsDefault()
    {
        var reg = new FakeRegistry();
        var e = new TweakEngine(Catalog, reg);
        var t = Catalog["explorer.extensions"];
        reg.Set(Hive.User, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", RegType.DWord, 0);
        e.Apply(t, "on", Scope.All);
        e.Apply(t, "off", Scope.All);
        Assert.Equal(1, reg.Get(Hive.User, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt"));
    }

    [Fact]
    public void RevertKeyRemovesOwnClassesKey()
    {
        var reg = new FakeRegistry();
        reg.Set(Hive.User, @"Software\Classes\CLSID\{00000000-0000-0000-0000-000000000001}", "", RegType.String, "сусід");
        var e = new TweakEngine(Catalog, reg);
        var t = Catalog["explorer.classicMenu"];
        e.Apply(t, "on", Scope.All);
        Assert.Contains(reg.Keys, k => k.Contains("{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("", reg.Get(Hive.User, @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", ""));
        e.Apply(t, "off", Scope.All);
        Assert.DoesNotContain(reg.Keys, k => k.Contains("{86ca1aa0", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("сусід", reg.Get(Hive.User, @"Software\Classes\CLSID\{00000000-0000-0000-0000-000000000001}", ""));
    }

    [Fact]
    public void MixedTweakIsRecordedForMachineAndSplitsByScope()
    {
        var reg = new FakeRegistry();
        var e = new TweakEngine(Catalog, reg);
        var t = Catalog["privacy.ads"];
        Assert.Equal(Hive.Machine, t.Owner);
        e.Apply(t, "on", Scope.User);
        Assert.Equal(TweakState.Partial, e.State(t));
        Assert.Null(e.Recorded(t));
        Assert.True(e.NeedsMachine(t, "on"));
        e.Apply(t, "on", Scope.Machine);
        Assert.Equal(TweakState.On, e.State(t));
        Assert.Equal("on", reg.Get(Hive.Machine, TweakEngine.MachineChoices, t.Id));
        Assert.False(e.NeedsMachine(t, "on"));
    }

    [Fact]
    public void UserTweakIsRecordedForUser()
    {
        var reg = new FakeRegistry();
        var e = new TweakEngine(Catalog, reg);
        var t = Catalog["start.localSearch"];
        Assert.Equal(Hive.User, t.Owner);
        Assert.False(e.NeedsMachine(t, "on"));
        e.Apply(t, "on", Scope.User);
        Assert.Equal("on", reg.Get(Hive.User, TweakEngine.UserChoices, t.Id));
    }

    [Fact]
    public void ChoiceSelectsAndRevertsToWindows()
    {
        var reg = new FakeRegistry();
        var e = new TweakEngine(Catalog, reg);
        var t = Catalog["taskbar.search"];
        Assert.Equal("box", e.Selected(t));
        e.Apply(t, "hidden", Scope.All);
        Assert.Equal(0, reg.Get(Hive.User, @"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode"));
        Assert.Equal(TweakState.On, e.State(t));
        e.Apply(t, "icon", Scope.All);
        Assert.Equal("icon", e.Recorded(t));
        e.Apply(t, "box", Scope.All);
        Assert.Null(reg.Get(Hive.User, @"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode"));
        Assert.Null(e.Recorded(t));
        reg.Set(Hive.User, @"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", RegType.DWord, 3);
        Assert.Null(e.Selected(t));
    }

    [Fact]
    public void EnforceRestoresOnlyChosenAndOnlyInScope()
    {
        var reg = new FakeRegistry();
        var e = new TweakEngine(Catalog, reg);
        var ads = Catalog["privacy.ads"];
        var search = Catalog["start.localSearch"];
        e.Apply(ads, "on", Scope.All);
        e.Apply(search, "on", Scope.All);
        // «Оновлення Windows» скинуло частину налаштувань.
        reg.DeleteValue(Hive.User, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled");
        reg.DeleteValue(Hive.Machine, @"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy");
        reg.DeleteValue(Hive.User, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled");
        Assert.Equal(2, e.Enforce(Scope.User).Count);
        Assert.Equal(TweakState.On, e.State(search));
        Assert.Equal(TweakState.Partial, e.State(ads));
        Assert.Single(e.Enforce(Scope.Machine));
        Assert.Equal(TweakState.On, e.State(ads));
        Assert.Empty(e.Enforce(Scope.All));
        // Не обране людиною охоронець не чіпає.
        Assert.Equal(TweakState.Off, e.State(Catalog["start.noRecentFiles"]));
    }

    [Fact]
    public void RunCommandsOnlyForMachineScopeAndRealChange()
    {
        var reg = new FakeRegistry();
        var e = new TweakEngine(Catalog, reg);
        var t = Catalog["system.noHibernation"];
        Assert.Empty(e.Apply(t, "on", Scope.User).Commands);
        Assert.Equal(new[] { "powercfg.exe /hibernate off" }, e.Apply(t, "on", Scope.Machine).Commands);
        Assert.Empty(e.Apply(t, "on", Scope.Machine).Commands);
        Assert.Equal(new[] { "powercfg.exe /hibernate on" }, e.Apply(t, "off", Scope.All).Commands);
    }

    [Fact]
    public void ImageRegistryMapsHivesAndSkipsClasses()
    {
        var inner = new FakeRegistry();
        var image = new ImageRegistry(inner);
        var e = new TweakEngine(Catalog, image);
        e.Apply(Catalog["taskbar.noWidgets"], "on", Scope.All);
        e.Apply(Catalog["system.noFastStartup"], "on", Scope.All);
        e.Apply(Catalog["start.localSearch"], "on", Scope.All);
        var gallery = e.Apply(Catalog["explorer.noGallery"], "on", Scope.All);
        Assert.Equal(0, inner.Get(Hive.Machine, @"KV_SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests"));
        Assert.Equal(0, inner.Get(Hive.Machine, @"KV_SYSTEM\ControlSet001\Control\Session Manager\Power", "HiberbootEnabled"));
        Assert.Equal(1, inner.Get(Hive.Machine, @"KV_USER\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions"));
        Assert.Equal("on", inner.Get(Hive.Machine, @"KV_USER\Software\Kvatyrka\Tweaks", "explorer.noGallery"));
        Assert.Equal("on", inner.Get(Hive.Machine, @"KV_SOFTWARE\Kvatyrka\Tweaks", "taskbar.noWidgets"));
        Assert.Single(gallery.Skipped);
        Assert.False(gallery.Changed);
        Assert.DoesNotContain(inner.Values.Keys, k => k.StartsWith("HKCU", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("universal")]
    [InlineData("gigant")]
    public void BuilderProfilesApplyToImage(string name)
    {
        var json = File.ReadAllText(Path.Combine(Root(), "builder", "profiles", name + ".json"));
        var profile = ImageProfile.Parse(json);
        var inner = new FakeRegistry();
        var (report, commands) = profile.Apply(Catalog, new ImageRegistry(inner));
        Assert.NotEmpty(report);
        Assert.Contains(report, l => l.StartsWith(Catalog["privacy.telemetry"].Name, StringComparison.Ordinal));
        // Ізоляція ядра без перевірки драйверів – лише в профілі під конкретне залізо.
        var hvci = inner.Get(Hive.Machine, @"KV_SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled");
        Assert.Equal(name == "gigant" ? 1 : null, hvci);
        if (name == "gigant")
        {
            Assert.Contains("powercfg.exe /hibernate off", commands);
            Assert.Equal("13", inner.Get(Hive.Machine, @"KV_USER\Control Panel\Mouse", "MouseSensitivity"));
            Assert.Equal(0, inner.Get(Hive.Machine, @"KV_USER\Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode"));
        }
        else Assert.Empty(commands);
    }

    [Fact]
    public void ProfileResolveRules()
    {
        var p = new ProfileTweaks { Recommended = true, Off = { "edge.clean" }, On = { "games.rawMouse" }, Choices = { ["taskbar.search"] = "box" } };
        var r = p.Resolve(Catalog);
        Assert.False(r.ContainsKey("edge.clean"));
        Assert.Equal("on", r["games.rawMouse"]);
        Assert.False(r.ContainsKey("taskbar.search"));
        Assert.Equal("on", r["start.noAds"]);
        Assert.Throws<CatalogException>(() => new ProfileTweaks { On = { "nope" } }.Resolve(Catalog));
        Assert.Throws<CatalogException>(() => new ProfileTweaks { On = { "taskbar.search" } }.Resolve(Catalog));
        Assert.Throws<CatalogException>(() => new ProfileTweaks { Choices = { ["taskbar.search"] = "nope" } }.Resolve(Catalog));
        Assert.Empty(new ProfileTweaks { Recommended = false }.Resolve(Catalog));
    }

    [Fact]
    public void ElevatedRequestAcceptsOnlyCatalog()
    {
        var req = new ElevatedRequest { Guardian = true };
        req.Tweaks.Add(("taskbar.noWidgets", "on"));
        req.Tweaks.Add(("taskbar.search", "icon"));
        req.Apps.Add("copilot");
        var back = ElevatedRequest.Parse(req.ToArgs(), Catalog);
        Assert.Equal(req.Tweaks, back.Tweaks);
        Assert.Equal(req.Apps, back.Apps);
        Assert.True(back.Guardian);
        Assert.Throws<CatalogException>(() => ElevatedRequest.Parse(new[] { "tweak:evil=on" }, Catalog));
        Assert.Throws<CatalogException>(() => ElevatedRequest.Parse(new[] { "tweak:taskbar.noWidgets=maybe" }, Catalog));
        Assert.Throws<CatalogException>(() => ElevatedRequest.Parse(new[] { "app:system32" }, Catalog));
        Assert.Throws<CatalogException>(() => ElevatedRequest.Parse(new[] { "reg:HKLM\\x" }, Catalog));
    }

    [Theory]
    [InlineData("Microsoft.Copilot", "Microsoft.Copilot", true)]
    [InlineData("king.com.*", "king.com.CandyCrushSaga", true)]
    [InlineData("*.Netflix", "4DF9E0F8.Netflix", true)]
    [InlineData("*.Netflix", "Netflix", false)]
    [InlineData("Disney.*", "Microsoft.Disney", false)]
    [InlineData("a*b*c", "aXbYc", true)]
    [InlineData("a*b*c", "acb", false)]
    public void WildcardMatches(string pattern, string text, bool expected) => Assert.Equal(expected, Wildcard.Match(pattern, text));

    [Fact]
    public void InsiderConflictIsDetected()
    {
        var reg = new FakeRegistry();
        var t = Catalog["privacy.telemetry"];
        Assert.Null(SystemFacts.Conflict(t, reg));
        reg.Set(Hive.Machine, @"SOFTWARE\Microsoft\WindowsSelfHost\Applicability", "BranchName", RegType.String, "Dev");
        Assert.NotNull(SystemFacts.Conflict(t, reg));
        Assert.Null(SystemFacts.Conflict(Catalog["start.noAds"], reg));
    }

    [Theory]
    [InlineData("""{"key":"HKCU\\X","name":"A","type":"dword","value":1}""", """{"key":"HKCU\\X","name":"A","type":"dword","value":0}""")]
    public void CatalogRejectsSharedValues(string op1, string op2)
    {
        var json = $$"""
        {"version":1,"categories":[{"id":"c","name":"C","icon":"Folder"}],
         "tweaks":[{"id":"a","category":"c","name":"A","description":"A.","default":true,"set":[{{op1}}]},
                   {"id":"b","category":"c","name":"B","description":"B.","default":true,"set":[{{op2}}]}]}
        """;
        Assert.Throws<CatalogException>(() => Catalog.Parse(json));
    }

    [Theory]
    [InlineData("""{"key":"HKCU\\Software\\Classes\\CLSID\\{x}","name":"A","type":"dword","value":1,"revertKey":"HKCU\\Software"}""")]
    [InlineData("""{"key":"HKLM\\SOFTWARE\\X","name":"A","type":"dword","value":1,"revertKey":"HKLM\\SOFTWARE\\X"}""")]
    [InlineData("""{"key":"HKCU\\X","name":"A","type":"dword","value":"1"}""")]
    [InlineData("""{"key":"HKEY_USERS\\X","name":"A","type":"dword","value":1}""")]
    [InlineData("""{"key":"HKCU\\X","name":"A","type":"qword","value":1}""")]
    [InlineData("""{"key":"HKCU\\Software\\Classes\\CLSID\\","name":"A","type":"dword","value":1,"revertKey":"HKCU\\Software\\Classes\\CLSID\\"}""")]
    [InlineData("""{"key":"HKCU\\Software\\Classes\\CLSID\\{x}","name":"A","type":"dword","value":1,"revertKey":"HKCU\\Software\\Classes\\CLSID\\{x}"}""")]
    [InlineData("""{"key":"HKCU\\Software\\Classes\\CLSID\\\\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}","name":"A","type":"dword","value":1,"revertKey":"HKCU\\Software\\Classes\\CLSID\\\\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}"}""")]
    public void CatalogRejectsUnsafeOps(string op)
    {
        var json = $$"""
        {"version":1,"categories":[{"id":"c","name":"C","icon":"Folder"}],
         "tweaks":[{"id":"a","category":"c","name":"A","description":"A.","default":true,"set":[{{op}}]}]}
        """;
        Assert.Throws<CatalogException>(() => Catalog.Parse(json));
    }
}

public class SafetyTests
{
    private static readonly Catalog Catalog = Catalog.Builtin();

    // Без цього Windows не працює чи не оновлюється – жоден шаблон програм не сміє їх зачепити.
    [Theory]
    [InlineData("Microsoft.WindowsStore")]
    [InlineData("Microsoft.DesktopAppInstaller")]
    [InlineData("Microsoft.SecHealthUI")]
    [InlineData("Microsoft.StorePurchaseApp")]
    [InlineData("Microsoft.VCLibs.140.00")]
    [InlineData("Microsoft.UI.Xaml.2.8")]
    [InlineData("Microsoft.WindowsAppRuntime.1.5")]
    [InlineData("Microsoft.MicrosoftEdge.Stable")]
    [InlineData("Microsoft.Windows.ShellExperienceHost")]
    [InlineData("Microsoft.Windows.StartMenuExperienceHost")]
    [InlineData("MicrosoftWindows.Client.CBS")]
    [InlineData("Microsoft.AAD.BrokerPlugin")]
    [InlineData("Microsoft.GamingServices")]
    [InlineData("Microsoft.XboxIdentityProvider")]
    [InlineData("Microsoft.WidgetsPlatformRuntime")]
    [InlineData("windows.immersivecontrolpanel")]
    [InlineData("Microsoft.Windows.Photos")]
    public void NoAppPatternMatchesEssentialPackages(string package) =>
        Assert.DoesNotContain(Catalog.Apps, a => a.Matches(package));

    [Fact]
    public void CatalogRejectsBroadAppPatternsAndUnsafeIds()
    {
        string Json(string apps, string id = "a") => $$"""
            {"version":1,"categories":[{"id":"c","name":"C","icon":"Folder"}],
             "tweaks":[{"id":"{{id}}","category":"c","name":"A","description":"A.","default":true,"set":[{"key":"HKCU\X","name":"A","type":"dword","value":1}]}],
             "apps":[{{apps}}]}
            """;
        Assert.Throws<CatalogException>(() => Catalog.Parse(Json("""{"id":"x","name":"X","packages":["*"]}""")));
        Assert.Throws<CatalogException>(() => Catalog.Parse(Json("""{"id":"x","name":"X","packages":["Mi*"]}""")));
        Assert.Throws<CatalogException>(() => Catalog.Parse(Json("""{"id":"x y","name":"X","packages":["Microsoft.BingNews"]}""")));
        Assert.Throws<CatalogException>(() => Catalog.Parse(Json("", "a\"b")));
    }

    [Fact]
    public void CatalogRejectsRevertKeyOverAnotherTweak()
    {
        const string json = """
            {"version":1,"categories":[{"id":"c","name":"C","icon":"Folder"}],
             "tweaks":[
               {"id":"a","category":"c","name":"A","description":"A.","default":true,"set":[{"key":"HKCU\Software\Classes\CLSID\{11111111-1111-1111-1111-111111111111}","name":"A","type":"dword","value":1,"revertKey":"HKCU\Software\Classes\CLSID\{11111111-1111-1111-1111-111111111111}"}]},
               {"id":"b","category":"c","name":"B","description":"B.","default":true,"set":[{"key":"HKCU\Software\Classes\CLSID\{11111111-1111-1111-1111-111111111111}\Sub","name":"B","type":"dword","value":1}]}]}
            """;
        Assert.Throws<CatalogException>(() => Catalog.Parse(json));
    }

    [Fact]
    public void NeedsMachineForOffWhileRecordExists()
    {
        var reg = new FakeRegistry();
        var e = new TweakEngine(Catalog, reg);
        var t = Catalog["taskbar.noWidgets"];
        e.Apply(t, "on", Scope.All);
        reg.DeleteValue(Hive.Machine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests");
        Assert.True(e.NeedsMachine(t, "off"));
        Assert.True(e.NeedsMachine(t, "on"));
    }

    [Fact]
    public void EnforceSkipsConflictingTweaks()
    {
        var reg = new FakeRegistry();
        var e = new TweakEngine(Catalog, reg);
        var t = Catalog["privacy.telemetry"];
        e.Apply(t, "on", Scope.All);
        reg.DeleteValue(Hive.Machine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry");
        reg.Set(Hive.Machine, @"SOFTWARE\Microsoft\WindowsSelfHost\Applicability", "EnablePreviewBuilds", RegType.DWord, 2);
        Assert.Empty(e.Enforce(Scope.All));
        Assert.Null(reg.Get(Hive.Machine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry"));
    }

    [Fact]
    public void GarbageRecordsAreIgnored()
    {
        var reg = new FakeRegistry();
        var e = new TweakEngine(Catalog, reg);
        reg.Set(Hive.User, TweakEngine.UserChoices, "start.localSearch", RegType.String, "maybe");
        reg.Set(Hive.User, TweakEngine.UserChoices, "start.noAds", RegType.DWord, 1);
        reg.Set(Hive.User, TweakEngine.UserChoices, "taskbar.search", RegType.String, "on");
        Assert.Empty(e.Enforce(Scope.All));
    }

    [Fact]
    public void ProfileRejectsUnknownFields()
    {
        Assert.Throws<CatalogException>(() => ImageProfile.Parse("""{"tweaks":{"recomended":false,"of":["edge.clean"]}}"""));
        Assert.Throws<CatalogException>(() => ImageProfile.Parse("""{"tweaks":{},"registry":[{"key":"HKCU\X","nam":"A","type":"dword","value":1}]}"""));
        Assert.Throws<CatalogException>(() => new ProfileTweaks { On = { "edge.clean" }, Off = { "edge.clean" } }.Resolve(Catalog));
        Assert.Throws<CatalogException>(() => new ProfileTweaks { Off = { "nope" } }.Resolve(Catalog));
    }

    // Профіль у образі: кожне вибране покращення справді в потрібному стані, а все написане – лише в гілках образу.
    [Theory]
    [InlineData("universal")]
    [InlineData("gigant")]
    public void ProfilesFullyApplyInsideImageOnly(string name)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var profile = ImageProfile.Parse(File.ReadAllText(Path.Combine(root, "builder", "profiles", name + ".json")));
        var inner = new FakeRegistry();
        var image = new ImageRegistry(inner);
        profile.Apply(Catalog, image);
        Assert.All(inner.Keys, k => Assert.StartsWith(@"HKLM\KV_", k));
        var e = new TweakEngine(Catalog, image);
        foreach (var (id, value) in profile.Tweaks.Resolve(Catalog))
        {
            var t = Catalog[id];
            Assert.Equal(value, e.Recorded(t));
            if (t.AllOps.Any(o => o.Path.Key.StartsWith(@"Software\Classes", StringComparison.OrdinalIgnoreCase))) continue;
            Assert.Equal(value, e.Current(t));
        }
    }

    // Аргументи процесу адміністратора з'єднуються пробілами – після розбору мають бути ті самі.
    [Fact]
    public void ElevatedArgsSurviveCommandLine()
    {
        var req = new ElevatedRequest { Guardian = false };
        foreach (var t in Catalog.Tweaks)
            foreach (var v in t.IsChoice ? t.Choice!.Options.Select(o => o.Id) : new[] { "on", "off" }) req.Tweaks.Add((t.Id, v));
        req.Apps.AddRange(Catalog.Apps.Select(a => a.Id));
        var line = string.Join(' ', req.ToArgs());
        var back = ElevatedRequest.Parse(line.Split(' '), Catalog);
        Assert.Equal(req.Tweaks, back.Tweaks);
        Assert.Equal(req.Apps, back.Apps);
        Assert.False(back.Guardian);
        foreach (var bad in new[] { "tweak:taskbar.noWidgets", "tweak:=on", "guardian:ON", "app:" })
            Assert.Throws<CatalogException>(() => ElevatedRequest.Parse(new[] { bad }, Catalog));
    }
}

public class DocsTests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string here = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", ".."));

    // README перелічує всі покращення, тож опис не відстає від каталогу.
    [Fact]
    public void ReadmeListsEveryTweak()
    {
        var readme = File.ReadAllText(Path.Combine(Root(), "README.md"));
        Assert.All(Catalog.Builtin().Tweaks, t => Assert.Contains(t.Name, readme));
    }
}
