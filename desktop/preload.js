// Exposes the few desktop controls the game menus need.
'use strict';
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('hollowmereDesktop', {
  toggleFullscreen: () => ipcRenderer.invoke('hm:toggle-fullscreen'),
  isFullscreen: () => ipcRenderer.invoke('hm:is-fullscreen'),
  quit: () => ipcRenderer.invoke('hm:quit'),
});
