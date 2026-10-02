using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace ArcadiaWeather.Presentation;

internal static class WindowFrame
{
    internal static void KeepContentInWorkArea(Window window, FrameworkElement content)
    {
        var inset = new Thickness();
        nint handle = new WindowInteropHelper(window).Handle;
        if (window.WindowState == WindowState.Maximized && handle != 0 && GetWindowRect(handle, out var bounds))
        {
            // Windows maximizes the invisible resize frame beyond the work area.
            // Keep the navbar and controls inside the visible area on this monitor,
            // including mixed-DPI displays and taskbars on any screen edge.
            var work = Forms.Screen.FromHandle(handle).WorkingArea;
            var dpi = VisualTreeHelper.GetDpi(window);
            inset = new Thickness(
                Math.Max(0, work.Left - bounds.Left) / dpi.DpiScaleX,
                Math.Max(0, work.Top - bounds.Top) / dpi.DpiScaleY,
                Math.Max(0, bounds.Right - work.Right) / dpi.DpiScaleX,
                Math.Max(0, bounds.Bottom - work.Bottom) / dpi.DpiScaleY);
        }
        content.Margin = inset;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out WindowRect rect);
}
