using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FluentSakhrDictionary;

/// <summary>Captures this window to a PNG without a visible desktop (CI screenshots).</summary>
static class CaptureHelper
{
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

    public static void Save(IntPtr hwnd, string path)
    {
        GetWindowRect(hwnd, out var r);
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
        IntPtr bmp = CreateCompatibleBitmap(IntPtr.Zero, w, h);
        IntPtr old = SelectObject(dc, bmp);
        PrintWindow(hwnd, dc, 2); // PW_RENDERFULLCONTENT
        using var image = Image.FromHbitmap(bmp);
        image.Save(path, ImageFormat.Png);
        SelectObject(dc, old);
        DeleteObject(bmp);
        DeleteDC(dc);
    }
}
