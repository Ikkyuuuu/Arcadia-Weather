using System.IO;
using System.Windows.Threading;
using ArcadiaWeather;
using ArcadiaWeather.Core;
using ArcadiaWeather.Infrastructure;

// No Application, Window, rendering, UI automation, weather fetch, or GPS request.
// Controller playback is disabled and no dispatcher message loop is started.
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/theme-preference-tests");
        Directory.CreateDirectory(root);
        int passed = 0, failed = 0;
        void Test(string name, Action<SettingsStore> action)
        {
            try
            {
                var store = new SettingsStore(Path.Combine(root, Guid.NewGuid().ToString("N")));
                action(store);
                Console.WriteLine($"PASS {name}"); passed++;
            }
            catch (Exception ex) { Console.WriteLine($"FAIL {name}: {ex.Message}"); failed++; }
        }
        static AppController Controller(SettingsStore store) => new(store, Dispatcher.CurrentDispatcher, disablePlayback: true);
        static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
        }

        Test("Dark mode is written immediately and restored by a new app controller", store =>
        {
            using (var app = Controller(store)) { app.SetTheme("dark"); Equal("dark", store.LoadSettings().AppTheme); }
            using var restarted = Controller(new SettingsStore(store.DirectoryPath));
            Equal("dark", restarted.Settings.AppTheme);
        });
        Test("Switching back to light mode is also permanent", store =>
        {
            using (var app = Controller(store)) { app.SetTheme("dark"); app.SetTheme("light"); }
            using var restarted = Controller(new SettingsStore(store.DirectoryPath));
            Equal("light", restarted.Settings.AppTheme);
        });
        Test("System preference is saved when explicitly selected", store =>
        {
            using var app = Controller(store); app.SetTheme("dark"); app.SetTheme("system");
            Equal("system", new SettingsStore(store.DirectoryPath).LoadSettings().AppTheme);
        });
        Test("Theme changes preserve other preferences and do not refresh weather", store =>
        {
            store.SaveSettings(new AppSettings { LocationName = "Bang Rak", Volume = 63, Muted = false, TemperatureUnit = "F", WallpaperFit = "fit" });
            using var app = Controller(store);
            var refreshBefore = app.NextRefresh;
            var draft = app.Settings.Copy(); draft.LocationName = "Unsaved draft";
            app.SetTheme("dark");
            var saved = store.LoadSettings();
            Equal("Bang Rak", saved.LocationName); Equal(63, saved.Volume); Equal(false, saved.Muted);
            Equal("F", saved.TemperatureUnit); Equal("fit", saved.WallpaperFit);
            Equal("Unsaved draft", draft.LocationName);
            Equal(refreshBefore, app.NextRefresh); Equal(false, app.IsFetching);
            Equal(1, Directory.GetFiles(store.DirectoryPath).Length);
        });
        Test("Invalid themes cannot replace the saved preference", store =>
        {
            using var app = Controller(store); app.SetTheme("light");
            bool rejected = false;
            try { app.SetTheme("invalid"); } catch (ArgumentException) { rejected = true; }
            Equal(true, rejected); Equal("light", app.Settings.AppTheme); Equal("light", store.LoadSettings().AppTheme);
        });
        Test("A failed disk save keeps the previously saved theme and does not announce success", store =>
        {
            using var app = Controller(store); app.SetTheme("light");
            int changes = 0; app.Changed += () => changes++;
            // A directory at the atomic-write filename reliably simulates an unwritable target.
            Directory.CreateDirectory(Path.Combine(store.DirectoryPath, "settings.json.tmp"));
            bool rejected = false;
            try { app.SetTheme("dark"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { rejected = true; }
            Equal(true, rejected); Equal("light", app.Settings.AppTheme);
            Equal("light", new SettingsStore(store.DirectoryPath).LoadSettings().AppTheme); Equal(0, changes);
        });
        Console.WriteLine($"\n{passed} passed; {failed} failed.");
        return failed == 0 ? 0 : 1;
    }
}
