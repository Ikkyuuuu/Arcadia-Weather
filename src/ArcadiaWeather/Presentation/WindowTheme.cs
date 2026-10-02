using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace ArcadiaWeather.Presentation;

internal static class WindowTheme
{
    internal static bool IsDark { get; private set; }
    // Match native window surfaces to the palette; WindowChrome supplies the navbar caption.
    internal static void Apply(Window window, string preference = "system")
    {
        bool light = preference == "light";
        if (preference == "system")
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                light = key?.GetValue("AppsUseLightTheme") is int value && value == 1;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException) { }
        }
        IsDark = !light;
        var colors = new Dictionary<string, string>
        {
            ["BackgroundBrush"] = light ? "#F3F5F5" : "#0E1315",
            ["PanelBrush"] = light ? "#FFFFFF" : "#151C1F",
            ["InputBrush"] = light ? "#EEF2F2" : "#1B2427",
            ["LineBrush"] = light ? "#DAE0E2" : "#293236",
            ["TextBrush"] = light ? "#0F1A1D" : "#EEF3F4",
            ["MutedBrush"] = light ? "#3F4F54" : "#A7B4B8",
            ["SubtleBrush"] = light ? "#56666B" : "#82939A",
            ["AccentBrush"] = light ? "#127489" : "#36BBD9",
            ["OnAccentBrush"] = light ? "#FFFFFF" : "#06272F",
            ["MintBrush"] = light ? "#127489" : "#36BBD9",
            ["HoldBrush"] = light ? "#8A5A0C" : "#E9B45C",
            ["HoldSoftBrush"] = light ? "#F2E9D8" : "#25241E"
        };
        foreach (var (key, color) in colors)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze(); System.Windows.Application.Current.Resources[key] = brush;
        }
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        nint handle = new WindowInteropHelper(window).Handle;
        if (handle == 0) return;
        int dark = light ? 0 : 1, caption = light ? 0x00F5F5F3 : 0x0015130E, text = light ? 0x001D1A0F : 0x00F4F3EE;
        _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
