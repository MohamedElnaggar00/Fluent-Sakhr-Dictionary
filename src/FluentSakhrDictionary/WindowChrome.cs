using System.Runtime.InteropServices;

namespace FluentSakhrDictionary;

/// <summary>Removes the thin light border Windows 11 draws around borderless WinUI windows.</summary>
static class WindowChrome
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

    const int GWL_STYLE = -16;
    const long WS_CAPTION = 0x00C00000L; // includes WS_BORDER | WS_DLGFRAME - the residual frame that renders as light dots
    const uint SWP_NOMOVE = 2, SWP_NOSIZE = 1, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20;

    const int GWL_EXSTYLE = -20;
    const long WS_EX_LAYOUTRTL = 0x00400000L;

    /// <summary>Mirrors the window frame for Arabic so the caption buttons move to the left.
    /// XAML content mirrors via FlowDirection; this covers the DWM-drawn min/max/close buttons.</summary>
    public static void SetRtlMirror(IntPtr hwnd, bool rtl)
    {
        try
        {
            long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            ex = rtl ? (ex | WS_EX_LAYOUTRTL) : (ex & ~WS_EX_LAYOUTRTL);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
        }
        catch { }
    }

    public static void Apply(IntPtr hwnd)
    {
        try
        {
            // Strip the caption/border styles (keep WS_THICKFRAME so the window stays resizable);
            // the leftover border styles are what render as white dots along the top edge.
            long st = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
            st &= ~WS_CAPTION;
            SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(st));
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }
        catch { }
        try
        {
            int none = unchecked((int)0xFFFFFFFE), round = 2;
            DwmSetWindowAttribute(hwnd, 34, ref none, 4);   // DWMWA_BORDER_COLOR = none
            DwmSetWindowAttribute(hwnd, 33, ref round, 4);  // DWMWA_WINDOW_CORNER_PREFERENCE = round
        }
        catch { }
    }
}
