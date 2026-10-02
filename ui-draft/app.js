/* Offline UI draft: sample data only. No GPS, network, media, or desktop access. */
'use strict';
const scenes = { 1: 'Daylight', 2: 'Cloudy', 3: 'Night', 4: 'Evening', 5: 'Rain' };
const scenarios = {
  day: { cloud: 20, rain: 0, code: 0, temperature: 32, phase: 'morning', condition: 'Clear', icon: 'sun' },
  afternoon: { cloud: 25, rain: 0, code: 1, temperature: 33, phase: 'afternoon', condition: 'Mostly clear', icon: 'sun' },
  evening: { cloud: 32, rain: 0, code: 2, temperature: 29, phase: 'evening', condition: 'Partly cloudy', icon: 'sun' },
  night: { cloud: 15, rain: 0, code: 0, temperature: 26, phase: 'night', condition: 'Clear', icon: 'moon' },
  clouds: { cloud: 90, rain: 0, code: 3, temperature: 28, phase: 'afternoon', condition: 'Cloudy', icon: 'cloud' },
  'evening-clouds': { cloud: 90, rain: 0, code: 3, temperature: 27, phase: 'evening', condition: 'Cloudy', icon: 'cloud' },
  'night-clouds': { cloud: 90, rain: 0, code: 3, temperature: 26, phase: 'night', condition: 'Cloudy', icon: 'cloud' },
  rain: { cloud: 98, rain: 3.2, code: 63, temperature: 25, phase: 'afternoon', condition: 'Rain', icon: 'rain' },
  // Zero measured precipitation still selects rain when a wet weather code is present.
  drizzle: { cloud: 85, rain: 0, code: 51, temperature: 26, phase: 'morning', condition: 'Drizzle', icon: 'rain' },
  showers: { cloud: 96, rain: 2.1, code: 80, temperature: 25, phase: 'evening', condition: 'Showers', icon: 'rain' },
  storm: { cloud: 100, rain: 12.4, code: 95, temperature: 24, phase: 'night', condition: 'Thunderstorm', icon: 'rain' },
};
// Coordinates and solar times are illustrative fixtures, not current observations.
const bangkokSample = { country: 'Thailand', timezone: 'Asia/Bangkok', sunrise: '06:09', sunset: '18:08' };
const places = [
  { ...bangkokSample, name: 'Bangkok', lat: 13.7563, lon: 100.5018 },
  { ...bangkokSample, name: 'Pathum Wan, Bangkok', lat: 13.7449, lon: 100.5331 },
  { ...bangkokSample, name: 'Watthana, Bangkok', lat: 13.7425, lon: 100.5856 },
  { ...bangkokSample, name: 'Bang Rak, Bangkok', lat: 13.7279, lon: 100.5241 },
  { ...bangkokSample, name: 'Chatuchak, Bangkok', lat: 13.8285, lon: 100.5598 },
  { name: 'Chiang Mai', country: 'Thailand', lat: 18.7883, lon: 98.9853, timezone: 'Asia/Bangkok', sunrise: '06:15', sunset: '18:12' },
  { name: 'Portland, Oregon', country: 'United States', lat: 45.5152, lon: -122.6784, timezone: 'America/Los_Angeles', sunrise: '07:08', sunset: '18:48' },
];
const wetCodes = new Set([51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82, 95, 96, 97, 99]);
function minutesOf(time) {
  const [hours, minutes] = time.split(':').map(Number);
  return hours * 60 + minutes;
}
function clockTime(minutes) {
  const value = ((Math.round(minutes) % 1440) + 1440) % 1440;
  return String(Math.floor(value / 60)).padStart(2, '0') + ':' + String(value % 60).padStart(2, '0');
}
function sampleWeather(scenario, place) {
  const weather = scenarios[scenario];
  const sunrise = minutesOf(place.sunrise);
  const sunset = minutesOf(place.sunset);
  const times = {
    morning: sunrise + 90,
    afternoon: sunrise + (sunset - sunrise) * .65,
    evening: sunset - 26,
    night: Math.min(1439, sunset + 180),
  };
  return { ...weather, time: clockTime(times[weather.phase]) };
}
function nextCloudState(cloud, wasCloudy, enter, exit) {
  return wasCloudy ? cloud > exit : cloud >= enter;
}
function chooseAutomaticScene(weather, place, timing, cloudy) {
  if (wetCodes.has(weather.code) || weather.rain > 0) return 5;
  const time = minutesOf(weather.time);
  const sunrise = minutesOf(place.sunrise);
  const sunset = minutesOf(place.sunset);
  if (time < sunrise || time >= sunset + timing.night) return 3;
  if (cloudy) return 2;
  return time >= Math.max(sunrise, sunset - timing.evening) ? 4 : 1;
}

