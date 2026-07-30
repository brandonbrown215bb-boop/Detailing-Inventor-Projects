'use strict';

const { app, BrowserWindow } = require('electron');
const path = require('path');

app.whenReady().then(async () => {
  const win = new BrowserWindow({
    show: false,
    webPreferences: {
      preload: path.join(__dirname, '..', 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      additionalArguments: ['--usv-version=1.0.21'],
    },
  });

  win.webContents.on('preload-error', (_event, _preloadPath, error) => {
    console.error('PRELOAD_ERROR:', error.message);
  });

  win.webContents.on('did-finish-load', async () => {
    const result = await win.webContents.executeJavaScript(
      '({ hasApi: !!window.unitSurfaceViewer, version: window.unitSurfaceViewer?.appVersion || null })'
    );
    console.log('BRIDGE_RESULT:', JSON.stringify(result));
    app.exit(result.hasApi ? 0 : 1);
  });

  await win.loadFile(path.join(__dirname, '..', 'index.html'));
});
