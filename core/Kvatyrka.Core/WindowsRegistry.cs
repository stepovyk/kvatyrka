using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Kvatyrka.Core;

// Справжній реєстр Windows, 64-розрядний вигляд.
[SupportedOSPlatform("windows")]
public sealed class WindowsRegistry : IRegistry
{
    private static RegistryKey Root(Hive hive) =>
        RegistryKey.OpenBaseKey(hive == Hive.Machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64);

    public RegValue? GetValue(Hive hive, string key, string name)
    {
        using var root = Root(hive);
        using var k = root.OpenSubKey(key);
        var value = k?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null) return null;
        return k!.GetValueKind(name) switch
        {
            RegistryValueKind.DWord when value is int i => new RegValue(RegType.DWord, i),
            RegistryValueKind.String when value is string s => new RegValue(RegType.String, s),
            RegistryValueKind.ExpandString when value is string s => new RegValue(RegType.ExpandString, s),
            RegistryValueKind.QWord when value is long l => new RegValue(RegType.QWord, l),
            RegistryValueKind.Binary when value is byte[] b => new RegValue(RegType.Binary, b),
            RegistryValueKind.MultiString when value is string[] m => new RegValue(RegType.MultiString, m),
            _ => null,
        };
    }

    public bool KeyExists(Hive hive, string key)
    {
        using var root = Root(hive);
        using var k = root.OpenSubKey(key);
        return k is not null;
    }

    public bool IsEmptyKey(Hive hive, string key)
    {
        using var root = Root(hive);
        using var k = root.OpenSubKey(key);
        return k is not null && k.ValueCount == 0 && k.SubKeyCount == 0;
    }

    public void Set(Hive hive, string key, string name, RegType type, object value)
    {
        using var root = Root(hive);
        using var k = root.CreateSubKey(key, true);
        k.SetValue(name, value, type switch
        {
            RegType.DWord => RegistryValueKind.DWord,
            RegType.ExpandString => RegistryValueKind.ExpandString,
            RegType.QWord => RegistryValueKind.QWord,
            RegType.Binary => RegistryValueKind.Binary,
            RegType.MultiString => RegistryValueKind.MultiString,
            _ => RegistryValueKind.String,
        });
    }

    public void DeleteValue(Hive hive, string key, string name)
    {
        using var root = Root(hive);
        using var k = root.OpenSubKey(key, true);
        k?.DeleteValue(name, false);
    }

    public void DeleteKey(Hive hive, string key)
    {
        // Порожній чи кореневий шлях не видаляється ніколи.
        if (string.IsNullOrWhiteSpace(key.Trim('\\'))) throw new ArgumentException("Порожній ключ");
        using var root = Root(hive);
        root.DeleteSubKeyTree(key.TrimEnd('\\'), false);
    }
}
