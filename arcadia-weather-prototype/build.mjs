/** Build a portable, fully offline HTML file. No dependencies or server required. */
import { readFile, writeFile, stat } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
const root = path.dirname(fileURLToPath(import.meta.url));
const source = path.join(root, 'source');
const read = name => readFile(path.join(source, name), 'utf8');
const files = await Promise.all(['index.html', 'styles.css', 'core.js', 'app.js'].map(read));
let [html, css, core, app] = files;
const assets = {};
for (let day = 1; day <= 5; day++) {
  const [poster, video] = await Promise.all([
    readFile(path.join(source, 'assets', `day${day}.jpg`)),
    readFile(path.join(source, 'assets', `day${day}.mp4`))
  ]);
  assets[day] = { poster: `data:image/jpeg;base64,${poster.toString('base64')}`, video: `data:video/mp4;base64,${video.toString('base64')}` };
}
const scriptTag = content => `<script>\n${content.replaceAll('</script', '<\\/script')}\n</script>`;
html = html.replace('<link rel="stylesheet" href="styles.css">', () => `<style>\n${css}\n</style>`)
  .replace('src="assets/day1.jpg"', () => `src="${assets[1].poster}"`)
  .replace('<script src="core.js"></script>', () => scriptTag(core))
  .replace('<script src="assets.js"></script>', () => scriptTag(`window.ArcadiaAssets = ${JSON.stringify(assets)};`))
  .replace('<script src="app.js"></script>', () => scriptTag(app));
html = html.replace('<!doctype html>', '<!doctype html>\n<!-- Arcadia Weather: standalone offline prototype. Editable files are in source/. -->');
const output = path.join(root, 'Arcadia Weather.html');
await writeFile(output, html, 'utf8');
console.log(`Built Arcadia Weather.html (${((await stat(output)).size / 1024 / 1024).toFixed(1)} MB). All five clips, scene images, styles, and scripts are embedded.`);