// UI bindings. State is deliberately session-only so a reload resets the design.
const $ = selector => document.querySelector(selector);
const $$ = selector => [...document.querySelectorAll(selector)];
const state = {
  scene: 4, automatic: true, paused: false, muted: true, volume: 35,
  scenario: 'evening', place: { ...places[0] },
  cloudy: false, cloudEnter: 70, cloudExit: 55, evening: 60, night: 30,
};
const settingsIds = ['district', 'location-name', 'latitude', 'longitude', 'timezone',
  'display', 'startup', 'fullscreen', 'cloud-enter', 'cloud-exit', 'evening', 'night'];
function readSettingsFields() {
  return Object.fromEntries(settingsIds.map(id => {
    const field = document.getElementById(id);
    return [id, field.type === 'checkbox' ? field.checked : field.value];
  }));
}
let savedFields = readSettingsFields();
let chosenQuery = savedFields.district;
let unresolvedSearch = false;
let toastTimer;
let locateTimer;
let lastSceneAnnouncement = '';
function notify(message) {
  clearTimeout(toastTimer);
  $('#toast').textContent = message;
  $('#toast').classList.add('show');
  toastTimer = setTimeout(() => $('#toast').classList.remove('show'), 3200);
}
function setIcon(element, name) { element.querySelector('use').setAttribute('href', '#i-' + name); }
function automaticScene() {
  return chooseAutomaticScene(sampleWeather(state.scenario, state.place), state.place, state, state.cloudy);
}
function renderScene() {
  $$('[data-scene-image]').forEach(image => {
    const selected = Number(image.dataset.sceneImage) === state.scene;
    image.classList.toggle('visible', selected);
    image.setAttribute('aria-hidden', String(!selected));
  });
  $$('.scene-card').forEach(button => {
    const selected = Number(button.dataset.scene) === state.scene;
    button.classList.toggle('selected', selected);
    button.setAttribute('aria-pressed', String(selected));
    button.setAttribute('aria-label', 'Day ' + button.dataset.scene + ', ' + scenes[button.dataset.scene]);
    button.title = 'Hold ' + scenes[button.dataset.scene] + ' manually';
  });
  const day = 'Day ' + String(state.scene).padStart(2, '0');
  $('#scene-title').textContent = scenes[state.scene];
  $('#scene-meta').textContent = day;
  $('#manual-label').hidden = state.automatic;
  $('#automatic-toggle').checked = state.automatic;
  $('#scene-detail').textContent = !state.automatic ? 'Held until Automatic resumes'
    : state.scene === 3 ? 'Sunrise ' + state.place.sunrise + ' · local time'
    : state.scene === 5 ? 'Wet weather · all-day override'
    : state.scene === 2 ? 'Cloud cover · daylight & evening'
    : 'Sunset ' + state.place.sunset + ' · local time';
  $('#sample-scene').textContent = scenes[state.scene] + ' · ' + day + (state.automatic ? '' : ' · Manual');
  $('#resume-automatic').hidden = state.automatic;
  const announcement = scenes[state.scene] + ', ' + day + '. ' + (state.automatic ? 'Automatic.' : 'Held manually.');
  if (announcement !== lastSceneAnnouncement) {
    $('#scene-status').textContent = announcement;
    lastSceneAnnouncement = announcement;
  }
}
function renderWeather() {
  const weather = sampleWeather(state.scenario, state.place);
  state.cloudy = nextCloudState(weather.cloud, state.cloudy, state.cloudEnter, state.cloudExit);
  $('#temperature').textContent = weather.temperature + '°';
  $('#weather-condition').textContent = weather.condition;
  $('.weather-button').setAttribute('aria-label', 'Sample weather: ' + weather.temperature + ' degrees Celsius, ' + weather.condition);
  $('#weather-time').textContent = weather.time;
  $('#cloud-cover').textContent = weather.cloud + '%';
  $('#precipitation').textContent = weather.rain + ' mm';
  $('#sunrise-time').textContent = state.place.sunrise;
  $('#sunset-time').textContent = state.place.sunset;
  $('#sample-location').textContent = state.place.name;
  $('#sample-timing-note').textContent = state.place.custom
    ? 'Custom coordinates use sample sunrise 06:00 and sunset 18:00 in this offline preview.'
    : 'Illustrative local times for ' + state.place.name + '.';
  setIcon($('#weather-icon'), weather.icon);
  if (state.automatic) state.scene = automaticScene();
  renderScene();
}
function updateMotion() {
  const paused = document.hidden || state.paused || $$('dialog[open]').length > 0;
  $('#hero-images').style.animationPlayState = paused ? 'paused' : 'running';
}
function cancelLocate() {
  clearTimeout(locateTimer);
  $('#locate-button').disabled = false;
  $('#locate-button span').textContent = 'Use my location';
  $('#settings-form button[type="submit"]').disabled = false;
}
function resetSettingsDraft() {
  cancelLocate();
  settingsIds.forEach(id => {
    const field = document.getElementById(id);
    if (field.type === 'checkbox') field.checked = savedFields[id];
    else field.value = savedFields[id];
    field.removeAttribute('aria-invalid');
  });
  chosenQuery = savedFields.district;
  unresolvedSearch = false;
  $('#location-results').hidden = true;
  $('#location-status').hidden = true;
  $('#save-status').textContent = 'Saved for this session only.';
}
function openDialog(id, focusId) {
  $('#sound-control').open = false;
  $$('dialog[open]').forEach(dialog => dialog.close());
  if (id === 'settings-dialog') resetSettingsDraft();
  document.getElementById(id).showModal();
  if (focusId) {
    document.getElementById(focusId).focus();
    if (focusId === 'district') $('#district').select();
  }
  document.body.classList.add('modal-open');
  updateMotion();
}
$$('[data-open]').forEach(button => button.addEventListener('click', () => openDialog(button.dataset.open, button.dataset.focus)));
$$('[data-close]').forEach(button => button.addEventListener('click', () => button.closest('dialog').close()));
$$('dialog').forEach(dialog => {
  dialog.addEventListener('close', () => {
    if (dialog.id === 'settings-dialog') cancelLocate();
    document.body.classList.toggle('modal-open', $$('dialog[open]').length > 0);
    updateMotion();
  });
  let backdropPressed = false;
  function outside(event) {
    const bounds = dialog.getBoundingClientRect();
    return event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom;
  }
  dialog.addEventListener('pointerdown', event => { backdropPressed = event.target === dialog && outside(event); });
  dialog.addEventListener('click', event => {
    if (backdropPressed && event.target === dialog && outside(event)) dialog.close();
    backdropPressed = false;
  });
});
const sceneButtons = $$('.scene-card');
sceneButtons.forEach((button, index) => {
  button.addEventListener('click', () => {
    state.scene = Number(button.dataset.scene);
    state.automatic = false;
    renderScene();
  });
  button.addEventListener('keydown', event => {
    const targets = { ArrowRight: (index + 1) % 5, ArrowLeft: (index + 4) % 5, Home: 0, End: 4 };
    if (!(event.key in targets)) return;
    event.preventDefault();
    sceneButtons[targets[event.key]].focus();
  });
});
function setAutomatic(automatic) {
  state.automatic = automatic;
  if (automatic) state.scene = automaticScene();
  renderScene();
}
$('#automatic-toggle').addEventListener('change', event => setAutomatic(event.target.checked));
$('#resume-automatic').addEventListener('click', () => {
  setAutomatic(true);
  $('#scenario-select').focus();
});
$('#pause-button').addEventListener('click', () => {
  state.paused = !state.paused;
  updateMotion();
  $('#paused-label').hidden = !state.paused;
  setIcon($('#pause-button'), state.paused ? 'play' : 'pause');
  $('#pause-button').setAttribute('aria-label', state.paused ? 'Resume preview motion' : 'Pause preview motion');
  $('#pause-button').setAttribute('aria-pressed', String(state.paused));
  $('#pause-button').title = state.paused ? 'Resume' : 'Pause';
});
function renderSound() {
  const silent = state.muted || state.volume === 0;
  setIcon($('#sound-icon'), silent ? 'muted' : 'volume');
  $('#sound-control summary').setAttribute('aria-label', 'Sound controls, ' + (silent ? 'muted' : state.volume + ' percent'));
  $('#mute-button').textContent = silent ? 'Off' : 'On';
  $('#mute-button').setAttribute('aria-pressed', String(silent));
  $('#volume').value = state.volume;
  $('#volume').style.setProperty('--level', state.volume + '%');
  $('#volume').setAttribute('aria-valuetext', state.volume + ' percent' + (silent ? ', muted' : ''));
  $('#volume-value').textContent = state.volume + '%';
}
$('#mute-button').addEventListener('click', () => {
  const silent = state.muted || state.volume === 0;
  state.muted = !silent;
  if (silent && state.volume === 0) state.volume = 35;
  renderSound();
});
$('#volume').addEventListener('input', event => {
  state.volume = Number(event.target.value);
  state.muted = state.volume === 0;
  renderSound();
});
$('#theme-button').addEventListener('click', () => {
  const dark = document.documentElement.dataset.theme !== 'dark';
  document.documentElement.dataset.theme = dark ? 'dark' : 'light';
  setIcon($('#theme-button'), dark ? 'sun' : 'moon');
  $('#theme-button').setAttribute('aria-label', 'Switch to ' + (dark ? 'light' : 'dark') + ' theme');
  $('#theme-button').title = dark ? 'Switch to light theme' : 'Switch to dark theme';
});
$('#scenario-select').addEventListener('change', event => {
  state.scenario = event.target.value;
  // Each scenario is an independent sample; live refreshes retain hysteresis.
  state.cloudy = false;
  renderWeather();
  $('#refresh-status').textContent = 'Offline sample · No live connection';
});
$('#refresh-button').addEventListener('click', () => {
  const button = $('#refresh-button');
  button.disabled = true;
  button.classList.add('spinning');
  $('#refresh-status').textContent = 'Refreshing sample…';
  setTimeout(() => {
    renderWeather();
    button.disabled = false;
    button.classList.remove('spinning');
    $('#refresh-status').textContent = 'Sample refreshed · No live connection';
  }, 600);
});
function markUnsaved(event) {
  event?.target?.removeAttribute('aria-invalid');
  $('#save-status').textContent = 'Unsaved changes';
}
function fillPlace(place) {
  $('#location-name').value = place.name;
  $('#latitude').value = place.lat;
  $('#longitude').value = place.lon;
  $('#timezone').value = place.timezone;
  $('#district').value = place.name;
  chosenQuery = place.name;
  unresolvedSearch = false;
  $('#location-results').hidden = true;
  $$('#coordinates-details input').forEach(field => field.removeAttribute('aria-invalid'));
  $('#district').removeAttribute('aria-invalid');
  markUnsaved();
}
function searchPlaces() {
  const results = $('#location-results');
  const query = $('#district').value.trim().toLowerCase();
  const matches = places.filter(place => place.name.toLowerCase().includes(query));
  results.replaceChildren();
  results.hidden = false;
  if (!matches.length) {
    const hint = document.createElement('p');
    hint.className = 'no-results';
    hint.textContent = 'Try Bangkok, a Bangkok district, Chiang Mai or Portland. You can also enter coordinates below.';
    results.append(hint);
  }
  matches.forEach(place => {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'location-result';
    button.append(document.createTextNode(place.name));
    button.append(Object.assign(document.createElement('span'), { textContent: place.country + ' · Sample result' }));
    button.addEventListener('click', () => {
      fillPlace(place);
      $('#location-status').hidden = false;
      $('#location-status').textContent = place.name + ' selected. Save to apply.';
      $('#district').focus();
    });
    results.append(button);
  });
}
$('#district').addEventListener('input', () => {
  unresolvedSearch = $('#district').value.trim() !== chosenQuery;
  $('#location-status').hidden = true;
  searchPlaces();
});
$('#district').addEventListener('keydown', event => {
  if (event.key === 'Escape' && !$('#location-results').hidden) {
    event.preventDefault();
    event.stopPropagation();
    $('#location-results').hidden = true;
  }
  if (event.key === 'ArrowDown' || event.key === 'Enter') {
    event.preventDefault();
    searchPlaces();
    $('#location-results button')?.focus();
  }
});
$('#location-results').addEventListener('keydown', event => {
  const buttons = $$('#location-results button');
  const index = buttons.indexOf(document.activeElement);
  if (event.key === 'Escape') {
    event.preventDefault();
    event.stopPropagation();
    $('#location-results').hidden = true;
    $('#district').focus();
  } else if (event.key === 'ArrowUp' || event.key === 'ArrowDown') {
    event.preventDefault();
    const next = index + (event.key === 'ArrowDown' ? 1 : -1);
    if (next < 0) $('#district').focus();
    else buttons[Math.min(next, buttons.length - 1)]?.focus();
  }
});
// Editing coordinates is an explicit alternative to selecting a search result.
$$('#coordinates-details input').forEach(field => field.addEventListener('input', () => {
  $('#district').value = $('#location-name').value;
  chosenQuery = $('#district').value.trim();
  unresolvedSearch = false;
  $('#location-results').hidden = true;
  $('#location-status').hidden = true;
}));
document.addEventListener('click', event => {
  if (!event.target.closest('#district') && !event.target.closest('#location-results')) $('#location-results').hidden = true;
  if (!event.target.closest('#sound-control')) $('#sound-control').open = false;
});
$('#sound-control').addEventListener('focusout', event => {
  if (event.relatedTarget && !$('#sound-control').contains(event.relatedTarget)) $('#sound-control').open = false;
});
document.addEventListener('keydown', event => {
  if (event.key === 'Escape' && $('#sound-control').open) {
    event.preventDefault();
    $('#sound-control').open = false;
    $('#sound-control summary').focus();
  }
});
$('#locate-button').addEventListener('click', () => {
  const button = $('#locate-button');
  button.disabled = true;
  $('#settings-form button[type="submit"]').disabled = true;
  button.querySelector('span').textContent = 'Locating…';
  $('#location-status').hidden = false;
  $('#location-status').textContent = 'Simulating Windows location autofill…';
  locateTimer = setTimeout(() => {
    fillPlace(places[1]);
    $('#coordinates-details').open = true;
    cancelLocate();
    $('#location-status').textContent = 'Sample coordinates filled. No location access requested. Windows GPS availability and accuracy depend on your device.';
  }, 800);
});
function fieldError(field, message) {
  $('#save-status').textContent = message;
  field.setAttribute('aria-invalid', 'true');
  let ancestor = field.parentElement;
  while (ancestor) {
    if (ancestor.tagName === 'DETAILS') ancestor.open = true;
    ancestor = ancestor.parentElement;
  }
  field.focus();
}
$('#settings-form').addEventListener('input', markUnsaved);
$('#settings-form').addEventListener('change', markUnsaved);
$('#settings-form').addEventListener('submit', event => {
  event.preventDefault();
  // Validate after opening collapsed sections so every error can be reached.
  const invalid = $$('#settings-form input, #settings-form select').find(field => !field.validity.valid);
  if (invalid) { fieldError(invalid, invalid.validationMessage); return; }
  if (unresolvedSearch) {
    searchPlaces();
    fieldError($('#district'), 'Choose a sample result or enter coordinates before saving.');
    return;
  }
  const enter = Number($('#cloud-enter').value);
  const exit = Number($('#cloud-exit').value);
  if (exit >= enter) { fieldError($('#cloud-exit'), 'Cloud clearing must be below the entry threshold.'); return; }
  const timezone = $('#timezone').value.trim();
  try { new Intl.DateTimeFormat('en', { timeZone: timezone }); }
  catch { fieldError($('#timezone'), 'Enter a valid timezone, such as Asia/Bangkok.'); return; }
  const name = $('#location-name').value.trim();
  if (!name) { fieldError($('#location-name'), 'Enter a location name.'); return; }
  const lat = Number($('#latitude').value);
  const lon = Number($('#longitude').value);
  const samplePlace = places.find(place => Math.abs(place.lat - lat) < .0001 && Math.abs(place.lon - lon) < .0001 && place.timezone === timezone);
  if (state.place.lat !== lat || state.place.lon !== lon || state.place.timezone !== timezone) state.cloudy = false;
  state.place = { name, lat, lon, timezone, sunrise: samplePlace?.sunrise ?? '06:00', sunset: samplePlace?.sunset ?? '18:00', custom: !samplePlace };
  state.cloudEnter = enter;
  state.cloudExit = exit;
  state.evening = Number($('#evening').value);
  state.night = Number($('#night').value);
  $('#location-name').value = name;
  $('#timezone').value = timezone;
  $('#district').value = name;
  savedFields = readSettingsFields();
  chosenQuery = name;
  $('#header-location').textContent = name;
  $('.location-button').setAttribute('aria-label', 'Change location, currently ' + name);
  renderWeather();
  $('#settings-dialog').close();
  notify('Settings saved for this preview.');
});
document.addEventListener('visibilitychange', updateMotion);
// Mirror the app's five-minute cadence using fixtures, never a network request.
setInterval(() => {
  renderWeather();
  $('#refresh-status').textContent = 'Sample refreshed · No live connection';
}, 5 * 60 * 1000);
renderWeather();
renderSound();
updateMotion();
// Preserve existing bookmarked draft views.
if (location.hash === '#settings') openDialog('settings-dialog');
if (location.hash === '#guide') openDialog('preview-dialog');
