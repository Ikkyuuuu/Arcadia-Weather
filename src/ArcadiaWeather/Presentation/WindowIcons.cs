using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Drawing = System.Drawing;

namespace ArcadiaWeather.Presentation;

/// <summary>Owns the native icons for the lifetime of the main window.</summary>
internal sealed class WindowIcons : IDisposable
{
    private readonly Drawing.Icon _small = Load(32);
    private readonly Drawing.Icon _large = Load(64);

    internal static void IdentifyProcess() =>
        Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID("ArcadiaWeather.Desktop"));

    internal static Drawing.Icon Load(int size)
    {
        using var stream = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/ArcadiaWeather;component/Assets/arcadia.ico")).Stream;
        using var icon = new Drawing.Icon(stream, size, size);
        return (Drawing.Icon)icon.Clone();
    }

    internal void Apply(Window window)
    {
        nint handle = new WindowInteropHelper(window).Handle;
        if (handle == 0) return;
        // Use both native slots: the taskbar/Alt+Tab use the large icon, while
        // Windows can request the small icon independently of WPF's title bar.
        _ = SendMessage(handle, 0x0080, 0, _small.Handle); // WM_SETICON, ICON_SMALL
        _ = SendMessage(handle, 0x0080, 1, _large.Handle); // WM_SETICON, ICON_BIG
    }

    public void Dispose() { _small.Dispose(); _large.Dispose(); }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(nint window, uint message, nint parameter, nint value);
}
