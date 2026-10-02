using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Input;
using Microsoft.Win32;
using ArcadiaWeather.Infrastructure;
using ArcadiaWeather.Presentation;
using Forms = System.Windows.Forms;

namespace ArcadiaWeather;

public partial class MainWindow : Window
{
    private readonly AppController _controller;
    private readonly WindowIcons _windowIcons = new();
    private readonly Dictionary<Scene, SceneCardButton> _cards = [];
    private readonly Dictionary<Scene, BitmapImage> _images = [];
    private readonly List<Border> _sceneThumbnails = [];
    private readonly List<TextBlock> _sceneSubtitles = [];
    private bool _ready, _updatingAudio, _allowClose;
    private Scene? _shownScene;
    private bool? _shownManualMode;
    private bool _refreshSpinning, _locationSpinning;
    private int _sceneTransition;
    private readonly CancellationTokenSource _searchLifetime = new();
    private readonly DeviceLocationReader _locationReader = new(new WindowsLocationPlatform());
    private CancellationTokenSource? _locationRequest;
    private string? _previewPath;
    private bool _previewFailed, _previewReady;
    private bool _updatingTheme;
    private string? _appearanceError;
    public MainWindow(AppController controller)
    {
        _controller = controller;
        InitializeComponent();
        WindowTheme.Apply(this, _controller.Settings.AppTheme);
        SourceInitialized += (_, _) => { WindowTheme.Apply(this, _controller.Settings.AppTheme); _windowIcons.Apply(this); };
        SystemEvents.UserPreferenceChanged += SystemThemeChanged;
        foreach (var scene in Enum.GetValues<Scene>())
        {
            var image = new BitmapImage(new Uri($"pack://application:,,,/Assets/day{(int)scene}.jpg"));
            image.Freeze();
            _images[scene] = image;
            var content = new StackPanel();
            var thumbnail = new Border { Height = 84, CornerRadius = new CornerRadius(7), Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill } };
            _sceneThumbnails.Add(thumbnail);
            content.Children.Add(thumbnail);
            var caption = new StackPanel { Margin = new Thickness(5, 10, 5, 8) };
            caption.Children.Add(new TextBlock { Text = $"Day {(int)scene}", FontSize = 13, FontWeight = FontWeights.SemiBold });
            var subtitle = new TextBlock { Text = SceneInfo.ShortName(scene), FontSize = 11, Margin = new Thickness(0, 3, 0, 0) };
            subtitle.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            _sceneSubtitles.Add(subtitle);
            caption.Children.Add(subtitle);
            content.Children.Add(caption);
            var button = new SceneCardButton { Content = content, Margin = new Thickness(0, 0, scene == Scene.Rain ? 0 : 12, 0), ToolTip = $"Hold Day {(int)scene} until Automatic weather is selected" };
            System.Windows.Automation.AutomationProperties.SetName(button, $"Day {(int)scene} {SceneInfo.ShortName(scene)}");
            var selected = scene; button.Click += (_, _) => _controller.SelectScene(selected);
            _cards[scene] = button; SceneCards.Children.Add(button);
        }
        FillSettings(); SyncThemeControls(); _ready = true;
        _controller.Changed += UpdateStatus;
        Closing += OnClosing;
        Loaded += (_, _) => { _windowIcons.Apply(this); WindowFrame.KeepContentInWorkArea(this, WindowContent); UpdateLayoutForWidth(); Motion.Reveal(BayPage); UpdateStatus(); };
        Closed += (_, _) => _windowIcons.Dispose();
        IsVisibleChanged += (_, _) => { UpdateRefreshMotion(); UpdatePreview(); };
        StateChanged += (_, _) =>
        {
            UpdateWindowControls(); UpdateRefreshMotion(); UpdatePreview();
            Dispatcher.BeginInvoke(() => WindowFrame.KeepContentInWorkArea(this, WindowContent));
        };
        DpiChanged += (_, _) => WindowFrame.KeepContentInWorkArea(this, WindowContent);
        UpdateStatus();
    }
    public void AllowClose()
    {
        _allowClose = true;
        _locationRequest?.Cancel();
        _searchLifetime.Cancel();
        SystemEvents.UserPreferenceChanged -= SystemThemeChanged;
        HeroVideo.Close();
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) { _controller.Changed -= UpdateStatus; _searchLifetime.Cancel(); return; }
        e.Cancel = true; _locationRequest?.Cancel(); Hide();
    }
    private void UpdateStatus()
    {
        if (!_ready) return;
        var settings = _controller.Settings;
        var now = DateTimeOffset.UtcNow;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        var local = TimeZoneInfo.ConvertTime(now, zone);
        HeaderLocation.Text = settings.LocationName;
        HeaderClock.Text = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        HeaderClock.ToolTip = local.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture);
        CoordinatesText.Text = FormattableString.Invariant($"{Math.Abs(settings.Latitude):0.0000}° {(settings.Latitude < 0 ? "S" : "N")}, {Math.Abs(settings.Longitude):0.0000}° {(settings.Longitude < 0 ? "W" : "E")}");
        var scene = _controller.ActiveScene;
        if (_shownScene != scene)
        {
            ShowScene(scene); SceneTitle.Text = SceneInfo.ShortName(scene);
            SceneEyebrow.Text = $"Day {(int)scene}"; _shownScene = scene;
        }
        bool manual = _controller.ManualScene is not null;
        ModeBadge.Text = manual ? "Manual" : "Automatic";
        ModeDot.Fill = new SolidColorBrush(Color.FromRgb(6, 39, 47));
        HeroCopy.ToolTip = manual ? "Your chosen scene · select Automatic to follow the weather again" : _controller.Decision.Reason;
        AutoButton.Tag = manual ? "" : "selected";
        ManualButton.Tag = manual ? "selected" : "";
        if (_shownManualMode != manual)
        {
            UpdateModeIndicator(animate: _shownManualMode is not null);
            _shownManualMode = manual;
        }
        HoldNote.Visibility = manual ? Visibility.Visible : Visibility.Hidden;
        HoldText.Text = $"Holding Day {(int)scene}. Automatic would show Day {(int)_controller.Decision.Scene}.";
        ModeReason.Text = manual ? "Your scene stays until you return to Automatic." : _controller.Decision.Reason;
        ModeReason.ToolTip = ModeReason.Text;
        System.Windows.Automation.AutomationProperties.SetItemStatus(AutoButton, manual ? "" : "Selected");
        System.Windows.Automation.AutomationProperties.SetItemStatus(ManualButton, manual ? "Selected" : "");
        AutoButton.ToolTip = "Let the time and weather choose your wallpaper";
        foreach (var (value, button) in _cards)
        {
            button.IsSelected = value == scene;
            System.Windows.Automation.AutomationProperties.SetItemStatus(button, value == scene ? "Active scene" : "");
        }
        PauseGlyph.Text = _controller.UserPaused ? "\uE768" : "\uE769";
        PauseButton.ToolTip = _controller.UserPaused ? "Resume wallpaper" : "Pause wallpaper";
        System.Windows.Automation.AutomationProperties.SetName(PauseButton, (string)PauseButton.ToolTip);
        PausedBadge.Visibility = _controller.IsPaused ? Visibility.Visible : Visibility.Collapsed;
        PausedBadgeText.Text = _controller.UserPaused ? "Paused" : "Paused automatically";
        PlaybackText.Text = _controller.PlayerError is not null ? "Playback needs attention" : _controller.IsPaused ? (_controller.UserPaused ? "Paused" : "Paused · fullscreen / lock / sleep") : "Playing on your desktop";
        var weather = _controller.Weather;
        bool matching = SceneRules.MatchesLocation(settings, weather);
        bool fresh = SceneRules.IsFresh(settings, weather, now);
        TemperatureText.Text = matching && fresh ? FormatTemperature(weather!.Temperature) : "—°";
        TemperatureText.ToolTip = settings.TemperatureUnit == "F" ? "Degrees Fahrenheit" : "Degrees Celsius";
        FeelsLikeText.Text = matching && fresh && weather!.FeelsLike is double feels ? FormatTemperature(feels) : "—";
        HumidityText.Text = matching && fresh && weather!.Humidity is double humidity ? $"{humidity:0}%" : "—";
        ConditionText.Text = matching && fresh ? SceneInfo.WeatherDescription(weather!.WeatherCode) : "Using time of day";
        string weatherIcon = matching && fresh && SceneRules.IsWet(weather!) ? "RainIcon"
            : matching && fresh && weather!.WeatherCode is 2 or 3 or 45 or 48 or 71 or 73 or 75 or 77 or 85 or 86 ? "CloudIcon"
            : _controller.Decision.Phase == DayPhase.Night ? "MoonIcon" : "SunIcon";
        WeatherIcon.Data = (Geometry)FindResource(weatherIcon);
        CloudText.Text = matching && fresh ? $"{weather!.CloudCover:0}%" : "—";
        var sun = matching ? weather!.Sun?.FirstOrDefault(s => s.Date == DateOnly.FromDateTime(local.DateTime)) : null;
        SunsetText.Text = sun is null ? "—" : TimeZoneInfo.ConvertTime(sun.Sunset, zone).ToString("HH:mm");
        var remaining = _controller.NextRefresh - now;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        UpdateText.Text = _controller.IsFetching ? "Checking the weather…" : fresh
            ? $"Checked {TimeZoneInfo.ConvertTime(weather!.FetchedAt, zone):HH:mm} · next {remaining:mm\\:ss}"
            : "Weather offline";
        UpdateText.ToolTip = $"Next weather check in {remaining:mm\\:ss}";
        RefreshButton.IsEnabled = !_controller.IsFetching;
        UpdateRefreshMotion();
        string? error = _appearanceError ?? _controller.PlayerError ?? _controller.WeatherError ?? _controller.Store.Warning;
        ErrorBanner.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        ErrorText.Text = error ?? "";
        _updatingAudio = true;
        MuteGlyph.Text = settings.Muted || settings.Volume == 0 ? "\uE74F" : "\uE767";
        MuteButton.ToolTip = settings.Muted || settings.Volume == 0 ? "Unmute wallpaper" : "Mute wallpaper";
        System.Windows.Automation.AutomationProperties.SetName(MuteButton, (string)MuteButton.ToolTip);
        VolumeSlider.Value = settings.Volume; VolumeText.Text = settings.Volume.ToString();
        _updatingAudio = false;
        UpdatePreview();
    }
    private void ShowScene(Scene scene)
    {
        int transition = ++_sceneTransition;
        // A replaced animation cannot clear the next scene's image during rapid clicks.
        HeroPreviousImage.Source = HeroImage.Opacity < 0.5 ? HeroPreviousImage.Source : HeroImage.Source;
        HeroImage.BeginAnimation(OpacityProperty, null);
        HeroImage.Source = _images[scene];
        HeroImage.Opacity = 1;
        if (_shownScene is null || !Motion.CanAnimate(HeroImage)) { HeroPreviousImage.Source = null; return; }
        var fade = Motion.Transition(0, 1, 520);
        fade.Completed += (_, _) => { if (transition == _sceneTransition) HeroPreviousImage.Source = null; };
        HeroImage.BeginAnimation(OpacityProperty, fade);
        Motion.Reveal(HeroCopy);
    }
    private void UpdateRefreshMotion()
    {
        if (!_ready) return;
        bool spinning = _controller.IsFetching && BayTab.IsSelected && WindowState != WindowState.Minimized && Motion.CanAnimate(this);
        if (spinning != _refreshSpinning)
        {
            _refreshSpinning = spinning;
            SetSpinning(RefreshRotation, spinning);
        }
        bool locating = _locationRequest is not null && SettingsTab.IsSelected && WindowState != WindowState.Minimized && Motion.CanAnimate(this);
        if (locating != _locationSpinning)
        {
            _locationSpinning = locating;
            SetSpinning(LocationRotation, locating);
        }
    }
    private static void SetSpinning(RotateTransform rotation, bool spinning)
    {
        rotation.BeginAnimation(RotateTransform.AngleProperty, null);
        rotation.Angle = 0;
        if (spinning) rotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.1)) { RepeatBehavior = RepeatBehavior.Forever });
    }
    private void RoundedSurface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement surface) surface.Clip = new RectangleGeometry(new Rect(0, 0, surface.ActualWidth, surface.ActualHeight), 18, 18);
    }
    private void FillSettings()
    {
        var s = _controller.Settings;
        LocationNameInput.Text = s.LocationName;
        LatitudeInput.Text = s.Latitude.ToString(CultureInfo.InvariantCulture);
        LongitudeInput.Text = s.Longitude.ToString(CultureInfo.InvariantCulture);
        TimezoneInput.Text = s.TimeZoneId;
        MonitorInput.ItemsSource = Forms.Screen.AllScreens.Select((screen, index) => new { Device = screen.DeviceName, Label = $"Display {index + 1} · {screen.Bounds.Width} × {screen.Bounds.Height}{(screen.Primary ? " · Primary" : "")}" }).ToArray();
        MonitorInput.SelectedValue = string.IsNullOrEmpty(s.MonitorDevice) ? Forms.Screen.PrimaryScreen?.DeviceName : s.MonitorDevice;
        if (MonitorInput.SelectedIndex < 0) MonitorInput.SelectedIndex = 0;
        StartupInput.IsChecked = s.StartWithWindows; FullScreenInput.IsChecked = s.PauseOnFullScreen;
        UnitInput.SelectedValue = s.TemperatureUnit; FitInput.SelectedValue = s.WallpaperFit; ThemeInput.SelectedValue = s.AppTheme;
        CloudEnterInput.Text = s.CloudEnterPercent.ToString(); CloudExitInput.Text = s.CloudExitPercent.ToString();
        EveningInput.Text = s.EveningMinutesBeforeSunset.ToString(); NightInput.Text = s.NightMinutesAfterSunset.ToString();
    }
    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || e.Source != Tabs) return;
        if (!SettingsTab.IsSelected) _locationRequest?.Cancel();
        // Keep a location lookup or typed settings draft intact when navigating between tabs.
        Dispatcher.BeginInvoke(() =>
        {
            Motion.Reveal(BayTab.IsSelected ? BayPage : SettingsTab.IsSelected ? SettingsPage : GuidePage);
            UpdateRefreshMotion();
            UpdatePreview();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await _controller.RefreshAsync();
    private void Auto_Click(object sender, RoutedEventArgs e) => _controller.SelectScene(null);
    private void Manual_Click(object sender, RoutedEventArgs e) => _controller.SelectScene(_controller.ActiveScene);
    private void ModeSelector_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateModeIndicator(animate: false);
    private void UpdateModeIndicator(bool animate)
    {
        double width = ModeSelector.ActualWidth / 2;
        if (width <= 0 || ModeIndicator is null) return;
        ModeIndicator.Width = width;
        Motion.SlideTo(ModeIndicator, _controller.ManualScene is null ? 0 : width, animate);
    }
    private void Pause_Click(object sender, RoutedEventArgs e) => _controller.TogglePause();
    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        var settings = _controller.Settings;
        bool unmute = settings.Muted || settings.Volume == 0;
        _controller.SetAudio(unmute && settings.Volume == 0 ? 25 : settings.Volume, !unmute);
    }
    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_ready && !_updatingAudio) _controller.SetAudio((int)Math.Round(e.NewValue), e.NewValue == 0);
    }
    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        SetLocationBusy(true);
        SearchButton.Content = "Searching…";
        SettingsMessage.Text = "Finding matching locations…";
        try
        {
            var cities = await _controller.WeatherClient.SearchAsync(CitySearch.Text, _searchLifetime.Token);
            CityResults.ItemsSource = cities;
            if (cities.Count > 0) CityResults.SelectedIndex = 0;
            SettingsMessage.Text = cities.Count == 0 ? "No matching cities. Try another name or enter coordinates." : "Choose a city, then save your settings.";
        }
        catch (Exception ex) { SettingsMessage.Text = "City search is unavailable. You can enter coordinates manually."; _controller.Store.Log(ex); }
        finally { SearchButton.Content = "Search"; SetLocationBusy(false); }
    }
    private async void UseLocation_Click(object sender, RoutedEventArgs e)
    {
        if (_locationRequest is not null) return;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(_searchLifetime.Token);
        _locationRequest = request;
        SetLocationBusy(true, canCancel: true);
        UseLocationButton.Content = "Locating…";
        LocationSettingsButton.Visibility = Visibility.Collapsed;
        LocationStatusText.Visibility = Visibility.Visible;
        LocationStatusText.Text = "Waiting for Windows location permission / a position…";
        try
        {
            Activate();
            var location = await _locationReader.ReadAsync(request.Token);
            request.Token.ThrowIfCancellationRequested();
            CityResults.ItemsSource = null;
            LatitudeInput.Text = location.Latitude.ToString("F6", CultureInfo.InvariantCulture);
            LongitudeInput.Text = location.Longitude.ToString("F6", CultureInfo.InvariantCulture);
            LocationNameInput.Text = "My current location";
            // Use a valid device timezone as a visible fallback while resolving the coordinate timezone.
            TimezoneInput.Text = TimeZoneInfo.Local.Id;
            string detail = $"{location.Source} · reported accuracy {location.AccuracyDescription}.";
            LocationStatusText.Text = detail + " Finding the timezone…";
            var draft = _controller.Settings.Copy();
            draft.Latitude = location.Latitude; draft.Longitude = location.Longitude;
            draft.TimeZoneId = TimezoneInput.Text;
            try
            {
                var weather = await _controller.WeatherClient.FetchAsync(draft, request.Token);
                request.Token.ThrowIfCancellationRequested();
                TimezoneInput.Text = weather.TimeZoneId;
                LocationStatusText.Text = detail + " Coordinates and timezone filled. Save settings to apply.";
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                LocationStatusText.Text = detail + " Coordinates filled. Timezone lookup is unavailable; check the timezone before saving.";
            }
            SettingsMessage.Text = "Location filled. Save settings to use it for your wallpaper.";
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            LocationStatusText.Text = "Location lookup cancelled. Nothing was saved.";
        }
        catch (LocationUnavailableException ex)
        {
            LocationStatusText.Text = ex.Message;
            LocationSettingsButton.Visibility = ex.OpenSettings ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex.HResult == unchecked((int)0x80070005))
        {
            LocationStatusText.Text = "Windows denied location access. Open Windows location settings to allow it, then try again.";
            LocationSettingsButton.Visibility = Visibility.Visible;
        }
        catch (Exception ex) when (ex is TimeoutException || ex.HResult == unchecked((int)0x800705B4))
        {
            LocationStatusText.Text = "Windows could not find a position within 30 seconds. Check Location services, then try again or enter coordinates manually.";
            LocationSettingsButton.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            LocationStatusText.Text = "Windows could not determine your location. Check Location services, then try again or enter coordinates manually.";
            LocationSettingsButton.Visibility = Visibility.Visible;
            // Do not record coordinates or a platform exception containing location data in logs.
            _controller.Store.Log($"Location lookup failed: {ex.GetType().Name}, HRESULT 0x{ex.HResult:X8}");
        }
        finally
        {
            _locationRequest = null;
            UseLocationButton.Content = "Use my location";
            SetLocationBusy(false);
            Motion.Reveal(LocationStatusText);
        }
    }
    private void SetLocationBusy(bool busy, bool canCancel = false)
    {
        UseLocationButton.IsEnabled = !busy; SearchButton.IsEnabled = !busy; SaveButton.IsEnabled = !busy; RevertButton.IsEnabled = !busy;
        CitySearch.IsEnabled = !busy; CityResults.IsEnabled = !busy;
        LocationNameInput.IsEnabled = !busy; LatitudeInput.IsEnabled = !busy;
        LongitudeInput.IsEnabled = !busy; TimezoneInput.IsEnabled = !busy;
        CancelLocationButton.Visibility = busy && canCancel ? Visibility.Visible : Visibility.Collapsed;
        LocationActivity.Visibility = CancelLocationButton.Visibility;
        UpdateRefreshMotion();
    }
    private void CancelLocation_Click(object sender, RoutedEventArgs e) => _locationRequest?.Cancel();
    private void LocationSettings_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("ms-settings:privacy-location") { UseShellExecute = true });
    private void City_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CityResults.SelectedItem is not City city) return;
        LocationNameInput.Text = city.ToString(); LatitudeInput.Text = city.Latitude.ToString(CultureInfo.InvariantCulture);
        LongitudeInput.Text = city.Longitude.ToString(CultureInfo.InvariantCulture); TimezoneInput.Text = city.TimeZoneId;
    }
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SetLocationBusy(true);
        SaveButton.Content = "Saving…";
        try
        {
            var s = _controller.Settings.Copy();
            s.LocationName = LocationNameInput.Text.Trim(); s.Latitude = double.Parse(LatitudeInput.Text, CultureInfo.InvariantCulture);
            s.Longitude = double.Parse(LongitudeInput.Text, CultureInfo.InvariantCulture); s.TimeZoneId = TimezoneInput.Text.Trim();
            s.MonitorDevice = MonitorInput.SelectedValue as string ?? "";
            s.StartWithWindows = StartupInput.IsChecked == true; s.PauseOnFullScreen = FullScreenInput.IsChecked == true;
            s.TemperatureUnit = UnitInput.SelectedValue as string ?? "C";
            s.WallpaperFit = FitInput.SelectedValue as string ?? "fill";
            s.AppTheme = ThemeInput.SelectedValue as string ?? "system";
            s.CloudEnterPercent = int.Parse(CloudEnterInput.Text); s.CloudExitPercent = int.Parse(CloudExitInput.Text);
            s.EveningMinutesBeforeSunset = int.Parse(EveningInput.Text); s.NightMinutesAfterSunset = int.Parse(NightInput.Text);
            await _controller.SaveSettingsAsync(s); SettingsMessage.Text = "✓ Saved. Your desktop is following the new settings.";
            WindowTheme.Apply(this, s.AppTheme);
            Motion.Reveal(SettingsMessage);
        }
        catch (FormatException) { SettingsMessage.Text = "Check the number fields. Use a dot for decimal coordinates."; }
        catch (Exception ex) { SettingsMessage.Text = ex.Message; _controller.Store.Log(ex); }
        finally { SaveButton.Content = "Save settings"; SetLocationBusy(false); }
    }
    private void Logs_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo(_controller.Store.DirectoryPath) { UseShellExecute = true });
    private string FormatTemperature(double celsius) => $"{(_controller.Settings.TemperatureUnit == "F" ? celsius * 9 / 5 + 32 : celsius):0}°";
    private void SystemThemeChanged(object sender, UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        if (_allowClose) return;
        WindowTheme.Apply(this, _controller.Settings.AppTheme);
        SyncThemeControls();
        UpdatePreview();
    });
    private void ChangeLocation_Click(object sender, RoutedEventArgs e)
    {
        SettingsTab.IsSelected = true;
        Dispatcher.BeginInvoke(() => { LocationSection.BringIntoView(); CitySearch.Focus(); });
    }
    private void About_Click(object sender, RoutedEventArgs e) => Tabs.SelectedItem = AboutTab;
    private void SettingsJump_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string name } && FindName(name) is FrameworkElement section)
        { section.BringIntoView(); section.Focus(); }
    }
    private void Revert_Click(object sender, RoutedEventArgs e)
    {
        FillSettings(); CityResults.ItemsSource = null;
        LocationStatusText.Visibility = Visibility.Collapsed;
        SettingsMessage.Text = "Unsaved changes reverted.";
    }
    private void CitySearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SearchButton.IsEnabled) { e.Handled = true; Search_Click(sender, e); }
    }
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_ready) return;
        WindowFrame.KeepContentInWorkArea(this, WindowContent);
        UpdateLayoutForWidth();
    }
    private void WallpaperViewport_SizeChanged(object sender, SizeChangedEventArgs e) { if (_ready) UpdateLayoutForWidth(); }
    private void UpdateLayoutForWidth()
    {
        bool narrowNavbar = ActualWidth < 1000;
        BrandText.Visibility = narrowNavbar ? Visibility.Collapsed : Visibility.Visible;
        Tabs.Padding = new Thickness(narrowNavbar ? 68 : 220, 0, 390, 0);
        double width = WallpaperViewport.ActualWidth;
        double height = WallpaperViewport.ActualHeight;
        if (width <= 0 || height <= 0) return;
        bool compact = width < 1000;
        bool shortWindow = height < 660;
        BayPage.Margin = new Thickness(compact ? 16 : 28, shortWindow ? 16 : 24, compact ? 16 : 28, shortWindow ? 16 : 24);
        WeatherColumn.Width = new GridLength(compact ? 238 : width < 1160 ? 285 : 310);
        DashboardGap.Width = new GridLength(compact ? 20 : 36);
        NowPanel.Width = WeatherColumn.Width.Value;
        // Both axes are constrained by the actual tab viewport. The preview gets
        // the remaining star row; controls and all five scene cards stay visible.
        ScenesPanel.Margin = new Thickness(0, shortWindow ? 14 : 22, 0, 0);
        ScenesHint.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        foreach (var thumbnail in _sceneThumbnails) thumbnail.Height = compact ? 44 : shortWindow ? 62 : 84;
        foreach (var subtitle in _sceneSubtitles) subtitle.Visibility = width < 820 ? Visibility.Collapsed : Visibility.Visible;
        TemperatureText.FontSize = shortWindow ? 56 : 68;
        SettingsIndex.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SettingsIndexColumn.Width = new GridLength(compact ? 0 : 180);
    }
    private void Minimize_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }
    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();
    private void UpdateWindowControls()
    {
        bool maximized = WindowState == WindowState.Maximized;
        MaximizeGlyph.Text = maximized ? "\uE923" : "\uE922";
        MaximizeButton.ToolTip = maximized ? "Restore" : "Maximize";
        System.Windows.Automation.AutomationProperties.SetName(MaximizeButton, maximized ? "Restore window" : "Maximize window");
    }
    private void SyncThemeControls()
    {
        _updatingTheme = true;
        try
        {
            DarkModeToggle.IsChecked = WindowTheme.IsDark;
            ThemeInput.SelectedValue = _controller.Settings.AppTheme;
        }
        finally { _updatingTheme = false; }
    }
    private void DarkMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready && !_updatingTheme) ApplySavedTheme(DarkModeToggle.IsChecked == true ? "dark" : "light");
    }
    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && !_updatingTheme && ThemeInput.SelectedValue is string theme) ApplySavedTheme(theme);
    }
    private void ApplySavedTheme(string theme)
    {
        try
        {
            _controller.SetTheme(theme);
            _appearanceError = null;
            WindowTheme.Apply(this, theme);
            SettingsMessage.Text = "Theme saved. It will be remembered next time.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _controller.Store.Log(ex);
            _appearanceError = "Could not save the theme. Check access to the app settings folder and try again.";
            SettingsMessage.Text = _appearanceError;
        }
        finally { SyncThemeControls(); UpdateStatus(); }
    }
    private void UpdatePreview()
    {
        if (!_ready || _allowClose || _shownScene is null) return;
        var stretch = _controller.Settings.WallpaperFit == "fit" ? Stretch.Uniform : Stretch.UniformToFill;
        HeroImage.Stretch = HeroPreviousImage.Stretch = HeroVideo.Stretch = stretch;
        bool canPreview = BayTab.IsSelected && IsVisible && WindowState != WindowState.Minimized && Motion.CanAnimate(this);
        if (!canPreview)
        {
            if (HeroVideo.Source is not null) HeroVideo.Pause();
            if (!Motion.GetEnabled(this) || !SystemParameters.ClientAreaAnimation) HeroVideo.Opacity = 0;
            return;
        }
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Preview", $"day{(int)_shownScene}.mp4");
        if (_previewPath != path)
        {
            HeroVideo.Close(); HeroVideo.Opacity = 0;
            _previewPath = path; _previewFailed = false; _previewReady = false;
            if (!File.Exists(path)) { _previewFailed = true; return; }
            HeroVideo.Source = new Uri(path);
        }
        if (_previewFailed) return;
        HeroVideo.Opacity = _previewReady ? 1 : 0;
        if (_controller.IsPaused) HeroVideo.Pause(); else HeroVideo.Play();
    }
    private void Preview_Opened(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _previewReady = true; _previewFailed = false;
        HeroVideo.Opacity = Motion.CanAnimate(this) ? 1 : 0;
        UpdatePreview();
    }
    private void Preview_Ended(object sender, RoutedEventArgs e)
    {
        HeroVideo.Position = TimeSpan.Zero;
        UpdatePreview();
    }
    private void Preview_Failed(object sender, ExceptionRoutedEventArgs e)
    {
        _previewFailed = true;
        _previewReady = false;
        HeroVideo.Opacity = 0; // Desktop VLC playback is independent; retain the matching still.
    }
    private void Exit_Click(object sender, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();
    private void Link_Navigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); e.Handled = true;
    }
}
