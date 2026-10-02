using LibVLCSharp.Shared;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ArcadiaWeather.Playback;

public sealed class WallpaperPlayer : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private LibVLC? _vlc;
    private MediaPlayer? _player;
    private WallpaperWindow? _window;
    private nint _host;
    private string? _currentPath;
    private string _monitor = "";
    private bool _paused, _disposed;
    private int _volume = 25;
    private bool _muted = true;
    private string _fit = "fill";
    public event Action<string>? Failed;
    public long PlaybackTime => _player?.Time ?? 0;
    public bool IsPlaying => _player?.IsPlaying == true;
    public bool IsAttached => _window is { IsDisposed: false } && DesktopHost.IsWindow(_host)
        && DesktopHost.GetParent(_window.Handle) == _host;

    public WallpaperPlayer(Dispatcher dispatcher) => _dispatcher = dispatcher;
    public Forms.Screen TargetScreen => Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == _monitor)
        ?? Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
    public bool FullScreenAppActive => DesktopHost.HasFullScreenApp(TargetScreen);

    private void EnsurePlayer()
    {
        if (_player is not null && _window is { IsDisposed: false } && IsAttached) return;
        ReleasePlayer();
        if (_vlc is null)
        {
            LibVLCSharp.Shared.Core.Initialize(Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64"));
            _vlc = new LibVLC("--no-video-title-show", "--no-osd", "--no-snapshot-preview", "--avcodec-hw=any", "--quiet");
        }
        _window = new WallpaperWindow { Bounds = TargetScreen.Bounds };
        var handle = _window.Handle;
        _host = DesktopHost.Attach(handle, TargetScreen.Bounds);
        _window.Show();
        // Show can adjust WinForms z-order, so attach once more after Show.
        _host = DesktopHost.Attach(handle, TargetScreen.Bounds);
        _player = new MediaPlayer(_vlc) { Hwnd = handle, EnableHardwareDecoding = true, EnableMouseInput = false, EnableKeyInput = false };
        _player.EncounteredError += OnError;
        _player.EndReached += OnEndReached;
        _player.Playing += OnPlaying;
    }
    public void Play(string path, string monitor, int volume, bool muted, string fit = "fill")
    {
        if (!File.Exists(path)) throw new FileNotFoundException("A bundled scene is missing. Extract the complete app package again.");
        _volume = volume; _muted = muted;
        bool fitChanged = _fit != fit;
        _fit = fit;
        if (_monitor != monitor) { ReleasePlayer(); _monitor = monitor; }
        bool replay = _currentPath != path || !IsAttached || _player is null;
        EnsurePlayer();
        _player!.Volume = volume;
        _player.Mute = muted;
        if (fitChanged) ApplyFit();
        if (replay)
        {
            _currentPath = path;
            using var media = new Media(_vlc!, path, FromType.FromPath);
            media.AddOption(":input-repeat=65535");
            if (!_player.Play(media)) throw new InvalidOperationException("The video player could not start this file.");
            _player.Volume = volume;
            _player.Mute = muted;
            if (_paused) _player.SetPause(true);
        }
    }
    public void SetPaused(bool paused)
    {
        if (_paused == paused) return;
        _paused = paused;
        _player?.SetPause(paused);
    }
    public void SetAudio(int volume, bool muted)
    {
        _volume = volume; _muted = muted;
        if (_player is null) return;
        _player.Volume = volume; _player.Mute = muted;
    }
    public void Reattach() => ReleasePlayer();
    public bool Snapshot(string path) => _player?.TakeSnapshot(0, path, 960, 0) == true;
    public void SeekNearEnd() { if (_player is not null && _player.Length > 2000) _player.Time = _player.Length - 1200; }
    private void OnPlaying(object? sender, EventArgs e) => _dispatcher.BeginInvoke(() =>
    {
        if (_disposed || !ReferenceEquals(sender, _player) || _player is null) return;
        _player.Volume = _volume; _player.Mute = _muted;
        ApplyFit();
        // Play is asynchronous: pausing while the media is opening may otherwise be lost.
        if (_paused) _player.SetPause(true);
    });
    private void ApplyFit()
    {
        if (_player is null) return;
        // Crop to the display's ratio for Fill; an empty crop restores letterboxed Fit.
        var bounds = TargetScreen.Bounds;
        _player.CropGeometry = _fit == "fill" ? $"{bounds.Width}:{bounds.Height}" : "";
        _player.AspectRatio = "";
        _player.Scale = 0;
    }
    private void OnError(object? sender, EventArgs e) => _dispatcher.BeginInvoke(() =>
    {
        if (!_disposed && ReferenceEquals(sender, _player)) { _currentPath = null; Failed?.Invoke("Video playback failed. Check that the file is available locally."); }
    });
    private void OnEndReached(object? sender, EventArgs e) => _dispatcher.BeginInvoke(() =>
    {
        if (_disposed || !ReferenceEquals(sender, _player) || _currentPath is null || _player is null || _vlc is null) return;
        using var media = new Media(_vlc, _currentPath, FromType.FromPath);
        media.AddOption(":input-repeat=65535");
        _player.Play(media);
        if (_paused) _player.SetPause(true);
    });
    private void ReleasePlayer()
    {
        if (_player is not null)
        {
            _player.EncounteredError -= OnError; _player.EndReached -= OnEndReached;
            _player.Playing -= OnPlaying;
            _player.Stop(); _player.Dispose(); _player = null;
        }
        _window?.Close(); _window?.Dispose(); _window = null; _host = 0; _currentPath = null;
    }
    public void Dispose() { _disposed = true; ReleasePlayer(); _vlc?.Dispose(); _vlc = null; }
}
