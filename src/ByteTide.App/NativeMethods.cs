using System.Runtime.InteropServices;

namespace ByteTide.App;

/// <summary>
/// Small Win32 interop surface: click-through windows (so the graph can sit
/// on top of other apps without blocking clicks) and proper icon handle
/// cleanup for the live-updating tray icon.
/// </summary>
internal static class NativeMethods
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const uint LWA_ALPHA = 0x2;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr handle);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Gives the window a dark title bar on Windows 10 (20H1+) / 11. Silently does nothing elsewhere.</summary>
    public static void UseDarkTitleBar(IntPtr handle, bool dark)
    {
        try
        {
            int value = dark ? 1 : 0;
            // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (19 on early Windows 10 builds)
            if (DwmSetWindowAttribute(handle, 20, ref value, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, 19, ref value, sizeof(int));
        }
        catch
        {
            // Cosmetic only.
        }
    }

    private static IntPtr GetWindowExStyle(IntPtr hWnd) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, GWL_EXSTYLE) : new IntPtr(GetWindowLong32(hWnd, GWL_EXSTYLE));

    private static void SetWindowExStyle(IntPtr hWnd, IntPtr value)
    {
        if (IntPtr.Size == 8) SetWindowLongPtr64(hWnd, GWL_EXSTYLE, value);
        else SetWindowLong32(hWnd, GWL_EXSTYLE, value.ToInt32());
    }

    /// <summary>
    /// Enables/disables click-through on a window. When enabling, also
    /// explicitly sets the layered-window alpha so the window stays visible
    /// even if it wasn't already layered via Form.Opacity.
    /// </summary>
    public static void SetClickThrough(IntPtr handle, bool enabled, byte alpha)
    {
        int exStyle = GetWindowExStyle(handle).ToInt32();

        if (enabled)
        {
            exStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT;
            SetWindowExStyle(handle, new IntPtr(exStyle));
            SetLayeredWindowAttributes(handle, 0, alpha, LWA_ALPHA);
        }
        else
        {
            exStyle &= ~WS_EX_TRANSPARENT;
            SetWindowExStyle(handle, new IntPtr(exStyle));
        }
    }
}
