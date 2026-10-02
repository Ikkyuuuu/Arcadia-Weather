const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.join(__dirname, '..');
const html = fs.readFileSync(path.join(root, 'source/index.html'), 'utf8');
const css = fs.readFileSync(path.join(root, 'source/styles.css'), 'utf8');
const app = fs.readFileSync(path.join(root, 'source/app.js'), 'utf8');

test('HTML IDs are unique and JavaScript element references exist', () => {
  const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map(match => match[1]);
  assert.equal(ids.length, new Set(ids).size, 'duplicate ID');
  for (const match of app.matchAll(/\$\('([^']+)'\)/g)) assert.ok(ids.includes(match[1]), `Missing element: ${match[1]}`);
});
test('label, ARIA and icon fragment references resolve', () => {
  const ids = new Set([...html.matchAll(/\bid="([^"]+)"/g)].map(match => match[1]));
  for (const match of html.matchAll(/(?:for|aria-labelledby|aria-describedby|aria-controls)="([^"]+)"/g)) {
    for (const id of match[1].split(' ')) assert.ok(ids.has(id), `Missing label/ARIA target: ${id}`);
  }
  for (const match of html.matchAll(/<use[^>]*href="#([^"]+)"/g)) assert.ok(ids.has(match[1]), `Missing icon: ${match[1]}`);
});
test('prototype has the full-width cyan navbar, responsive breakpoints and motion support', () => {
  assert.match(css, /\.topbar\s*\{\s*width:\s*100%;[^}]*background:\s*#36bbd9/);
  assert.match(css, /@media\(max-width:800px\)/);
  assert.match(css, /@media\(max-width:500px\)/);
  assert.match(css, /prefers-reduced-motion:reduce/);
  assert.match(app, /motionPreference\.matches/);
});
test('no live API, geolocation, external font or video file-path controls', () => {
  assert.doesNotMatch(app, /\bfetch\s*\(|XMLHttpRequest|navigator\.geolocation|WebSocket|https?:\/\//);
  assert.doesNotMatch(html, /<input[^>]*type="file"/);
  assert.doesNotMatch(css, /@import|url\(\s*['"]?https?:/);
});
test('source scripts parse without third-party runtime dependencies', () => {
  for (const name of ['core.js','assets.js','app.js']) new vm.Script(fs.readFileSync(path.join(root,'source',name),'utf8'), {filename:name});
});
test('built HTML embeds all five videos and five scene images, with no external scripts or styles', () => {
  const built = fs.readFileSync(path.join(root, 'Arcadia Weather.html'), 'utf8');
  assert.equal((built.match(/data:video\/mp4;base64,/g) || []).length, 5);
  assert.equal((built.match(/data:image\/jpeg;base64,/g) || []).length, 6); // Five assets plus initial poster.
  assert.doesNotMatch(built, /<script\s+src=|<link[^>]+rel="stylesheet"|src="assets\//);
  const scripts = [...built.matchAll(/<script>([\s\S]*?)<\/script>/g)];
  assert.equal(scripts.length, 3);
  for (const [index, script] of scripts.entries()) new vm.Script(script[1], {filename:`inline-${index}`});
});
