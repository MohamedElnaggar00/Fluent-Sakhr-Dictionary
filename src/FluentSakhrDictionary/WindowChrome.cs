using System.Runtime.InteropServices;

namespace FluentSakhrDictionary;

/// <summary>Removes the thin light border Windows 11 draws around borderless WinUI windows.</summary>
static class WindowChrome
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);

    const int GWL_EXSTYLE = -20;
    const long WS_EX_LAYOUTRTL = 0x00400000L;

    /// <summary>XAML mirrors layout; native bitmap mirroring reverses rendered glyphs.</summary>
    public static void ClearNativeMirror(IntPtr hwnd)
    {
        try
        {
            long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex & ~WS_EX_LAYOUTRTL));
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
