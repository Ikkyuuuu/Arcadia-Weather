using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ArcadiaWeather.Playback;

internal static class DesktopHost
{
    private const int Style = -16, ExStyle = -20;
    private const long Child = 0x40000000, Popup = 0x80000000;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint FindWindow(string? className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint FindWindowEx(nint parent, nint after, string? className, string? title);
    private delegate bool EnumWindowsCallback(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetParent(nint child, nint parent);
    [DllImport("user32.dll")] internal static extern nint GetParent(nint child);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern nint SendMessageTimeout(nint window, uint message, nuint wParam, nint lParam, uint flags, uint timeout, out nuint result);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(nint window, ref Point point);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int count);
    [DllImport("user32.dll")] private static extern bool RedrawWindow(nint window, nint rect, nint region, uint flags);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }

    internal static nint Attach(nint window, Rectangle bounds)
    {
        var progman = FindWindow("Progman", null);
        if (progman == 0) throw new InvalidOperationException("Windows desktop is not ready. Try Resume in a moment.");
        SendMessageTimeout(progman, 0x052C, 0xD, 1, 2, 1000, out _);
        var shell = FindWindowEx(progman, 0, "SHELLDLL_DefView", null);
        var childWorker = FindWindowEx(progman, 0, "WorkerW", null);
        // Windows 11's raised desktop: render between the icon layer and wallpaper layer.
        bool raised = shell != 0 && childWorker != 0;
        nint host = raised ? progman : 0;
        if (!raised)
        {
            SendMessageTimeout(progman, 0x052C, 0, 0, 2, 1000, out _);
            EnumWindows((top, _) =>
            {
                if (FindWindowEx(top, 0, "SHELLDLL_DefView", null) != 0)
                {
                    var candidate = FindWindowEx(0, top, "WorkerW", null);
                    if (candidate != 0) { host = candidate; return false; }
                }
                return true;
            }, 0);
        }
        if (host == 0) throw new InvalidOperationException("Could not locate the Windows wallpaper layer. Restart Explorer, then try Resume.");

        long style = GetWindowLongPtr(window, Style).ToInt64();
        SetWindowLongPtr(window, Style, (nint)((style | Child) & ~Popup));
        long extended = GetWindowLongPtr(window, ExStyle).ToInt64();
        SetWindowLongPtr(window, ExStyle, (nint)(extended | 0x08000000L | 0x80L)); // no activate, tool window
        Marshal.SetLastPInvokeError(0);
        var oldParent = SetParent(window, host);
        if (oldParent == 0 && Marshal.GetLastPInvokeError() != 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        var origin = new Point(bounds.Left, bounds.Top);
        ScreenToClient(host, ref origin);
        if (!SetWindowPos(window, raised ? shell : 0, origin.X, origin.Y, bounds.Width, bounds.Height, 0x0010 | 0x0020 | 0x0040))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (raised) RedrawWindow(shell, 0, 0, 0x0001 | 0x0080 | 0x0100);
        return host;
    }

    internal static bool HasFullScreenApp(Screen target)
    {
        var foreground = GetForegroundWindow();
        if (foreground == 0) return false;
        GetWindowThreadProcessId(foreground, out uint process);
        if (process == Environment.ProcessId) return false;
        var name = new StringBuilder(128);
        GetClassName(foreground, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        if (!GetWindowRect(foreground, out var rect)) return false;
        var b = target.Bounds;
        return rect.Left <= b.Left && rect.Top <= b.Top && rect.Right >= b.Right && rect.Bottom >= b.Bottom;
    }
}

internal sealed class WallpaperWindow : Form
{
    protected override bool ShowWithoutActivation => true;
    public WallpaperWindow()
    {
        Text = "ArcadiaWeather wallpaper";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        BackColor = Color.Black;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
    }
    protected override CreateParams CreateParams
    {
        get { var value = base.CreateParams; value.ExStyle |= 0x08000080; return value; }
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x21) { message.Result = 3; return; } // MA_NOACTIVATE
        if (message.Msg == 0x84) { message.Result = -1; return; } // HTTRANSPARENT
        base.WndProc(ref message);
    }
}
