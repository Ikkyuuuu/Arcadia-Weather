using System.Net.Http;
using System.Windows.Threading;
using Microsoft.Win32;
using ArcadiaWeather.Infrastructure;
using ArcadiaWeather.Playback;

namespace ArcadiaWeather;

public sealed class AppController : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _fetchLock = new(1, 1);
    private readonly SceneRules _rules = new();
    private readonly DispatcherTimer _timer;
    private readonly Dispatcher _dispatcher;
    private int _generation;
    private bool _disposed, _suspended, _sessionLocked;
    private DateTimeOffset _nextPlayerRetry;
    private readonly bool _disablePlayback;
    public SettingsStore Store { get; }
    public AppSettings Settings { get; private set; }
    public WeatherClient WeatherClient { get; }
    public WallpaperPlayer Player { get; }
    public WeatherSnapshot? Weather { get; private set; }
    public SceneDecision Decision { get; private set; }
    public Scene? ManualScene { get; private set; }
    public Scene ActiveScene => ManualScene ?? Decision.Scene;
    public bool UserPaused { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsFetching { get; private set; }
    public DateTimeOffset NextRefresh { get; private set; } = DateTimeOffset.UtcNow;
    public string? WeatherError { get; private set; }
    public string? PlayerError { get; private set; }
    public event Action? Changed;

    public AppController(SettingsStore store, Dispatcher dispatcher, bool disablePlayback = false)
    {
        Store = store; _dispatcher = dispatcher; _disablePlayback = disablePlayback;
        Settings = store.LoadSettings(); Weather = store.LoadWeather();
        WeatherClient = new(_http); Player = new(dispatcher);
        Decision = _rules.Decide(Settings, Weather, DateTimeOffset.UtcNow);
        Player.Failed += error => { PlayerError = error; _nextPlayerRetry = DateTimeOffset.UtcNow.AddMinutes(1); Store.Log(error); Changed?.Invoke(); };
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, OnTick, dispatcher);
        SystemEvents.PowerModeChanged += OnPowerMode;
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }
    public async Task StartAsync()
    {
        Evaluate();
        await RefreshAsync();
    }
    private async void OnTick(object? sender, EventArgs e)
    {
        if (_disposed) return;
        Evaluate();
        if (!_suspended && DateTimeOffset.UtcNow >= NextRefresh) await RefreshAsync();
    }
    public void Evaluate()
    {
        if (_disposed) return;
        var now = DateTimeOffset.UtcNow;
        Decision = _rules.Decide(Settings, Weather, now);
        IsPaused = UserPaused || _suspended || _sessionLocked || (Settings.PauseOnFullScreen && Player.FullScreenAppActive);
        if (!_disablePlayback && now >= _nextPlayerRetry)
        {
            try
            {
                Player.Play(Settings.VideoPath(ActiveScene), Settings.MonitorDevice, Settings.Volume, Settings.Muted, Settings.WallpaperFit);
                Player.SetPaused(IsPaused); PlayerError = null;
            }
            catch (Exception ex)
            {
                PlayerError = ex.Message; Store.Log(ex); _nextPlayerRetry = now.AddMinutes(1);
            }
        }
        Changed?.Invoke();
    }
    public async Task RefreshAsync()
    {
        if (_disposed || !await _fetchLock.WaitAsync(0)) return;
        int generation = _generation;
        IsFetching = true; Changed?.Invoke();
        try
        {
            var snapshot = await WeatherClient.FetchAsync(Settings.Copy(), _lifetime.Token);
            if (_disposed || generation != _generation) return;
            Weather = snapshot; WeatherError = null;
            try { Store.SaveWeather(snapshot); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Store.Log(ex); }
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception ex)
        {
            if (_disposed || generation != _generation) return;
            WeatherError = ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests }
                ? "Weather service busy. Retrying in 5 minutes."
                : "Weather update unavailable. Retrying in 5 minutes.";
            Store.Log(ex);
        }
        finally
        {
            NextRefresh = generation == _generation ? DateTimeOffset.UtcNow.Add(AppSettings.PollInterval) : DateTimeOffset.UtcNow;
            IsFetching = false; _fetchLock.Release();
            if (!_disposed) Evaluate();
        }
    }
    public void SelectScene(Scene? scene) { ManualScene = scene; _nextPlayerRetry = default; Evaluate(); }
    public void TogglePause() { UserPaused = !UserPaused; _nextPlayerRetry = default; Evaluate(); }
    public void SetTheme(string theme)
    {
        if (Settings.AppTheme == theme) return;
        var updated = Settings.Copy();
        updated.AppTheme = theme;
        updated.Validate();
        // Save only the appearance preference. No location request, player restart,
        // startup registry update, or unsaved settings-form draft is involved.
        Store.SaveSettings(updated);
        Settings.AppTheme = theme;
        Changed?.Invoke();
    }
    public void SetAudio(int volume, bool muted)
    {
        Settings.Volume = volume; Settings.Muted = muted;
        Player.SetAudio(volume, muted);
        try { Store.SaveSettings(Settings); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Store.Log(ex); }
        Changed?.Invoke();
    }
    public async Task SaveSettingsAsync(AppSettings settings)
    {
        settings.Validate();
        var missing = Enum.GetValues<Scene>().Where(s => !File.Exists(settings.VideoPath(s))).Select(s => $"Day {(int)s}").ToList();
        if (missing.Count > 0) throw new InvalidOperationException($"Bundled scenes are missing: {string.Join(", ", missing)}. Extract the complete app package again.");
        StartupManager.Apply(settings.StartWithWindows);
        Store.SaveSettings(settings);
        bool newLocation = settings.Latitude != Settings.Latitude || settings.Longitude != Settings.Longitude || settings.TimeZoneId != Settings.TimeZoneId;
        bool newMonitor = settings.MonitorDevice != Settings.MonitorDevice;
        Settings = settings; _generation++; _rules.Reset();
        if (newLocation) Weather = null;
        if (newMonitor) Player.Reattach();
        _nextPlayerRetry = default; NextRefresh = DateTimeOffset.UtcNow;
        Evaluate(); await RefreshAsync();
    }
    private void OnPowerMode(object sender, PowerModeChangedEventArgs e) => _dispatcher.BeginInvoke(() =>
    {
        if (_disposed) return;
        _suspended = e.Mode == PowerModes.Suspend;
        if (e.Mode == PowerModes.Resume) { NextRefresh = DateTimeOffset.UtcNow; _nextPlayerRetry = default; Player.Reattach(); }
        Evaluate();
    });
    private void OnDisplayChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke(() =>
    {
        if (_disposed) return;
        Player.Reattach(); _nextPlayerRetry = default; Evaluate();
    });
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e) => _dispatcher.BeginInvoke(() =>
    {
        if (_disposed) return;
        if (e.Reason == SessionSwitchReason.SessionLock) _sessionLocked = true;
        if (e.Reason == SessionSwitchReason.SessionUnlock) { _sessionLocked = false; NextRefresh = DateTimeOffset.UtcNow; }
        Evaluate();
    });
    public void Dispose()
    {
        _disposed = true; _timer.Stop(); _lifetime.Cancel();
        SystemEvents.PowerModeChanged -= OnPowerMode;
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        Player.Dispose(); _http.Dispose();
    }
}
