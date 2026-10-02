using System.Text.Json;
using Microsoft.Win32;

namespace ArcadiaWeather.Infrastructure;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string DirectoryPath { get; }
    public string? Warning { get; private set; }
    public SettingsStore(string? directory = null)
    {
        DirectoryPath = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcadiaWeather");
        Directory.CreateDirectory(DirectoryPath);
    }
    public AppSettings LoadSettings()
    {
        var settings = Read<AppSettings>("settings.json") ?? new AppSettings();
        try { settings.Validate(); }
        catch (Exception ex) { Log(ex); Warning = "Saved settings were invalid. Defaults have been restored."; settings = new(); }
        return settings;
    }
    public WeatherSnapshot? LoadWeather() => Read<WeatherSnapshot>("weather-cache.json");
    public void SaveSettings(AppSettings settings) => Write("settings.json", settings);
    public void SaveWeather(WeatherSnapshot weather) => Write("weather-cache.json", weather);
    private T? Read<T>(string file)
    {
        var path = Path.Combine(DirectoryPath, file);
        if (!File.Exists(path)) return default;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path)); }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log(ex); Warning = $"Could not read {file}; using defaults.";
            try { File.Copy(path, path + $".invalid-{DateTime.Now:yyyyMMddHHmmss}", true); } catch (IOException) { }
            return default;
        }
    }
    private void Write<T>(string name, T value)
    {
        string path = Path.Combine(DirectoryPath, name), temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonOptions));
        File.Move(temporary, path, true);
    }
    public void Log(Exception ex) => Log(ex.ToString());
    public void Log(string message)
    {
        try
        {
            var path = Path.Combine(DirectoryPath, "app.log");
            if (File.Exists(path) && new FileInfo(path).Length > 2_000_000) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

public static class StartupManager
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static void Apply(bool enabled)
    {
        using var read = Registry.CurrentUser.OpenSubKey(Key);
        string? existing = read?.GetValue("ArcadiaWeather") as string;
        string command = $"\"{Environment.ProcessPath}\" --tray";
        if ((!enabled && existing is null) || (enabled && existing == command)) return;
        using var key = Registry.CurrentUser.CreateSubKey(Key, true);
        if (enabled) key.SetValue("ArcadiaWeather", command);
        else key.DeleteValue("ArcadiaWeather", false);
    }
}
