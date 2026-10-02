# Development Guide

The [README](../README.md) covers the app, scenes, and basic setup. This guide keeps the build, testing, and runtime details in one place.

## Build and Package

Build on Windows x64 with PowerShell and a .NET 10 SDK. The build script uses `.tools/dotnet/dotnet.exe` when present, otherwise `dotnet` from your PATH. Package versions are pinned in the project.

From the repository root, supply the five original videos using the filenames listed in the README:

```powershell
.\build.ps1 -VideoSource "C:\path\to\the\five\videos" -SingleExe
```

The script runs the rule and preference tests, creates a SHA-256 manifest for the source videos, and publishes one EXE to `dist/ArcadiaWeather-1.5.3-single`. The EXE includes the .NET runtime, libVLC and its plugins, videos, and license notices. It does not need an installed copy of .NET or VLC.

`-VideoSource` is a build input only. The app resolves bundled media and ignores legacy external video paths in saved settings. Generated executables, full-length videos, local SDKs, package caches, and test artifacts are excluded from Git. The short preview clips and scene images are tracked.

To produce a folder distribution, omit `-SingleExe`. Add `-CreateArchive` to that folder build for an optional ZIP. Keep the whole output folder together when sharing it. Use `-OutputDirectory` to choose another destination; a single-EXE build should use a fresh output directory.

### Single-EXE Extraction

On first launch, .NET extracts the bundled content under the current user's `%TEMP%\.net` cache, preserving the directory layout needed by libVLC plugins. Allow about 2 GB of additional free space beyond the EXE. Later launches reuse the cache; clearing it causes the next launch to extract again. No administrator permission is required.

In this mode, `AppContext.BaseDirectory` points to the extracted content and `Environment.ProcessPath` points to the distributable EXE. Windows startup uses the latter, so move the EXE to its final location before enabling startup.

## Tests

Run these commands from the repository root:

```powershell
dotnet run --project tests/ArcadiaWeather.Tests -c Release
dotnet run --project tests/ArcadiaWeather.Preferences.Tests -c Release -- artifacts/theme-preference-tests
```

If you use the local SDK, replace `dotnet` with `.\.tools\dotnet\dotnet.exe`. The test projects do not use a separate test framework.

The preference tests check immediate theme saving, restoration on restart, preservation of other settings, and failed-write recovery. They do not create windows, play videos, or access weather/location services.

After packaging, verify the embedded files with:

```powershell
.\dist\ArcadiaWeather-1.5.3-single\ArcadiaWeather.exe --verify-bundle "$PWD\artifacts\bundle-verification.json"
```

This check creates a JSON report without opening a UI, starting playback, requesting weather, or changing user settings.

### Optional Desktop Smoke Test

This test opens the app, briefly renders all five scenes on the desktop, contacts the weather service, and saves JSON results and UI renders. Run it only when you intend to exercise the desktop wallpaper and no other wallpaper engine is active:

```powershell
.\dist\ArcadiaWeather-1.5.3-single\ArcadiaWeather.exe --data-dir "$PWD\artifacts\smoke-data" --smoke-test "$PWD\artifacts\smoke"
```

It checks live weather, HEVC playback, decoded frames, desktop parenting, looping, pause/resume, and reattachment, then closes its player. `--data-dir` isolates settings and cache files for testing. `--tray` starts the app with its main window hidden.

## Scene Rules

Manual selection takes priority over automatic rules for the current session. It ends when Automatic is selected or the app restarts.

Automatic uses this order:

1. Rain, drizzle, freezing rain/drizzle, rain showers, or thunderstorms select Day 5 at any hour. A positive current `rain` or `showers` value also triggers it. Snowfall alone does not.
2. Night selects Day 3, regardless of cloud cover.
3. Cloudy daylight or evening selects Day 2.
4. Clear evening selects Day 4.
5. Clear morning/afternoon selects Day 1.

The default cloudy threshold is **70%**. After entering the cloudy state, it clears at **55%** or lower to avoid repeatedly changing scenes near one threshold. Day starts at sunrise, evening starts 60 minutes before sunset, and night starts 30 minutes after sunset. The thresholds and sunset offsets are editable in Settings.

All time rules use the selected location's timezone and are reevaluated every two seconds. A five-minute weather check does not restart the currently playing video when the scene is unchanged.

### Weather and Offline Behavior

Weather is requested at startup, every five minutes, and after waking. If an update fails, the last valid sample is usable for up to one hour, checking both its retrieval and observation times. Older weather falls back to the time-of-day rules.

