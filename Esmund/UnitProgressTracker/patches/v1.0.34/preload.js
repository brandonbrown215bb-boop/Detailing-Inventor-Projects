'use strict';

const { contextBridge, ipcRenderer } = require('electron');

function readAppVersion() {
  const arg = process.argv.find((entry) => entry.startsWith('--usv-version='));
  return arg ? arg.slice('--usv-version='.length) : '0.0.0';
}

contextBridge.exposeInMainWorld('unitSurfaceViewer', {
  appVersion: readAppVersion(),
  getAppVersion: () => ipcRenderer.invoke('get-app-version'),
  allowFolderScans: () => ipcRenderer.invoke('allow-folder-scans'),
  pickFolder: () => ipcRenderer.invoke('pick-folder'),
  scanFolder: (folderPath, onProgress) => {
    const listener = (_event, payload) => {
      if (typeof onProgress === 'function') onProgress(payload);
    };
    ipcRenderer.on('scan-progress', listener);
    return ipcRenderer
      .invoke('scan-folder', { folderPath })
      .finally(() => {
        ipcRenderer.removeListener('scan-progress', listener);
      });
  },
  loadProjectFolder: (folderPath, projectPayload) =>
    ipcRenderer.invoke('load-project-folder', { folderPath, projectPayload }),
  loadOptions: () => ipcRenderer.invoke('load-options'),
  saveOptions: (options) => ipcRenderer.invoke('save-options', options),
  saveProjectData: (folderPath, projectData) =>
    ipcRenderer.invoke('save-project-data', { folderPath, projectData }),
  saveProjectAs: (folderPath, payload) => ipcRenderer.invoke('save-project-as', { folderPath, payload }),
  saveProjectFile: (filePath, payload) =>
    ipcRenderer.invoke('save-project-file', { filePath, payload }),
  pickLoadProject: () => ipcRenderer.invoke('pick-load-project'),
  exportData: (folderPath, format, payload) =>
    ipcRenderer.invoke('export-data', { folderPath, format, payload }),
  makeStateId: (name, existingIds) => ipcRenderer.invoke('make-state-id', { name, existingIds }),
  cancelScan: () => ipcRenderer.invoke('cancel-scan'),
  loadRecentProjects: () => ipcRenderer.invoke('load-recent-projects'),
  rememberProject: (folderPath) => ipcRenderer.invoke('remember-project', { folderPath }),
  pickImportFile: () => ipcRenderer.invoke('pick-import-file'),
  pickImportBomXlsx: () => ipcRenderer.invoke('pick-import-bom-xlsx'),
  pickImportUnitConfig: () => ipcRenderer.invoke('pick-import-unit-config'),
  pickShellRoot: () => ipcRenderer.invoke('pick-shell-root'),
  createShellFolders: (rootPath, foldersOrPaths) =>
    ipcRenderer.invoke('create-shell-folders', {
      rootPath,
      folders: Array.isArray(foldersOrPaths) && foldersOrPaths.length && typeof foldersOrPaths[0] === 'object'
        ? foldersOrPaths
        : undefined,
      relativePaths: Array.isArray(foldersOrPaths) && foldersOrPaths.length && typeof foldersOrPaths[0] === 'string'
        ? foldersOrPaths
        : undefined,
    }),
  pathExists: (targetPath) => ipcRenderer.invoke('path-exists', { targetPath }),
  openShellFolder: (rootPath, relativePath) =>
    ipcRenderer.invoke('open-shell-folder', { rootPath, relativePath }),
});
