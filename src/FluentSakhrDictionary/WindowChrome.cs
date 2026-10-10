using System.Runtime.InteropServices;

namespace FluentSakhrDictionary;

/// <summary>Removes the thin light border Windows 11 draws around borderless WinUI windows.</summary>
static class WindowChrome
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);

    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr parameter);
    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);

    const long WS_EX_NOINHERITLAYOUT = 0x00100000L;
    const int GWL_EXSTYLE = -20;
    const long WS_EX_LAYOUTRTL = 0x00400000L;

    /// <summary>Mirrors the window frame for Arabic so the caption buttons move to the left.
    /// XAML owns content direction. Native mirroring must never reach child render hosts:
    /// it reflects their rendered glyphs, even when TextBlock.FlowDirection is LTR.</summary>
    public static void SetRtlMirror(IntPtr hwnd, bool rtl)
    {
        try
        {
            long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            ex |= WS_EX_NOINHERITLAYOUT; // also protects lazily created ListView render hosts
            ex = rtl ? (ex | WS_EX_LAYOUTRTL) : (ex & ~WS_EX_LAYOUTRTL);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
            // Remove stale native mirroring from already-created child hosts (language switches).
            EnumChildWindows(hwnd, (child, _) =>
            {
                long childEx = GetWindowLongPtr(child, GWL_EXSTYLE).ToInt64();
                SetWindowLongPtr(child, GWL_EXSTYLE, new IntPtr((childEx | WS_EX_NOINHERITLAYOUT) & ~WS_EX_LAYOUTRTL));
                return true;
            }, IntPtr.Zero);
        }
        catch { }
    }

    public static void Apply(IntPtr hwnd)
    {
        try
        {
            int none = unchecked((int)0xFFFFFFFE), round = 2;
            DwmSetWindowAttribute(hwnd, 34, ref none, 4);   // DWMWA_BORDER_COLOR = none
            DwmSetWindowAttribute(hwnd, 33, ref round, 4);  // DWMWA_WINDOW_CORNER_PREFERENCE = round
        }
        catch { }
    }
}
