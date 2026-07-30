'use strict';

const { app, BrowserWindow, ipcMain } = require('electron');
const path = require('path');

let scans = 0;
let allowed = false;

ipcMain.handle('get-app-version', async () => ({ version: 'test' }));
ipcMain.handle('allow-folder-scans', async () => {
  allowed = true;
  return { ok: true };
});
ipcMain.handle('scan-folder', async () => {
  scans += 1;
  return {
    folderPath: 'C:\\test',
    surfaces: [],
    errors: [],
    projectData: { version: 2, surfaces: {}, retired: {} },
    scanSource: 'none',
  };
});
ipcMain.handle('load-options', async () => ({
  version: 3,
  surfaceOpacity: 0.9,
  listDisplay: { nameMode: 'both', showTypeTag: true, showSkidTag: true, showSideTag: true, sortMode: 'default' },
  layout: { leftWidth: 260, rightWidth: 320 },
  uiTheme: { fontFamily: 'Segoe UI', fontSizePx: 14, colors: {} },
  viewer: { showGrid: false, fpsControlsEnabled: false, mouseButtons: { rotate: 0, pan: 2, zoom: 1 }, fpsKeys: {} },
  states: [{ id: 'current', name: 'Current', color: '#000', fillType: 'solid' }],
  checklistItems: [],
}));
ipcMain.handle('load-recent-projects', async () => ({
  lastFolder: 'C:\\Users\\esmun\\Documents\\Cursor\\Ce3\\xml_data\\20078\\ISG\\Skid 4',
  recent: [
    {
      folderPath: 'C:\\Users\\esmun\\Documents\\Cursor\\Ce3\\xml_data\\20078\\ISG\\Skid 4',
      label: 'Skid 4',
    },
  ],
}));
ipcMain.handle('load-project-folder', async () => ({
  folderPath: 'C:\\test',
  surfaces: [],
  errors: [],
  projectData: { version: 2, surfaces: {}, retired: {} },
  scanSource: 'none',
  fromCache: false,
}));
ipcMain.handle('remember-project', async () => ({ ok: true }));
ipcMain.handle('save-options', async () => ({ ok: true }));
ipcMain.handle('cancel-scan', async () => ({ ok: true }));

app.whenReady().then(() => {
  const win = new BrowserWindow({
    show: false,
    webPreferences: {
      preload: path.join(__dirname, '..', 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  win.webContents.on('did-finish-load', () => {
    setTimeout(() => {
      console.log(`SCANS_AT_STARTUP=${scans}`);
      console.log(`SCANS_ALLOWED=${allowed}`);
      app.exit(scans === 0 ? 0 : 1);
    }, 2500);
  });

  win.loadFile(path.join(__dirname, '..', 'index.html'), { query: { v: 'startup-test' } });
});
