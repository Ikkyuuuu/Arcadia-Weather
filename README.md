# Arcadia Weather

A Windows 10/11 x64 animated wallpaper app for your five Arcadia Bay videos. It runs in the tray and checks Bangkok weather through Open-Meteo every **five minutes**, including a check at startup and after waking.

## Run

Version 1.5.3 is a **single EXE**. Send only **dist/ArcadiaWeather-1.5.3-single/ArcadiaWeather.exe**. It contains the application, .NET runtime, video player, all five original videos, and license notices. Recipients do not need a ZIP, companion folder, .NET, VLC, or any video setup.

Double-click the EXE on Windows 10/11 x64. The first launch automatically extracts its contents into the current user's .NET cache under `%TEMP%\.net`; allow a little extra time and about 2 GB of free space for this copy, in addition to the EXE. Later launches reuse the extracted files. No administrator permission is required. If the cache is cleared, the next launch recreates it. The app does not need access to the original Pictures/OneDrive video folder and ignores legacy external video paths.

Exit an older running version from its tray menu before opening the new one. Keep the EXE at its final location before enabling Start with Windows; startup uses that EXE, not a temporary extracted copy. Each recipient keeps their own location and preferences locally. Weather requires internet; the included videos play offline.

## Interface and motion (v1.5.3)

The native Windows interface follows the supplied HTML design: a full-width **#36bbd9** navbar with vertically centered Wallpaper and Settings tabs, a large scene preview and five selectable scenes on the left, and location, current weather, and Automatic/Manual controls on the right. The main page fits the available window height without scrolling. The preview takes the remaining space, while smaller windows use compact scene cards and a narrower weather panel. A manual hold explains which scene Automatic would select and offers a return action.

The butterfly supplied by the user appears in the navbar, taskbar, tray, and executable icon. The running window explicitly supplies both native icon sizes and uses a stable Arcadia Weather taskbar identity. The cyan navbar also replaces the separate Windows title bar: drag its empty area to move the window, double-click it to maximize or restore, or use its minimize, maximize/restore, and close buttons. Close continues to hide the window to the tray. Navbar labels and page text use separate colors so both themes remain readable. The mode indicator keeps a fixed width, and the weather panel reserves its manual-hold space to avoid shifts when changing modes. Clicks do not leave focus outlines; keyboard navigation retains a visible focus indicator. Automatic/Manual uses a sliding selection pill, and the dark-mode switch thumb glides between states in 200 ms. Rapid clicks reverse from the current position. Both transitions stop immediately when Windows Animation effects are disabled. The Location, Display, and Windows navigation labels are centered in the settings sidebar.

The preview loops a bundled 12-second H.264 excerpt; the desktop continues to play the original full-length video with its original sound. Preview sound is always muted to avoid doubling desktop audio. Previews pause with the wallpaper and stop animating while hidden, minimized, on another tab, or when Windows Animation effects are off. A matching still remains available if preview playback is unsupported. The existing weather and solar-time rules are unchanged.

Settings is organized into Location, Display, and Windows. It includes real city/district search, editable coordinates, Windows location access, Celsius/Fahrenheit, Fill/Fit, and System/Light/Dark themes. The navbar's **Dark mode** toggle applies and saves the choice immediately, so the next launch restores it. The Settings theme picker also saves immediately; **System** follows Windows until an explicit Light or Dark choice is made. Other display changes apply with **Save settings**. The app uses local Segoe UI fonts, the Windows fallback specified in the HTML, without loading Google Fonts.

**Save settings** remains visible while the form scrolls. Unsaved edits survive tab changes and hiding the tray window. **Revert** discards the draft; it leaves the already-saved theme in place. Closing the application discards unsaved edits. The HTML's sample controls and fictional display data are replaced by the app's real services and connected-display selector; this release continues to target one chosen monitor.

## Use your computer's location

In **Settings**, click **Use my location**. The app requests permission from Windows, obtains a recent position, and automatically fills latitude and longitude. It also looks up the coordinate's timezone through Open-Meteo. Click **Save settings** to apply the filled location to your wallpaper.

Windows uses GPS/satellite positioning when supported, or available Wi-Fi, cellular, IP, or default-location information. The app displays the reported source and accuracy, including broad estimates; it does not promise GPS precision on a computer without GPS hardware. This is a one-time foreground lookup, not background location tracking. Location is never requested at startup or during five-minute weather checks.

If Windows denies access or Location services are disabled, the app provides a **Windows location settings** button. Enable the relevant location access yourself and try again. The app never changes Windows privacy settings or bypasses consent. A position lookup waits up to 30 seconds and can be cancelled. Invalid or old readings are rejected. If the timezone lookup is offline, coordinates are still filled; check the displayed computer timezone before saving. Manual coordinate entry and city search remain available.

Using this button sends the detected coordinates to Open-Meteo to resolve the timezone. Saving stores the coordinates locally and uses them for subsequent weather requests. Location names are set to **My current location**; no reverse-geocoding/district-name service is contacted.

## Rules, highest priority first

| Condition | Video |
|---|---|
| Rain, drizzle, rain showers, freezing rain/drizzle, or thunderstorms | Day 5 at any hour |
| Night without rain | Day 3, even when cloudy |
| Cloudy daylight or evening | Day 2 |
| Evening without rain or clouds | Day 4 |
| Morning/afternoon without rain or clouds | Day 1 |