Cached sunrise/sunset remains usable only for the matching date and location. When it is unavailable, the default local schedule is 06:00 for day, 17:00 for evening, and 19:00 for night. Weather cached for another location is not used.

The app uses Open-Meteo without an API key. See the provider's [terms](https://open-meteo.com/en/terms) for current usage conditions and limits, and the [third-party notices](../THIRD-PARTY-NOTICES.txt) for attribution. Weather values are model estimates rather than a local rain sensor.

## Location and Privacy

City/district search sends the search text to `geocoding-api.open-meteo.com`. Weather and timezone requests send the selected coordinates to `api.open-meteo.com`. Videos remain local. The app has no analytics or account system.

**Use my location** requests Windows permission for a one-time foreground lookup. Windows may use GPS, Wi-Fi, cellular, IP, or default-location information depending on the device and available services. The app displays the reported source and accuracy; it does not assume GPS precision.

The lookup can be cancelled, waits up to 30 seconds, and rejects invalid or old readings. It fills latitude/longitude and requests the timezone from Open-Meteo. If that request fails, the coordinates are still filled and the user should check the displayed computer timezone before saving. The name becomes **My current location**; no reverse-geocoding service is called.

**Save settings** applies the location and stores it locally for subsequent weather checks. Location access is never requested automatically at startup or during those checks. If access is denied, the app offers a link to Windows location settings; it does not alter privacy settings itself.

## Interface, Settings, and Playback

The WPF app uses local Segoe UI fonts and a full-width `#36bbd9` navbar. The navbar also acts as the title bar: drag empty space to move the window, or double-click it to maximize/restore. The butterfly is used for the app, taskbar, and tray icons.

The main page adjusts the preview and scene cards to the available window space. Mode controls reserve space so switching Automatic/Manual does not shift the weather panel. Keyboard navigation shows focus indicators. The mode pill and theme switch use 200 ms transitions, reversing smoothly when clicked again; transitions stop when Windows Animation effects are disabled.

The preview loops a bundled 12-second H.264 excerpt. It is always muted so it does not double the full-length desktop video's audio. Preview animation stops when paused, hidden, minimized, on another tab, or when Windows Animation effects are disabled. A still image is available if preview playback is unsupported.

The navbar theme switch and Settings theme picker save immediately. **System** follows Windows; choosing **Light** or **Dark** persists that explicit choice. Other settings are drafts until **Save settings**. Drafts survive tab changes and hiding the window. **Revert** discards the draft while keeping the already-saved theme; exiting the app also discards unsaved edits.

Audio is muted by default on a fresh install, with a default volume of 25. Mute and volume preferences are saved. Playback pauses on lock/sleep and, by default, when another fullscreen app covers the selected monitor. Only one monitor is targeted; disconnecting it falls back to the primary display.

Closing the window hides it to the tray. Double-click the tray icon to reopen it. **Exit** stops playback and reveals the existing Windows background. Windows startup is opt-in. A second launch opens the existing instance.

## Data and Troubleshooting

Settings, cached weather, and rotating logs are stored in `%LOCALAPPDATA%\ArcadiaWeather`, unless `--data-dir` overrides the directory. Preferences belong to the current Windows user.

The app handles missing/corrupt settings and reports missing videos or desktop-host failures. It attempts to reattach playback after display changes or a destroyed Explorer host. Wallpaper hosting depends on Windows shell behavior, so future shell updates may require compatibility adjustments.

For location failures, check the Windows permission and reported accuracy, or use city search/manual coordinates. For startup failures after moving an EXE, open it from the final location and update the startup setting. Exit an older running copy from its tray menu before launching a new version.

## Source Layout

| Folder | Purpose |
| --- | --- |
| [`src/ArcadiaWeather.Core`](../src/ArcadiaWeather.Core) | Scene rules, weather/API parsing, location models, and settings models |
| [`src/ArcadiaWeather`](../src/ArcadiaWeather) | WPF interface, Windows integration, tray, and Win32/libVLC playback |
| [`tests`](../tests) | Rule and preference regression checks |
| [`arcadia-weather-prototype`](../arcadia-weather-prototype) | Standalone HTML prototype and editable source |
| [`ui-draft`](../ui-draft) | Earlier HTML design draft |
| [`Licenses`](../Licenses) | Third-party license texts |

The current Windows interface lives in `src/ArcadiaWeather`. Editing an HTML prototype does not change the native app.
