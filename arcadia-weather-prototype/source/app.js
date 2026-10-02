(function () {
  'use strict';
  const C = window.ArcadiaCore;
  const assets = window.ArcadiaAssets;
  const $ = id => document.getElementById(id);
  const STORAGE_KEY = 'arcadia-weather-prototype-v1';
  const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)');
  let state = C.defaultState();
  let storageAvailable = true;
  try { state = C.hydrate(JSON.parse(localStorage.getItem(STORAGE_KEY))); }
  catch (_) { storageAvailable = false; }
  let currentScene = null;
  let currentView = 'wallpaper';
  let mediaRevision = 0;
  let mediaBlocked = false;
  let mediaUnavailable = false;
  let motionHeld = motionPreference.matches || state.settings.reduceMotion;
  let simulatedFullscreen = false;
  let nextRefresh = Date.now() + C.REFRESH_MS;
  let searchResults = [];
  let searchIndex = -1;
  let toastTimer;
  let announceTimer;
  let refreshTimer;
  const video = $('wallpaper-video');
  document.querySelectorAll('svg').forEach(svg => {
    svg.setAttribute('aria-hidden', 'true');
    svg.setAttribute('focusable', 'false');
  });

  const icon = name => `<svg class="icon" aria-hidden="true"><use href="#i-${name}"/></svg>`;
  const reducedMotion = () => motionPreference.matches || state.settings.reduceMotion;
  const fullscreenHeld = () => simulatedFullscreen && state.settings.pauseFullscreen;
  function save() {
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify(state)); storageAvailable = true; }
    catch (_) { storageAvailable = false; }
    $('settings-save-state').textContent = storageAvailable ? 'Saved in this browser' : 'Saved for this session';
  }
  function announce(message) {
    clearTimeout(announceTimer);
    $('announcer').textContent = '';
    announceTimer = setTimeout(() => { $('announcer').textContent = message; }, 70);
  }
  function toast(message) {
    clearTimeout(toastTimer);
    $('toast-message').textContent = message;
    $('toast').hidden = false;
    toastTimer = setTimeout(() => { $('toast').hidden = true; }, 3900);
  }
  function renderSceneButtons() {
    $('scene-grid').innerHTML = C.SCENES.map(scene => `<button type="button" class="scene-button" data-scene="${scene.id}" aria-pressed="false" aria-label="Select Day ${scene.id}: ${scene.name}. ${scene.condition}. Holds this scene in Manual mode."><span class="scene-thumb"><img src="${assets[scene.id].poster}" alt="" width="1200" height="675"><span class="scene-number">Day ${scene.id}</span><span class="scene-check">${icon('check')}</span></span><span class="scene-caption"><strong>${scene.name}</strong>${icon(scene.icon)}</span><span class="scene-condition">${scene.condition}</span></button>`).join('');
    $('scene-grid').addEventListener('click', event => {
      const button = event.target.closest('[data-scene]');
      if (!button) return;
      state = C.selectScene(state, Number(button.dataset.scene));
      save(); render();
      announce(`Day ${state.manualScene}, ${C.SCENES[state.manualScene - 1].name}, selected. Manual mode holds this scene until Automatic is enabled.`);
    });
    // Every scene is a normal Tab stop. Arrow keys are an additional convenience.
    $('scene-grid').addEventListener('keydown', event => {
      const buttons = [...$('scene-grid').querySelectorAll('button')];
      const index = buttons.indexOf(document.activeElement);
      if (index < 0) return;
      let target;
      if (event.key === 'ArrowRight') target = (index + 1) % buttons.length;
      if (event.key === 'ArrowLeft') target = (index + buttons.length - 1) % buttons.length;
      if (event.key === 'Home') target = 0;
      if (event.key === 'End') target = buttons.length - 1;
      if (target !== undefined) { event.preventDefault(); buttons[target].focus(); }
    });
  }
  function shouldPlay() {
    return !state.userPaused && !motionHeld && !fullscreenHeld() && !mediaBlocked && !mediaUnavailable && currentView === 'wallpaper' && !document.hidden;
  }
  function syncPlayback() {
    if (currentScene === null) return;
    video.volume = state.volume / 100;
    video.muted = state.muted || state.volume === 0;
    if (shouldPlay()) {
      const revision = mediaRevision;
      const promise = video.play();
      if (promise) promise.catch(error => {
        if (revision !== mediaRevision || error.name === 'AbortError' || !shouldPlay()) return;
        mediaBlocked = true;
        renderPlayback();
      });
    } else { video.pause(); }
    renderPlayback();
  }
  function renderPlayback() {
    const held = fullscreenHeld();
    const paused = state.userPaused || motionHeld || mediaBlocked || mediaUnavailable || held;
    let status = paused ? 'Paused' : video.readyState >= 2 && !video.paused ? 'Playing' : 'Loading';
    if (held) status = 'Fullscreen app';
    if (mediaUnavailable) status = 'Still preview';
    if (mediaBlocked) status = 'Ready to play';
    $('playback-status-text').textContent = status;
    $('playback-status').dataset.status = paused ? 'paused' : 'playing';
    $('pause-icon').setAttribute('href', paused ? '#i-play' : '#i-pause');
    $('pause-label').textContent = mediaUnavailable ? 'Retry' : held ? 'Paused' : paused ? 'Play' : 'Pause';
    $('pause-button').setAttribute('aria-label', mediaUnavailable ? 'Retry wallpaper playback' : held ? 'Wallpaper paused for the sample fullscreen app' : paused ? 'Resume wallpaper' : 'Pause wallpaper');
    $('pause-button').disabled = held;
    const muted = state.muted || state.volume === 0;
    $('volume-icon').setAttribute('href', muted ? '#i-muted' : '#i-volume');
    $('mute-button').setAttribute('aria-label', muted ? 'Unmute wallpaper' : 'Mute wallpaper');
    $('mute-button').setAttribute('aria-pressed', String(muted));
    $('volume').value = state.volume;
    $('volume').setAttribute('aria-valuetext', `${state.volume} percent${muted ? ', muted' : ''}`);
    $('volume-value').textContent = `${state.volume}%`;
    let note = '';
    if (mediaUnavailable) note = 'This browser could not play the clip. The scene image is shown; try playback again or open the file in Edge or Chrome.';
    else if (held) note = 'Paused while the sample fullscreen app is active. Stop the simulation in Settings to resume.';
    else if (motionHeld) note = 'Reduced motion is on. Press Play if you would like to animate this preview.';
    else if (mediaBlocked) note = 'Press Play to start the preview. Your browser is waiting for a playback gesture.';
    $('playback-note').textContent = note;
    $('playback-note').hidden = !note;
    $('footer-status').textContent = held ? 'Paused for fullscreen app' : state.mode === 'manual' ? `Manual · Day ${state.manualScene} held` : paused ? 'Wallpaper paused · weather in sync' : 'Weather in sync';
  }
  function loadScene(sceneId, force = false) {
    if (sceneId === currentScene && !force) return;
    currentScene = sceneId;
    mediaRevision++;
    mediaBlocked = false;
    mediaUnavailable = false;
    motionHeld = reducedMotion();
    video.pause();
    video.classList.remove('ready');
    const scene = C.SCENES.find(item => item.id === sceneId);
    $('preview-poster').src = assets[sceneId].poster;
    $('preview-poster').alt = scene.alt;
    video.poster = assets[sceneId].poster;
    video.src = assets[sceneId].video;
    video.setAttribute('aria-label', `Day ${sceneId}: ${scene.name}. ${scene.alt} Includes bundled scene audio.`);
    video.load();
    $('preview-day').textContent = `Day ${sceneId}`;
    $('preview-name').textContent = scene.name;
  }
  function renderWeather() {
    const forecast = C.WEATHER[state.weather];
    const period = C.periodFor(state.minutes);
    $('current-city').textContent = state.location.name;
    $('current-region').textContent = state.location.region;
    $('temperature').innerHTML = `${forecast.temperature}<span>°C</span>`;
    $('weather-description').textContent = forecast.label;
    $('weather-icon').setAttribute('href', `#i-${state.weather === 'clear' && period === 'Night' ? 'moon' : forecast.icon}`);
    $('local-time').textContent = C.minutesToTime(state.minutes);
    $('local-period').textContent = `${period} · sample local time`;
    $('wind-speed').textContent = `${forecast.wind} km/h`;
    $('humidity').textContent = `${forecast.humidity}%`;
    $('sample-weather').value = state.weather;
    $('sample-time').value = C.minutesToTime(state.minutes);
  }
  function renderLocationFields() {
    $('settings-city').textContent = state.location.name;
    $('settings-coordinates').textContent = C.coordinateLabel(state.location);
    $('latitude').value = state.location.lat;
    $('longitude').value = state.location.lon;
    $('coordinate-error').hidden = true;
    $('latitude').removeAttribute('aria-invalid');
    $('longitude').removeAttribute('aria-invalid');
  }
  function renderSettings() {
    $('display-select').value = state.settings.display;
    $('fit-select').value = state.settings.fit;
    $('aspect-select').value = state.settings.aspect;
    $('preview-stage').style.setProperty('--preview-fit', state.settings.fit);
    $('preview-stage').dataset.aspect = state.settings.aspect;
    $('preview-display').textContent = state.settings.display === 'all' ? 'All displays' : `Display ${state.settings.display}`;
    document.querySelectorAll('[data-monitor]').forEach(monitor => monitor.classList.toggle('selected', state.settings.display === 'all' || state.settings.display === monitor.dataset.monitor));
    $('startup-switch').setAttribute('aria-checked', String(state.settings.startWithWindows));
    $('fullscreen-switch').setAttribute('aria-checked', String(state.settings.pauseFullscreen));
    $('motion-switch').setAttribute('aria-checked', String(reducedMotion()));
    $('motion-switch').disabled = motionPreference.matches;
    $('system-motion-note').textContent = motionPreference.matches ? 'Enabled by your system accessibility preference.' : '';
    document.documentElement.classList.toggle('reduce-motion', reducedMotion());
    $('simulate-fullscreen').setAttribute('aria-pressed', String(simulatedFullscreen));
    $('simulate-fullscreen').textContent = simulatedFullscreen ? 'Stop simulation' : 'Simulate fullscreen app';
    $('fullscreen-sample-status').textContent = !simulatedFullscreen ? 'No sample fullscreen app is running.' : fullscreenHeld() ? 'Sample fullscreen app active. Wallpaper is paused.' : 'Sample fullscreen app active. Wallpaper playback is allowed.';
  }
  function render() {
    const sceneId = C.resolveScene(state);
    const automatic = state.mode === 'automatic';
    loadScene(sceneId);
    renderWeather();
    renderSettings();
    $('automatic-button').setAttribute('aria-pressed', String(automatic));
    $('manual-button').setAttribute('aria-pressed', String(!automatic));
    $('preview-mode').textContent = automatic ? 'Automatic' : 'Manual';
    $('mode-explanation').textContent = automatic ? 'Following the weather and time of day.' : 'This scene stays until you enable Automatic again.';
    $('current-rule').textContent = C.sceneReason(state);
    $('scene-mode-note').textContent = automatic ? 'Selecting a scene switches to Manual' : 'Choose any scene · it stays yours';
    document.querySelectorAll('[data-scene]').forEach(button => button.setAttribute('aria-pressed', String(Number(button.dataset.scene) === sceneId)));
    syncPlayback();
  }

  function renderView(focus = false) {
    const hash = window.location.hash;
    currentView = ['#settings', '#location-settings', '#display-settings', '#behavior-settings'].includes(hash) ? 'settings' : 'wallpaper';
    $('wallpaper-view').hidden = currentView !== 'wallpaper';
    $('settings-view').hidden = currentView !== 'settings';
    for (const view of ['wallpaper', 'settings']) {
      if (view === currentView) $(`nav-${view}`).setAttribute('aria-current', 'page');
      else $(`nav-${view}`).removeAttribute('aria-current');
    }
    syncPlayback();
    if (focus) {
      if (hash.endsWith('-settings')) {
        const section = $(hash.slice(1));
        const heading = section.querySelector('h2');
        heading.tabIndex = -1;
        heading.focus({ preventScroll: true });
        section.scrollIntoView({ behavior: reducedMotion() ? 'instant' : 'smooth', block: 'start' });
      } else {
        $(`${currentView}-heading`).focus({ preventScroll: true });
        window.scrollTo({ top: 0, behavior: 'instant' });
      }
    }
  }
  window.addEventListener('hashchange', () => renderView(true));
  $('location-shortcut').addEventListener('click', () => {
    if (window.location.hash === '#location-settings') renderView(true);
    else window.location.hash = 'location-settings';
  });
  $('automatic-button').addEventListener('click', () => {
    state = C.enableAutomatic(state);
    save(); render();
    announce(`Automatic enabled. ${C.sceneReason(state)}.`);
  });
  $('manual-button').addEventListener('click', () => {
    state = C.selectScene(state, C.resolveScene(state));
    save(); render();
    announce(`Manual enabled. Day ${state.manualScene} will stay until you enable Automatic.`);
  });
  $('pause-button').addEventListener('click', () => {
    if (fullscreenHeld()) return;
    if (mediaUnavailable) {
      state.userPaused = false;
      loadScene(currentScene, true);
      motionHeld = false;
    } else if (state.userPaused || motionHeld || mediaBlocked) {
      state.userPaused = false;
      motionHeld = false;
      mediaBlocked = false;
    } else { state.userPaused = true; }
    save(); syncPlayback();
    announce(state.userPaused ? 'Wallpaper paused.' : 'Wallpaper playback requested.');
  });
  $('mute-button').addEventListener('click', () => {
    const wasMuted = state.muted || state.volume === 0;
    state.muted = !wasMuted;
    if (wasMuted && state.volume === 0) state.volume = 40;
    save(); syncPlayback();
    announce(state.muted ? 'Wallpaper muted.' : `Wallpaper sound on, volume ${state.volume} percent.`);
  });
  $('volume').addEventListener('input', event => {
    state.volume = Number(event.target.value);
    state.muted = state.volume === 0;
    save(); syncPlayback();
  });
  video.addEventListener('loadeddata', () => { video.classList.add('ready'); renderPlayback(); });
  video.addEventListener('playing', renderPlayback);
  video.addEventListener('pause', renderPlayback);
  video.addEventListener('waiting', renderPlayback);
  video.addEventListener('error', () => { mediaUnavailable = true; video.classList.remove('ready'); renderPlayback(); });
  document.addEventListener('visibilitychange', () => { tickRefresh(); syncPlayback(); });

  $('sample-weather').addEventListener('change', event => {
    state.weather = event.target.value;
    nextRefresh = Date.now() + C.REFRESH_MS;
    save(); render(); tickRefresh();
    announce(`Sample weather changed to ${C.WEATHER[state.weather].label}. ${C.sceneReason(state)}.`);
  });
  $('sample-time').addEventListener('change', event => {
    const minutes = C.timeToMinutes(event.target.value);
    $('time-error').hidden = minutes !== null;
    event.target.setAttribute('aria-invalid', String(minutes === null));
    if (minutes === null) return;
    state.minutes = minutes;
    save(); render();
    announce(`Sample time ${C.minutesToTime(minutes)}. ${C.sceneReason(state)}.`);
  });
  function refreshWeather(manual = false) {
    nextRefresh = Date.now() + C.REFRESH_MS;
    // Reading the selected fixture deliberately keeps the sample forecast stable.
    // No fetch, geolocation, or Windows integration runs in this prototype.
    renderWeather();
    if (state.mode === 'automatic') render();
    if (manual) {
      clearTimeout(refreshTimer);
      $('refresh-button').parentElement.classList.add('is-refreshing');
      refreshTimer = setTimeout(() => $('refresh-button').parentElement.classList.remove('is-refreshing'), 650);
      toast('Sample weather refreshed. Next check in five minutes.');
    }
  }
  function tickRefresh() {
    const now = Date.now();
    if (now >= nextRefresh) refreshWeather();
    const seconds = Math.max(0, Math.ceil((nextRefresh - now) / 1000));
    $('refresh-countdown').textContent = `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
  }
  $('refresh-button').addEventListener('click', () => { refreshWeather(true); tickRefresh(); });
  setInterval(tickRefresh, 1000);

  function closeSearch() {
    $('search-results').hidden = true;
    $('city-search').setAttribute('aria-expanded', 'false');
    $('city-search').removeAttribute('aria-activedescendant');
    searchIndex = -1;
  }
  function updateSearchSelection() {
    [...$('search-results').children].forEach((option, index) => option.setAttribute('aria-selected', String(index === searchIndex)));
    if (searchIndex >= 0) {
      $('city-search').setAttribute('aria-activedescendant', `search-option-${searchIndex}`);
      $('search-results').children[searchIndex].scrollIntoView({ block: 'nearest', behavior: 'instant' });
    } else $('city-search').removeAttribute('aria-activedescendant');
  }
  function showSearch() {
    searchResults = C.searchLocations($('city-search').value);
    searchIndex = -1;
    $('search-results').innerHTML = searchResults.map((place, index) => `<li role="option" id="search-option-${index}" aria-selected="false" data-search-index="${index}"><span><strong>${place.name}</strong><small>${place.region} · ${place.kind}</small></span>${icon('arrow')}</li>`).join('');
    $('search-results').hidden = searchResults.length === 0;
    $('search-empty').hidden = searchResults.length > 0;
    $('city-search').setAttribute('aria-expanded', String(searchResults.length > 0));
    $('city-search').removeAttribute('aria-activedescendant');
  }
  function applyLocation(location, message) {
    state.location = { ...location };
    nextRefresh = Date.now() + C.REFRESH_MS;
    $('city-search').value = location.id === 'custom' ? '' : location.name;
    $('search-empty').hidden = true;
    closeSearch(); renderLocationFields(); save(); render(); tickRefresh();
    $('coordinate-status').textContent = 'Location applied';
    toast(message);
  }
  function chooseSearch(index) {
    const place = searchResults[index];
    if (place) applyLocation(place, `Sample location set to ${place.name}.`);
  }
  $('city-search').addEventListener('input', showSearch);
  $('city-search').addEventListener('focus', showSearch);
  $('city-search').addEventListener('keydown', event => {
    if (event.key === 'Escape') { closeSearch(); $('search-empty').hidden = true; return; }
    if (['ArrowDown', 'ArrowUp'].includes(event.key)) {
      event.preventDefault();
      if ($('search-results').hidden) showSearch();
      if (!searchResults.length) return;
      searchIndex = event.key === 'ArrowDown' ? (searchIndex + 1) % searchResults.length : (searchIndex <= 0 ? searchResults.length : searchIndex) - 1;
      updateSearchSelection();
    } else if (event.key === 'Enter' && !$('search-results').hidden) {
      event.preventDefault();
      chooseSearch(searchIndex >= 0 ? searchIndex : 0);
    } else if (event.key === 'Tab') closeSearch();
  });
  $('search-results').addEventListener('mousedown', event => event.preventDefault());
  $('search-results').addEventListener('click', event => {
    const option = event.target.closest('[data-search-index]');
    if (option) chooseSearch(Number(option.dataset.searchIndex));
  });
  document.addEventListener('click', event => {
    if (!$('search-results').contains(event.target) && event.target !== $('city-search')) { closeSearch(); $('search-empty').hidden = true; }
  });
  $('use-location').addEventListener('click', () => {
    applyLocation(C.LOCATIONS[0], 'Sample location found: Astoria. Coordinates autofilled.');
  });
  for (const id of ['latitude', 'longitude']) $(id).addEventListener('input', () => { $('coordinate-status').textContent = 'Unapplied changes'; });
  $('coordinates-form').addEventListener('submit', event => {
    event.preventDefault();
    const coords = C.parseCoordinates($('latitude').value, $('longitude').value);
    $('latitude').setAttribute('aria-invalid', String(!coords.latValid));
    $('longitude').setAttribute('aria-invalid', String(!coords.lonValid));
    if (!coords.valid) {
      $('coordinate-error').textContent = !coords.latValid ? 'Enter a latitude from −90 to 90.' : 'Enter a longitude from −180 to 180.';
      $('coordinate-error').hidden = false;
      $(!coords.latValid ? 'latitude' : 'longitude').focus();
      return;
    }
    const match = C.LOCATIONS.find(place => Math.abs(place.lat - coords.lat) < .00001 && Math.abs(place.lon - coords.lon) < .00001);
    const place = match || { id: 'custom', name: 'Custom coordinates', region: 'Manually entered location', lat: coords.lat, lon: coords.lon, kind: 'Coordinates' };
    applyLocation(place, 'Coordinates applied. Weather remains sample data.');
  });
  for (const [id, setting] of [['display-select', 'display'], ['fit-select', 'fit'], ['aspect-select', 'aspect']]) {
    $(id).addEventListener('change', event => { state.settings[setting] = event.target.value; save(); renderSettings(); announce('Display preference saved.'); });
  }
  $('startup-switch').addEventListener('click', () => {
    state.settings.startWithWindows = !state.settings.startWithWindows;
    save(); renderSettings();
    toast(`Start with Windows ${state.settings.startWithWindows ? 'enabled' : 'disabled'} in this prototype.`);
  });
  $('fullscreen-switch').addEventListener('click', () => {
    state.settings.pauseFullscreen = !state.settings.pauseFullscreen;
    save(); renderSettings(); syncPlayback();
    announce(`Pause during fullscreen apps ${state.settings.pauseFullscreen ? 'enabled' : 'disabled'}.`);
  });
  $('simulate-fullscreen').addEventListener('click', () => {
    simulatedFullscreen = !simulatedFullscreen;
    renderSettings(); syncPlayback();
    toast(simulatedFullscreen ? fullscreenHeld() ? 'Sample fullscreen app started. Wallpaper paused.' : 'Sample fullscreen app started. Pausing is disabled.' : state.userPaused || motionHeld ? 'Simulation stopped. Wallpaper remains paused.' : 'Simulation stopped. Wallpaper can resume.');
  });
  $('motion-switch').addEventListener('click', () => {
    state.settings.reduceMotion = !state.settings.reduceMotion;
    motionHeld = reducedMotion();
    save(); renderSettings(); syncPlayback();
    announce(`Reduced motion ${reducedMotion() ? 'enabled' : 'disabled'}.`);
  });
  motionPreference.addEventListener('change', () => { motionHeld = reducedMotion(); renderSettings(); syncPlayback(); });

  document.querySelectorAll('.rules-trigger').forEach(button => button.addEventListener('click', () => $('rules-dialog').showModal()));
  document.querySelectorAll('.close-dialog').forEach(button => button.addEventListener('click', () => $('rules-dialog').close()));
  $('reset-button').addEventListener('click', () => $('reset-dialog').showModal());
  document.querySelectorAll('.close-reset').forEach(button => button.addEventListener('click', () => $('reset-dialog').close()));
  for (const id of ['rules-dialog', 'reset-dialog']) $(id).addEventListener('click', event => {
    const rect = $(id).getBoundingClientRect();
    if (event.target === $(id) && (event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom)) $(id).close();
  });
  $('confirm-reset').addEventListener('click', () => {
    state = C.defaultState();
    simulatedFullscreen = false;
    currentScene = null;
    nextRefresh = Date.now() + C.REFRESH_MS;
    $('sample-controls').open = false;
    $('city-search').value = '';
    $('coordinate-status').textContent = '';
    $('search-empty').hidden = true;
    $('time-error').hidden = true;
    $('sample-time').removeAttribute('aria-invalid');
    closeSearch(); renderLocationFields(); save(); render(); tickRefresh();
    $('reset-dialog').close();
    toast('Prototype reset to the original sample settings.');
  });

  renderSceneButtons();
  renderLocationFields();
  renderSettings();
  renderView();
  render();
  tickRefresh();
  if (!storageAvailable) $('settings-save-state').textContent = 'Preferences saved for this session';
  // Save once to detect browsers that deny storage for local files.
  save();
})();
