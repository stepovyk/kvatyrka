using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kvatyrka.Shell.Native;

// Робота з об'єктами оболонки Windows: список усіх програм, їхні іконки, AUMID вікон.
internal static class ShellItems
{
    private static readonly Guid IID_IShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    private static readonly Guid BHID_EnumItems = new("94f60519-2850-4924-aa5a-d15e84868039");
    private static readonly Guid IID_IEnumShellItems = new("70629033-e363-4a28-a567-0db78006e6d7");
    private static readonly Guid IID_IPropertyStore = new("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");
    private static readonly PROPERTYKEY PKEY_AppUserModel_ID = new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    // PARENTRELATIVEPARSING дає AUMID або «{FOLDERID}\…\app.exe» – саме те, що розуміє «shell:AppsFolder\…».
    public enum SIGDN : uint { NORMALDISPLAY = 0, PARENTRELATIVEPARSING = 0x80018001 }

    [Flags]
    private enum SIIGBF { ResizeToFit = 0, BiggerSizeOk = 1, IconOnly = 4 }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY { public Guid fmtid; public uint pid; public PROPERTYKEY(Guid g, uint p) { fmtid = g; pid = p; } }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT { public ushort vt; ushort r1, r2, r3; public IntPtr p; public IntPtr p2; }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetParent(out IShellItem ppsi);
        [PreserveSig] int GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
        [PreserveSig] int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        [PreserveSig] int Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, Guid("70629033-e363-4a28-a567-0db78006e6d7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumShellItems
    {
        [PreserveSig] int Next(uint celt, [MarshalAs(UnmanagedType.Interface)] out IShellItem rgelt, out uint fetched);
        [PreserveSig] int Skip(uint celt);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumShellItems ppenum);
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint c);
        [PreserveSig] int GetAt(uint i, out PROPERTYKEY key);
        [PreserveSig] int GetValue([In] ref PROPERTYKEY key, out PROPVARIANT pv);
        [PreserveSig] int SetValue([In] ref PROPERTYKEY key, [In] ref PROPVARIANT pv);
        [PreserveSig] int Commit();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHGetPropertyStoreFromParsingName(string path, IntPtr pbc, int flags, [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PROPVARIANT pv);
    [DllImport("ole32.dll")] private static extern void CoTaskMemFree(IntPtr p);

    public sealed record Entry(string Name, string ParsingName);

    // Усі програми з «shell:AppsFolder» – той самий список, що й у меню «Пуск»: звичайні й зі Store.
    public static List<Entry> EnumerateApps()
    {
        var result = new List<Entry>();
        var iid = IID_IShellItem;
        if (SHCreateItemFromParsingName("shell:AppsFolder", IntPtr.Zero, ref iid, out var folderObj) != 0) return result;
        var folder = (IShellItem)folderObj;
        var bhid = BHID_EnumItems; var eiid = IID_IEnumShellItems;
        if (folder.BindToHandler(IntPtr.Zero, ref bhid, ref eiid, out var enumPtr) != 0) return result;
        var en = (IEnumShellItems)Marshal.GetObjectForIUnknown(enumPtr);
        Marshal.Release(enumPtr);
        while (en.Next(1, out var item, out var fetched) == 0 && fetched == 1)
        {
            var name = DisplayName(item, SIGDN.NORMALDISPLAY);
            var parsing = DisplayName(item, SIGDN.PARENTRELATIVEPARSING);
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(parsing)) result.Add(new Entry(name, parsing));
            Marshal.ReleaseComObject(item);
        }
        Marshal.ReleaseComObject(en);
        Marshal.ReleaseComObject(folder);
        return result;
    }

    private static string DisplayName(IShellItem item, SIGDN kind)
    {
        if (item.GetDisplayName(kind, out var p) != 0 || p == IntPtr.Zero) return string.Empty;
        try { return Marshal.PtrToStringUni(p) ?? string.Empty; } finally { CoTaskMemFree(p); }
    }

    // Справжня іконка програми або файла заданого розміру (у фізичних пікселях).
    public static BitmapSource? Icon(string parsingName, int px)
    {
        var iid = IID_IShellItem;
        if (SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out var obj) != 0) return null;
        try
        {
            if (obj is not IShellItemImageFactory factory) return null;
            if (factory.GetImage(new SIZE { cx = px, cy = px }, SIIGBF.IconOnly | SIIGBF.BiggerSizeOk, out var hbm) != 0 || hbm == IntPtr.Zero) return null;
            try { return FromHBitmap(hbm); } finally { Win32.DeleteObject(hbm); }
        }
        finally { Marshal.ReleaseComObject(obj); }
    }

    // HBITMAP від оболонки – 32 біти з премножною альфою, рядки знизу догори не гарантовано: копіюємо як є.
    private static BitmapSource? FromHBitmap(IntPtr hbm)
    {
        if (Win32.GetObject(hbm, Marshal.SizeOf<Win32.BITMAP>(), out var bm) == 0 || bm.bmBits == IntPtr.Zero || bm.bmBitsPixel != 32) return null;
        var stride = bm.bmWidthBytes;
        var h = Math.Abs(bm.bmHeight);
        var pixels = new byte[stride * h];
        Marshal.Copy(bm.bmBits, pixels, 0, pixels.Length);
        if (bm.bmHeight > 0)
        {
            var flipped = new byte[pixels.Length];
            for (var y = 0; y < h; y++) Buffer.BlockCopy(pixels, y * stride, flipped, (h - 1 - y) * stride, stride);
            pixels = flipped;
        }
        var src = BitmapSource.Create(bm.bmWidth, h, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        src.Freeze();
        return src;
    }

    private static readonly PROPERTYKEY PKEY_Link_TargetParsingPath = new(new Guid("B9B4B3FC-2B51-4A42-B5D8-324146AFCF25"), 2);

    // Шлях, на який веде ярлик програми з «shell:AppsFolder» (для Chrome, Discord тощо, де замість шляху – AUMID ярлика).
    public static string? LinkTarget(string parsingName)
    {
        var iid = IID_IPropertyStore;
        if (SHGetPropertyStoreFromParsingName($"shell:AppsFolder\\{parsingName}", IntPtr.Zero, 0, ref iid, out var obj) != 0) return null;
        try
        {
            var store = (IPropertyStore)obj;
            var key = PKEY_Link_TargetParsingPath;
            if (store.GetValue(ref key, out var pv) != 0) return null;
            try { return pv.vt == 31 && pv.p != IntPtr.Zero ? Marshal.PtrToStringUni(pv.p) : null; }
            finally { PropVariantClear(ref pv); }
        }
        catch (COMException) { return null; }
        finally { Marshal.ReleaseComObject(obj); }
    }

    public static string? WindowAumid(IntPtr hWnd)
    {
        var iid = IID_IPropertyStore;
        if (SHGetPropertyStoreForWindow(hWnd, ref iid, out var obj) != 0) return null;
        try
        {
            var store = (IPropertyStore)obj;
            var key = PKEY_AppUserModel_ID;
            if (store.GetValue(ref key, out var pv) != 0) return null;
            try { return pv.vt == 31 && pv.p != IntPtr.Zero ? Marshal.PtrToStringUni(pv.p) : null; }
            finally { PropVariantClear(ref pv); }
        }
        finally { Marshal.ReleaseComObject(obj); }
    }

    public static Size Dpi(Visual v)
    {
        var d = VisualTreeHelper.GetDpi(v);
        return new Size(d.DpiScaleX, d.DpiScaleY);
    }
}