Day begins at sunrise. Evening starts 60 minutes before sunset. Night starts 30 minutes after sunset. All times use the selected location's timezone. Clouds enter at 70% and clear at 55%, preventing flicker near a single threshold. Settings exposes both thresholds and sunset offsets. Rain also triggers if current `rain` or `showers` is above zero; snowfall alone is not classified as rain.

The selected video loops without being restarted on each weather check. A manual scene selection holds until **Automatic weather** is selected; this override intentionally wins over weather while active and resets on app restart. Sound starts muted; the sound button and slider control the original audio. Playback pauses on lock/sleep and, by default, when another fullscreen app covers the selected display. This version plays on one chosen monitor, with primary-monitor fallback if disconnected.

Closing the window keeps the tray app running. Double-click the tray icon to reopen. Use its **Exit** menu to stop the wallpaper and reveal your existing Windows background. Startup is opt-in and can be disabled in Settings. A second launch opens the existing instance.

## Weather and offline behavior

Open-Meteo's free endpoint is for personal/noncommercial use without an API key. One five-minute request is about 288 calls per 24 hours, below its 10,000/day free limit. The data are weather-model estimates, not a live rain sensor; polling every five minutes does not guarantee newly published data each time. Attribution appears in the app. Commercial API usage needs the appropriate Open-Meteo subscription.

The app sends only the selected coordinates and requested weather fields to `api.open-meteo.com`. City search sends the entered city name to `geocoding-api.open-meteo.com`. Videos stay local. The app itself has no analytics or account system.

An unsuccessful update keeps the last valid weather sample until it is one hour old (checking both retrieval and observation times), then falls back to time of day. Cached sunrise/sunset is used only for its matching date/location. If unavailable, the local fallback schedule is 06:00 day, 17:00 evening, 19:00 night. No weather cache from another location is used. Time rules are reevaluated every two seconds independently of weather polling.

Settings, cached weather, and rotating diagnostic logs live in `%LOCALAPPDATA%\ArcadiaWeather`. The app tolerates missing/corrupt settings and provides a clear message for missing videos or desktop-layer failures. Playback is reattached after display changes or a destroyed Explorer host. Desktop wallpaper hosting relies on Windows shell behavior, so future shell updates may need compatibility adjustments.

## Build and test

Requires a .NET 10 SDK on Windows x64. This workspace also supports an SDK at `.tools/dotnet/dotnet.exe`. Package versions are pinned in the project.

The repository contains the Windows app source, tests, preview clips, scene images, and editable HTML prototypes. Generated Windows executables, local SDK/package caches, and the five full-length wallpaper videos are not tracked. To build from a fresh clone, supply your own five original videos in a folder with filenames `Arcadia Bay Day 1 Sound AAC.mp4` through `Arcadia Bay Day 5 Sound AAC.mp4`, then run:

```powershell
.\build.ps1 -VideoSource "C:\path\to\the\five\videos" -SingleExe
```

This runs the tests and publishes exactly one EXE into `dist/ArcadiaWeather-1.5.3-single`. The bundled .NET 10 runtime extracts all content before starting the app, preserving the directory structure needed by LibVLC plugins. `AppContext.BaseDirectory` points to this extracted content in that publish mode; `Environment.ProcessPath` remains the distributable EXE. A source-video SHA-256 manifest is embedded for verification. `-VideoSource` is a build-time input only and is not saved in recipient settings.

For a conventional folder build, omit `-SingleExe`; add `-CreateArchive` for an optional ZIP. That alternative requires keeping its output folder together.

```powershell
dotnet run --project tests/ArcadiaWeather.Tests -c Release
dotnet run --project tests/ArcadiaWeather.Preferences.Tests -c Release -- artifacts/theme-preference-tests
# Isolated end-to-end check; briefly renders all five scenes on the desktop:
.\dist\ArcadiaWeather-1.5.3-single\ArcadiaWeather.exe --data-dir "$PWD\artifacts\smoke-data" --smoke-test "$PWD\artifacts\smoke"
# Headless packaging check: no UI, playback, weather requests, or user-settings changes:
.\dist\ArcadiaWeather-1.5.3-single\ArcadiaWeather.exe --verify-bundle "$PWD\artifacts\bundle-verification.json"
```

The preference tests verify immediate saving, restoration on restart, preservation of other settings, and failed-write recovery without creating windows, playing videos, or accessing weather/location services. The smoke test verifies live weather, HEVC playback, decoded frames, desktop parenting, looping, pause/resume, and reattachment. It saves JSON results and UI renders. It closes its player when complete. Do not run it alongside another wallpaper engine. `--tray` starts with the settings window hidden; `--data-dir` is useful for isolated testing.

Source layout: `ArcadiaWeather.Core` contains the rules and API parsing, `ArcadiaWeather` contains WPF settings/status, tray integration, and the Win32/LibVLC player. Tests have no test framework dependencies. `arcadia-weather-prototype` and `ui-draft` contain the earlier HTML design prototypes; the current application UI is in `src/ArcadiaWeather`.

The application source uses the repository's MIT license. Third-party libraries and Life is Strange artwork/media retain their respective licenses and ownership. This is an unofficial personal project using user-provided Life is Strange footage. This personal bundle includes the five videos supplied by the user. See `THIRD-PARTY-NOTICES.txt` for runtime and weather credits.
