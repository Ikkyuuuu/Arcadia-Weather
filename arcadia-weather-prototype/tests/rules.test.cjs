const test = require('node:test');
const assert = require('node:assert/strict');
const C = require('../source/core.js');

test('clear weather changes at the exact sample time boundaries', () => {
  for (const [time, expected] of [['00:00',3],['05:59',3],['06:00',1],['11:59',1],['12:00',1],['17:59',1],['18:00',4],['20:59',4],['21:00',3],['23:59',3]]) {
    assert.equal(C.automaticScene('clear', C.timeToMinutes(time)), expected, time);
  }
});
test('cloud cover replaces daytime and evening, never night, for every minute', () => {
  for (let minute = 0; minute < 1440; minute++) {
    assert.equal(C.automaticScene('cloudy', minute), minute < 360 || minute >= 1260 ? 3 : 2, C.minutesToTime(minute));
  }
});
test('every precipitation condition wins at every minute, including the night', () => {
  for (const weather of ['rain', 'drizzle', 'showers', 'thunderstorm']) {
    for (let minute = 0; minute < 1440; minute++) assert.equal(C.automaticScene(weather, minute), 5, `${weather} ${minute}`);
  }
});
test('manual scenes hold across weather and time updates until Automatic is re-enabled', () => {
  for (const scene of C.SCENES) {
    let state = C.selectScene(C.defaultState(), scene.id);
    for (const weather of Object.keys(C.WEATHER)) {
      for (const minutes of [0, 359, 360, 719, 720, 1079, 1080, 1259, 1260, 1439]) {
        state = { ...state, weather, minutes };
        assert.equal(C.resolveScene(state), scene.id);
        assert.equal(C.resolveScene(C.enableAutomatic(state)), C.automaticScene(weather, minutes));
      }
    }
  }
});
test('manual selection is explicit and cannot mutate the previous state', () => {
  const state = C.defaultState();
  const manual = C.selectScene(state, 4);
  assert.equal(state.mode, 'automatic');
  assert.equal(manual.mode, 'manual');
  assert.equal(manual.manualScene, 4);
  assert.throws(() => C.selectScene(state, 6), RangeError);
});
test('every valid local time round trips; malformed or empty times are rejected', () => {
  for (let minute = 0; minute < 1440; minute++) assert.equal(C.timeToMinutes(C.minutesToTime(minute)), minute);
  for (const time of ['', '24:00', '12:60', '1:20', '99:01', '12:00:00', '-1:20']) assert.equal(C.timeToMinutes(time), null);
});
test('coordinate validation accepts zero and geographic limits, rejects blanks and invalid numbers', () => {
  for (const coords of [['0','0'],['90','180'],['-90','-180'],['46.1879','-123.8313']]) assert.ok(C.parseCoordinates(...coords).valid);
  for (const coords of [['','0'],['0',' '],['91','0'],['0','-181'],['Infinity','5'],['0','NaN'],[undefined,12],[null,0],[true,false]]) assert.equal(C.parseCoordinates(...coords).valid, false);
  assert.equal(C.coordinateLabel({lat:-13.75,lon:100.5}), '13.7500° S, 100.5000° E');
});
test('offline search supports cities, districts, regional text, case and Thai aliases', () => {
  assert.equal(C.searchLocations(' ASTORIA ')[0].id, 'astoria');
  assert.equal(C.searchLocations('bang rak')[0].kind, 'District');
  assert.equal(C.searchLocations('บางรัก')[0].id, 'bang-rak');
  assert.ok(C.searchLocations('Bangkok').length >= 3);
  assert.equal(C.searchLocations('no such place xyz').length, 0);
});
test('saved preferences retain a manual hold and valid custom coordinates', () => {
  const saved = { ...C.selectScene(C.defaultState(), 2), weather: 'thunderstorm', location: { id:'custom',lat:0,lon:0 } };
  const restored = C.hydrate(JSON.parse(JSON.stringify(saved)));
  assert.equal(C.resolveScene(restored), 2);
  assert.equal(restored.location.lat, 0);
  assert.equal(restored.location.name, 'Custom coordinates');
});
test('invalid saved preferences fall back safely and sample place names cannot be injected', () => {
  const restored = C.hydrate({mode:'bad',manualScene:99,weather:'__proto__',minutes:-1,volume:Infinity,location:{id:'astoria',name:'<script>bad</script>',lat:1,lon:2},settings:{fit:'bad',display:'script',reduceMotion:'false'}});
  assert.deepEqual(restored, C.defaultState());
  assert.deepEqual(C.hydrate(null), C.defaultState());
});
test('the refresh interval is five minutes', () => { assert.equal(C.REFRESH_MS, 300000); });
