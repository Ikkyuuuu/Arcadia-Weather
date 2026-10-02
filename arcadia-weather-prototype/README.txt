ARCADIA WEATHER — INTERACTIVE UI PROTOTYPE

Open "Arcadia Weather.html" directly in a current Edge, Chrome, or Firefox.
No installation, local server, internet connection, or account is needed.
The HTML file includes all five short video clips, sound, scene images,
styles, scripts, and icons. It can be moved or shared by itself.

DESIGN
Full-width #36bbd9 navigation, warm off-white canvas, a large cinematic
preview, quiet weather column, and a five-scene collection. Segoe UI uses
the local Windows font, with system fallbacks. No external fonts or libraries.
This design was created from a blank canvas. Only supplied wallpaper media
was inspected or reused; the existing application UI was not inspected.

TRY IT
1. Open "Try sample weather" beneath the Automatic/Manual controls.
2. Set Cloudy and 22:00: Automatic selects Day 3, not Day 2.
3. Set Rain, Drizzle, Showers, or Thunderstorm: Automatic selects Day 5.
4. Choose any scene card, then change weather or time: the chosen scene
   holds in Manual mode. Enable Automatic to follow the rules again.
5. Pause/Play and the sound controls operate the actual embedded clip.
   Playback starts muted. Unmute or change volume to hear its bundled audio.
6. In Settings, search a sample city or district. Try "Bang Rak" or "บางรัก".
   Arrow keys browse suggestions; Enter applies a result; Escape closes it.
7. Edit coordinates and select Apply coordinates. Invalid ranges are rejected.
   "Use my location" applies Astoria's SAMPLE coordinates without GPS access.
8. Change wallpaper fit and preview screen shape to compare Fill, Fit, Stretch.
   Display 1, Display 2, and All displays are simulated, selectable targets.
9. Enable Pause during fullscreen apps, then Simulate fullscreen app.
   Return to Wallpaper to see it paused. Stop the simulation to release it.
   A wallpaper you manually paused stays paused when the simulation stops.

SAMPLE DATA AND RULES
Weather, time, coordinates, cities/districts, and displays are clearly labeled
sample data. Weather values are fixed demonstration fixtures. The selected
local time does not advance, and changing location does not query weather or
convert time zones. A real five-minute timer rereads the selected sample
forecast. Refresh now resets the countdown. Returning after an inactive tab
refreshes overdue data on the next opportunity.

Demonstration time boundaries:
  Day 1: 06:00–17:59 (morning 06:00–11:59, afternoon 12:00–17:59).
  Day 4: 18:00–20:59 (evening).
  Day 3: 21:00–05:59 (night).
  Day 2: cloudy daytime or evening, but never a cloudy night.
  Day 5: rain, drizzle, showers, or thunderstorms at any hour.
  Manual: holds the selected scene until Automatic is explicitly enabled.

PREFERENCES AND ACCESSIBILITY
Preferences and manual scene selection are saved in this browser's local
storage, when allowed for local files. If storage is unavailable, the
prototype continues with preferences for the current session. Reset prototype
restores defaults. Windows startup and display settings do not change Windows.

Standard keyboard operation, visible focus indicators, skip link, labeled
controls, screen-reader announcements, modal focus management, and keyboard
location search are included. Scene cards support Tab/Enter/Space and optional
Left/Right/Home/End navigation. Responsive layouts work down to 320px.
System reduced-motion settings are honored. A Reduce motion setting also
removes transitions and starts each new scene paused; Play remains available
as an explicit choice. Hidden-tab and hidden-view video playback is suspended.

MEDIA
The five original supplied JPGs and videos are the only scene assets used.
Each preview clip is a 12-second excerpt beginning four seconds into the
supplied video. Excerpts are encoded at 960 x 540, 24 fps, H.264/AAC for
portable browser playback. Brief audio fades soften the loop boundary;
the excerpts are not guaranteed to make a visually seamless loop.
If autoplay is blocked, press Play. If video is unsupported or fails,
the matching scene image remains visible with a Retry action.

EDITABLE FILES
  source/index.html        Accessible page structure and inline SVG symbols.
  source/styles.css        Design tokens, components, responsive layouts, motion.
  source/core.js           Pure scene rules, sample fixtures, coordinate validation.
  source/app.js            Playback, UI state, settings, location search, refresh.
  source/assets.js         Local media mapping.
  source/assets/day*.jpg   Five supplied scene images.
  source/assets/day*.mp4   Five shortened, browser-compatible scene clips.
  build.mjs                Embeds sources and assets into the standalone HTML.
  tests/*.test.cjs         Scene-rule and static structure/packaging checks.

Open source/index.html directly to work with the editable version. No server
or module loader is required. After edits, rebuild the standalone file with:
  node build.mjs

Run checks after building:
  node --test tests/rules.test.cjs tests/structure.test.cjs

VALIDATION
JavaScript syntax, all-minute precipitation/cloud rules, time boundaries,
manual holds, persistence sanitization, search, coordinate validation,
structural references, and embedded packaging are checked programmatically.
Video/audio streams are checked with ffprobe. No browser or desktop automation
was used. Visual layout and actual browser interaction are left for your review.

The Windows application has not been modified.
