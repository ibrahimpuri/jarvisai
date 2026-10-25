// Builds the shippable game: every script from index.html, in load order, concatenated and
// minified into one file, plus minified CSS, written to dist/app/. Run: node tools/build-web.js
'use strict';
const fs = require('fs');
const path = require('path');
const esbuild = require('esbuild');

const root = path.join(__dirname, '..');
const out = path.join(root, 'dist', 'app');
const html = fs.readFileSync(path.join(root, 'index.html'), 'utf8');
const scripts = [...html.matchAll(/<script src="([^"]+)"><\/script>/g)].map((m) => m[1]);
if (!scripts.length) throw new Error('no scripts found in index.html');

// Each file is a classic script sharing the global H namespace, so concatenation preserves behaviour.
const source = scripts.map((s) => `// ---- ${s}\n${fs.readFileSync(path.join(root, s), 'utf8')}\n`).join('\n');
const js = esbuild.transformSync(source, { loader: 'js', minify: true, target: 'es2020', legalComments: 'none' });
const css = esbuild.transformSync(fs.readFileSync(path.join(root, 'css', 'style.css'), 'utf8'), { loader: 'css', minify: true });

fs.rmSync(out, { recursive: true, force: true });
fs.mkdirSync(out, { recursive: true });
fs.writeFileSync(path.join(out, 'game.min.js'), js.code);
fs.writeFileSync(path.join(out, 'style.css'), css.code);
const page = html
  .replace(/<link rel="stylesheet" href="css\/style.css">/, '<link rel="stylesheet" href="style.css">')
  .replace(/(\s*<script src="[^"]+"><\/script>)+/, '\n<script src="game.min.js"></script>');
fs.writeFileSync(path.join(out, 'index.html'), page);
const kb = (f) => (fs.statSync(path.join(out, f)).size / 1024).toFixed(1) + ' KB';
console.log(`dist/app: ${scripts.length} scripts -> game.min.js (${kb('game.min.js')}), style.css (${kb('style.css')})`);
