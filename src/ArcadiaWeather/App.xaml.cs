using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using ArcadiaWeather.Infrastructure;
using ArcadiaWeather.Presentation;
using Forms = System.Windows.Forms;

namespace ArcadiaWeather;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private Forms.NotifyIcon? _tray;
    private AppController? _controller;
    private MainWindow? _main;
    private SettingsStore? _store;
    private bool _ownsMutex;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Packaging checks run before mutex/tray/settings initialization and never create a window.
        if (e.Args.Contains("--verify-bundle"))
        {
            string report = Argument(e.Args, "--verify-bundle") ?? Path.Combine(Environment.CurrentDirectory, "bundle-verification.json");
            Shutdown(await BundleVerification.RunAsync(report));
            return;
        }
        DispatcherUnhandledException += OnUnhandledException;
        try
        {
            string? dataDir = Argument(e.Args, "--data-dir");
            bool smoke = e.Args.Contains("--smoke-test");
            bool uiSmoke = e.Args.Contains("--ui-smoke-test");
            _mutex = new Mutex(true, uiSmoke ? "Local\\ArcadiaWeather.UiSmoke" : smoke ? "Local\\ArcadiaWeather.Smoke" : "Local\\ArcadiaWeather", out _ownsMutex);
            if (!_ownsMutex)
            {
                try { using var existing = EventWaitHandle.OpenExisting("Local\\ArcadiaWeather.Show"); existing.Set(); } catch (WaitHandleCannotBeOpenedException) { }
                Shutdown(); return;
            }
            WindowIcons.IdentifyProcess();
            _store = new SettingsStore(dataDir);
            _controller = new AppController(_store, Dispatcher, smoke || uiSmoke);
            _main = new MainWindow(_controller);
            MainWindow = _main;
            if (uiSmoke)
            {
                _main.Show();
                await SmokeTest.RunUiAsync(_controller, _main, Argument(e.Args, "--ui-smoke-test") ?? Path.Combine(AppContext.BaseDirectory, "ui-smoke"));
                Shutdown(Environment.ExitCode); return;
            }
            if (smoke)
            {
                _main.Show();
                await SmokeTest.RunAsync(_controller, _main, Argument(e.Args, "--smoke-test") ?? Path.Combine(AppContext.BaseDirectory, "smoke"));
                Shutdown(); return;
            }
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\ArcadiaWeather.Show");
            _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => Dispatcher.BeginInvoke(ShowWindow), null, -1, false);
            CreateTray();
            if (!e.Args.Contains("--tray")) _main.Show();
            await _controller.StartAsync();
        }
        catch (Exception ex)
        {
            _store?.Log(ex);
            MessageBox.Show($"Arcadia Weather could not start.\n\n{ex.Message}", "Arcadia Weather", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    private static string? Argument(string[] args, string key)
    {
        int index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("--") ? args[index + 1] : null;
    }
    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Arcadia Weather", null, (_, _) => ShowWindow());
        menu.Items.Add("Automatic weather", null, (_, _) => _controller!.SelectScene(null));
        foreach (var scene in Enum.GetValues<Scene>())
        {
            var selected = scene;
            menu.Items.Add($"Day {(int)scene} · {SceneInfo.ShortName(scene)}", null, (_, _) => _controller!.SelectScene(selected));
        }
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Pause / resume", null, (_, _) => _controller!.TogglePause());
        menu.Items.Add("Mute / unmute", null, (_, _) => _controller!.SetAudio(_controller.Settings.Volume, !_controller.Settings.Muted));
        menu.Items.Add("Refresh weather", null, async (_, _) => await _controller!.RefreshAsync());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());
        _tray = new Forms.NotifyIcon { Text = "Arcadia Weather", Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!), ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => ShowWindow();
        _controller!.Changed += () => { if (_tray is not null) _tray.Text = $"Arcadia Weather · {SceneInfo.ShortName(_controller.ActiveScene)}"; };
    }
    private void ShowWindow()
    {
        if (_main is null) return;
        _main.Show(); _main.WindowState = WindowState.Normal; _main.Activate();
    }
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _store?.Log(e.Exception);
        MessageBox.Show(e.Exception.Message, "Arcadia Weather", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _main?.AllowClose(); _controller?.Dispose();
        if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        _showWait?.Unregister(null); _showEvent?.Dispose();
        if (_ownsMutex) _mutex?.ReleaseMutex(); _mutex?.Dispose();
        base.OnExit(e);
    }
}
