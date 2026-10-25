// Hollowmere desktop shell: a single Electron window around the game.
'use strict';
const { app, BrowserWindow, ipcMain, Menu } = require('electron');
const path = require('path');
const fs = require('fs');

// Render on the discrete GPU with full hardware acceleration.
app.commandLine.appendSwitch('force_high_performance_gpu');
app.commandLine.appendSwitch('ignore-gpu-blocklist');
app.commandLine.appendSwitch('enable-gpu-rasterization');
app.commandLine.appendSwitch('enable-zero-copy');
app.commandLine.appendSwitch('disable-renderer-backgrounding');
// Let the procedural audio start without waiting for a click.
app.commandLine.appendSwitch('autoplay-policy', 'no-user-gesture-required');

if (!app.requestSingleInstanceLock()) app.quit();

const settingsPath = () => path.join(app.getPath('userData'), 'settings.json');
function loadSettings() {
  try { return Object.assign({ fullscreen: true }, JSON.parse(fs.readFileSync(settingsPath(), 'utf8'))); } catch (e) { return { fullscreen: true }; }
}
function saveSettings(s) {
  try { fs.writeFileSync(settingsPath(), JSON.stringify(s)); } catch (e) { /* settings are a convenience */ }
}

// The packaged app ships the minified build; a dev checkout runs the source files.
function gamePage() {
  const built = path.join(__dirname, '..', 'dist', 'app', 'index.html');
  return fs.existsSync(built) ? built : path.join(__dirname, '..', 'index.html');
}

let win = null;
function createWindow() {
  const settings = loadSettings();
  win = new BrowserWindow({
    width: 1600,
    height: 900,
    minWidth: 960,
    minHeight: 540,
    fullscreen: settings.fullscreen,
    backgroundColor: '#050607',
    title: 'Hollowmere',
    icon: path.join(__dirname, 'icon.png'),
    show: false,
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      backgroundThrottling: false,
      spellcheck: false,
    },
  });
  Menu.setApplicationMenu(null);
  win.once('ready-to-show', () => win.show());
  win.loadFile(gamePage());

  const setFullscreen = (on) => {
    win.setFullScreen(on);
    saveSettings(Object.assign(loadSettings(), { fullscreen: on }));
  };
  // F11 or Alt+Enter toggles fullscreen.
  win.webContents.on('before-input-event', (event, input) => {
    if (input.type !== 'keyDown') return;
    if (input.key === 'F11' || (input.alt && input.key === 'Enter')) {
      event.preventDefault();
      setFullscreen(!win.isFullScreen());
    }
  });
  // Never navigate away from the game or open new windows.
  win.webContents.on('will-navigate', (e) => e.preventDefault());
  win.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));

  ipcMain.handle('hm:toggle-fullscreen', () => { setFullscreen(!win.isFullScreen()); return win.isFullScreen(); });
  ipcMain.handle('hm:is-fullscreen', () => win.isFullScreen());
  ipcMain.handle('hm:quit', () => app.quit());
  win.on('closed', () => { win = null; });
}

app.on('second-instance', () => {
  if (!win) return;
  if (win.isMinimized()) win.restore();
  win.focus();
});
app.whenReady().then(createWindow);
app.on('window-all-closed', () => app.quit());
