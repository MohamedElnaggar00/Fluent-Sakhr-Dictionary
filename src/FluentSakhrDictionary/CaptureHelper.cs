using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FluentSakhrDictionary;

/// <summary>Captures this window to a PNG without a visible desktop (CI screenshots).</summary>
static class CaptureHelper
{
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

    public static void Save(IntPtr hwnd, string path)
    {
        if (hwnd == IntPtr.Zero) throw new InvalidOperationException("window handle is zero");
        if (!GetWindowRect(hwnd, out var r)) throw new InvalidOperationException("GetWindowRect failed");
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        if (w <= 0 || h <= 0) throw new InvalidOperationException("window rect is empty: " + w + "x" + h);
        IntPtr screen = GetDC(IntPtr.Zero);
        if (screen == IntPtr.Zero) throw new InvalidOperationException("GetDC failed");
        IntPtr dc = CreateCompatibleDC(screen);
        if (dc == IntPtr.Zero) { ReleaseDC(IntPtr.Zero, screen); throw new InvalidOperationException("CreateCompatibleDC failed"); }
        IntPtr bmp = CreateCompatibleBitmap(screen, w, h);
        if (bmp == IntPtr.Zero) { DeleteDC(dc); ReleaseDC(IntPtr.Zero, screen); throw new InvalidOperationException("CreateCompatibleBitmap failed"); }
        IntPtr old = SelectObject(dc, bmp);
        bool printed = PrintWindow(hwnd, dc, 2); // PW_RENDERFULLCONTENT
        if (!printed) printed = PrintWindow(hwnd, dc, 0);
        if (!printed) { SelectObject(dc, old); DeleteObject(bmp); DeleteDC(dc); ReleaseDC(IntPtr.Zero, screen); throw new InvalidOperationException("PrintWindow failed"); }
        using (var image = Image.FromHbitmap(bmp))
            image.Save(path, ImageFormat.Png);
        SelectObject(dc, old);
        DeleteObject(bmp);
        DeleteDC(dc);
        ReleaseDC(IntPtr.Zero, screen);
    }
}
