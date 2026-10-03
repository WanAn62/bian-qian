using System.Runtime.InteropServices;

namespace Notelet.Services;

internal static class Dwm
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    // kind: 2 = Mica(云母), 3 = Acrylic(亚克力)
    public static bool TryBackdrop(IntPtr hwnd, int kind, bool dark)
    {
        try
        {
            int v = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, 4);
            int pref = 2; // round
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, 4);
            int k = kind;
            return DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref k, 4) == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void DisableBackdrop(IntPtr hwnd)
    {
        try { int k = 1; DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref k, 4); } catch { }
    }

    public static void RoundCorners(IntPtr hwnd)
    {
        try { int p = 2; DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref p, 4); } catch { }
    }
}
