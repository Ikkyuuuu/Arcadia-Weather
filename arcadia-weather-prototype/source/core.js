/* Pure prototype rules. No browser, network, or platform dependencies. */
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.ArcadiaCore = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';
  const REFRESH_MS = 5 * 60 * 1000;
  const SCENES = [
    { id: 1, name: 'Daylight', condition: 'Morning & afternoon', icon: 'sun', alt: 'Sunlight falls across Arcadia Bay, its beach and wooded cliffs.' },
    { id: 2, name: 'Overcast', condition: 'Cloudy skies', icon: 'cloud', alt: 'Clouds gather over the ocean and wooded cliffs of Arcadia Bay.' },
    { id: 3, name: 'Nightfall', condition: 'After dark', icon: 'moon', alt: 'Moonlight reflects on the ocean beside the darkened town of Arcadia Bay.' },
    { id: 4, name: 'Golden hour', condition: 'Evening', icon: 'sunset', alt: 'A golden sunset lights the ocean, beach and lighthouse of Arcadia Bay.' },
    { id: 5, name: 'Storm', condition: 'Rain & thunderstorms', icon: 'storm', alt: 'Heavy rain and dark storm clouds sweep across Arcadia Bay.' }
  ];
  // Representative coordinates and weather are sample fixtures, not live observations.
  const LOCATIONS = [
    { id: 'astoria', name: 'Astoria', region: 'Oregon, United States', lat: 46.1879, lon: -123.8313, kind: 'City' },
    { id: 'seattle', name: 'Seattle', region: 'Washington, United States', lat: 47.6062, lon: -122.3321, kind: 'City' },
    { id: 'portland', name: 'Portland', region: 'Oregon, United States', lat: 45.5152, lon: -122.6784, kind: 'City' },
    { id: 'san-francisco', name: 'San Francisco', region: 'California, United States', lat: 37.7749, lon: -122.4194, kind: 'City' },
    { id: 'bangkok', name: 'Bangkok', region: 'Thailand', lat: 13.7563, lon: 100.5018, kind: 'City', aliases: 'กรุงเทพ กรุงเทพมหานคร' },
    { id: 'bang-rak', name: 'Bang Rak', region: 'Bangkok, Thailand', lat: 13.7300, lon: 100.5230, kind: 'District', aliases: 'บางรัก' },
    { id: 'pathum-wan', name: 'Pathum Wan', region: 'Bangkok, Thailand', lat: 13.7449, lon: 100.5345, kind: 'District', aliases: 'ปทุมวัน' },
    { id: 'chiang-mai', name: 'Chiang Mai', region: 'Thailand', lat: 18.7883, lon: 98.9853, kind: 'City', aliases: 'เชียงใหม่' },
    { id: 'shibuya', name: 'Shibuya', region: 'Tokyo, Japan', lat: 35.6618, lon: 139.7041, kind: 'District', aliases: '渋谷' },
    { id: 'london', name: 'London', region: 'England, United Kingdom', lat: 51.5074, lon: -0.1278, kind: 'City' },
    { id: 'vancouver', name: 'Vancouver', region: 'British Columbia, Canada', lat: 49.2827, lon: -123.1207, kind: 'City' },
    { id: 'paris', name: 'Paris', region: 'France', lat: 48.8566, lon: 2.3522, kind: 'City' }
  ];
  const WEATHER = {
    clear: { label: 'Clear skies', short: 'Clear', temperature: 18, wind: 12, humidity: 68, icon: 'sun' },
    cloudy: { label: 'Cloudy', short: 'Cloudy', temperature: 16, wind: 17, humidity: 76, icon: 'cloud' },
    rain: { label: 'Rain', short: 'Rain', temperature: 13, wind: 21, humidity: 91, icon: 'rain' },
    drizzle: { label: 'Drizzle', short: 'Drizzle', temperature: 14, wind: 11, humidity: 89, icon: 'rain' },
    showers: { label: 'Showers', short: 'Showers', temperature: 15, wind: 24, humidity: 86, icon: 'rain' },
    thunderstorm: { label: 'Thunderstorm', short: 'Thunderstorm', temperature: 12, wind: 38, humidity: 94, icon: 'storm' }
  };
  const RAIN = new Set(['rain', 'drizzle', 'showers', 'thunderstorm']);
  function periodFor(minutes) {
    if (minutes < 360 || minutes >= 1260) return 'Night';
    if (minutes < 720) return 'Morning';
    if (minutes < 1080) return 'Afternoon';
    return 'Evening';
  }
  function automaticScene(weather, minutes) {
    if (RAIN.has(weather)) return 5;
    const period = periodFor(minutes);
    if (period === 'Night') return 3;
    if (weather === 'cloudy') return 2;
    return period === 'Evening' ? 4 : 1;
  }
  function resolveScene(state) { return state.mode === 'manual' ? state.manualScene : automaticScene(state.weather, state.minutes); }
  function selectScene(state, sceneId) {
    if (!SCENES.some(scene => scene.id === sceneId)) throw new RangeError('Unknown scene');
    return { ...state, mode: 'manual', manualScene: sceneId };
  }
  function enableAutomatic(state) { return { ...state, mode: 'automatic' }; }
  function sceneReason(state) {
    if (state.mode === 'manual') return `Day ${state.manualScene} held until Automatic`;
    if (RAIN.has(state.weather)) return `${WEATHER[state.weather].short} overrides all scenes → Day 5`;
    if (periodFor(state.minutes) === 'Night' && state.weather === 'cloudy') return 'Cloudy night stays Day 3';
    return `${WEATHER[state.weather].short} ${periodFor(state.minutes).toLowerCase()} → Day ${resolveScene(state)}`;
  }
  function minutesToTime(minutes) { return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`; }
  function timeToMinutes(text) {
    if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(text)) return null;
    const [hours, minutes] = text.split(':').map(Number);
    return hours * 60 + minutes;
  }
  function parseCoordinates(latInput, lonInput) {
    const asNumber = input => !['string', 'number'].includes(typeof input) || String(input).trim() === '' ? NaN : Number(input);
    const lat = asNumber(latInput);
    const lon = asNumber(lonInput);
    const latValid = Number.isFinite(lat) && lat >= -90 && lat <= 90;
    const lonValid = Number.isFinite(lon) && lon >= -180 && lon <= 180;
    return { valid: latValid && lonValid, latValid, lonValid, lat, lon };
  }
  function coordinateLabel(location) {
    return `${Math.abs(location.lat).toFixed(4)}° ${location.lat < 0 ? 'S' : 'N'}, ${Math.abs(location.lon).toFixed(4)}° ${location.lon < 0 ? 'W' : 'E'}`;
  }
  function searchLocations(query) {
    const normalized = query.trim().toLocaleLowerCase();
    return LOCATIONS.filter(place => `${place.name} ${place.region} ${place.kind} ${place.aliases || ''}`.toLocaleLowerCase().includes(normalized)).slice(0, 8);
  }
  function defaultState() {
    return { mode: 'automatic', manualScene: 1, weather: 'clear', minutes: 980,
      location: { ...LOCATIONS[0] }, volume: 40, muted: true, userPaused: false,
      settings: { display: '1', fit: 'cover', aspect: 'wide', startWithWindows: true, pauseFullscreen: true, reduceMotion: false } };
  }
  function hydrate(saved) {
    const state = defaultState();
    if (!saved || typeof saved !== 'object') return state;
    if (['automatic', 'manual'].includes(saved.mode)) state.mode = saved.mode;
    if (SCENES.some(scene => scene.id === saved.manualScene)) state.manualScene = saved.manualScene;
    if (Object.hasOwn(WEATHER, saved.weather)) state.weather = saved.weather;
    if (Number.isInteger(saved.minutes) && saved.minutes >= 0 && saved.minutes < 1440) state.minutes = saved.minutes;
    if (Number.isFinite(saved.volume) && saved.volume >= 0 && saved.volume <= 100) state.volume = Math.round(saved.volume);
    for (const key of ['muted', 'userPaused']) if (typeof saved[key] === 'boolean') state[key] = saved[key];
    if (saved.location && typeof saved.location === 'object') {
      const match = LOCATIONS.find(place => place.id === saved.location.id);
      const coords = parseCoordinates(saved.location.lat, saved.location.lon);
      if (match) state.location = { ...match };
      else if (saved.location.id === 'custom' && coords.valid) state.location = { id: 'custom', name: 'Custom coordinates', region: 'Manually entered location', lat: coords.lat, lon: coords.lon, kind: 'Coordinates' };
    }
    const settings = saved.settings || {};
    for (const [key, allowed] of Object.entries({ display: ['1', '2', 'all'], fit: ['cover', 'contain', 'fill'], aspect: ['wide', 'ultrawide', 'classic'] })) if (allowed.includes(settings[key])) state.settings[key] = settings[key];
    for (const key of ['startWithWindows', 'pauseFullscreen', 'reduceMotion']) if (typeof settings[key] === 'boolean') state.settings[key] = settings[key];
    return state;
  }
  return { REFRESH_MS, SCENES, LOCATIONS, WEATHER, periodFor, automaticScene, resolveScene, selectScene, enableAutomatic, sceneReason, minutesToTime, timeToMinutes, parseCoordinates, coordinateLabel, searchLocations, defaultState, hydrate };
});
