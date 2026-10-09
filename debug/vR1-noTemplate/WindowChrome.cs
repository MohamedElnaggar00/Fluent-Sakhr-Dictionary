using System.Runtime.InteropServices;

namespace FluentSakhrDictionary;

/// <summary>Removes the thin light border Windows 11 draws around borderless WinUI windows.</summary>
static class WindowChrome
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

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
