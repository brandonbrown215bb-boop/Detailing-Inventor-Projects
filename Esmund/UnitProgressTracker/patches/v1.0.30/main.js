'use strict';

const { app, BrowserWindow, ipcMain, dialog, shell } = require('electron');
const fs = require('fs');
const path = require('path');
const { walk391ZIamFiles, readConfigsFromIams, extractSkidHintFromPath, cancelActiveScan, beginScanSession } = require('./src/iam-scan');
const { extractSurfaceScanMeta } = require('./src/config-meta.cjs');
const XLSX = require('xlsx');

const DATA_DIR_NAME = '.unit-surface-viewer';
const SURFACE_DATA_FILE = 'surface-data.json';
const SURFACE_GEOMETRY_CACHE_FILE = 'surface-geometry-cache.json';
const OPTIONS_FILE = 'options.json';
const RECENT_PROJECTS_FILE = 'recent-projects.json';
const MAX_RECENT = 12;

const pkg = require('./package.json');
const APP_VERSION = pkg.version || '0.0.0';

let mainWindow = null;
let folderScansAllowed = false;

function getUserOptionsPath() {
  return path.join(app.getPath('userData'), OPTIONS_FILE);
}

function getRecentProjectsPath() {
  return path.join(app.getPath('userData'), RECENT_PROJECTS_FILE);
}

function getProjectDataPath(folderPath) {
  return path.join(folderPath, DATA_DIR_NAME, SURFACE_DATA_FILE);
}

function getGeometryCachePath(folderPath) {
  return path.join(folderPath, DATA_DIR_NAME, SURFACE_GEOMETRY_CACHE_FILE);
}

function writeGeometryCache(folderPath, surfaces, scanSource) {
  if (!folderPath || !Array.isArray(surfaces)) return;
  writeJsonFileAtomic(getGeometryCachePath(folderPath), {
    version: 1,
    cachedAt: new Date().toISOString(),
    scanSource: scanSource || 'unknown',
    surfaces,
  });
}

function readGeometryCache(folderPath) {
  const cached = readJsonFile(getGeometryCachePath(folderPath), null);
  if (!cached || !Array.isArray(cached.surfaces)) return [];
  return cached.surfaces;
}

function readJsonFile(filePath, fallback) {
  try {
    if (!fs.existsSync(filePath)) return fallback;
    return JSON.parse(fs.readFileSync(filePath, 'utf8'));
  } catch {
    return fallback;
  }
}

function writeJsonFileAtomic(filePath, data) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  const tmpPath = `${filePath}.${process.pid}.${Date.now()}.tmp`;
  fs.writeFileSync(tmpPath, JSON.stringify(data, null, 2), 'utf8');
  fs.renameSync(tmpPath, filePath);
}

function writeJsonFile(filePath, data) {
  writeJsonFileAtomic(filePath, data);
}

function defaultOptions() {
  return {
    version: 3,
    surfaceOpacity: 0.9,
    listDisplay: {
      nameMode: 'both',
      showTypeTag: true,
      showSkidTag: true,
      showSideTag: true,
      sortMode: 'default',
    },
    layout: {
      leftWidth: 260,
      rightWidth: 320,
    },
    uiTheme: {
      fontFamily: '"Segoe UI", system-ui, sans-serif',
      fontSizePx: 14,
      colors: {
        text: '#e2e8f0',
        textMuted: '#94a3b8',
        panelBg: '#111827',
        headerBg: '#111827',
        accent: '#38bdf8',
        listText: '#e2e8f0',
      },
    },
    viewer: {
      showGrid: true,
      fpsControlsEnabled: true,
      mouseButtons: {
        rotate: 0,
        pan: 2,
        zoom: 1,
      },
      fpsKeys: {
        ascend: 'Space',
        descend: 'Control',
        sprint: 'ShiftLeft',
      },
    },
    states: [
      { id: 'current', name: 'Current', color: '#94a3b8' },
      { id: 'corrected', name: 'Corrected', color: '#f59e0b' },
      { id: 'built', name: 'Built', color: '#3b82f6' },
      { id: 'associated', name: 'Associated', color: '#8b5cf6' },
      { id: 'paperwork-corrected', name: 'Paperwork Corrected', color: '#06b6d4' },
      { id: 'paperwork-uploaded', name: 'Paperwork Uploaded', color: '#10b981' },
      { id: 'done', name: 'Done', color: '#22c55e' },
    ],
    checklistItems: [
      { id: 'verified-dims', label: 'Verified dimensions' },
      { id: 'verified-material', label: 'Verified material' },
      { id: 'verified-openings', label: 'Verified openings' },
      { id: 'paperwork-complete', label: 'Paperwork complete' },
    ],
    bomListDisplay: {
      sortMode: 'shell',
      searchText: '',
      skidFilter: '',
      segmentFilter: '',
      customSqOnly: false,
    },
  };
}

function slugId(name) {
  return String(name || '')
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '') || `state-${Date.now()}`;
}

function walkJsonFiles(rootDir, results = []) {
  let entries;
  try {
    entries = fs.readdirSync(rootDir, { withFileTypes: true });
  } catch {
    return results;
  }
  for (const entry of entries) {
    const full = path.join(rootDir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === DATA_DIR_NAME) continue;
      walkJsonFiles(full, results);
    } else if (entry.isFile() && entry.name.toLowerCase().endsWith('.json')) {
      results.push(full);
    }
  }
  return results;
}

function parseGeometryBox(obj) {
  if (!obj || typeof obj !== 'object') return null;
  const num = (v) => {
    if (v == null || v === '') return 0;
    const n = parseFloat(String(v).trim());
    return Number.isFinite(n) ? n : NaN;
  };
  const x = num(obj.x);
  const y = num(obj.y);
  const z = num(obj.z);
  const xLength = num(obj.xLength);
  const yLength = num(obj.yLength);
  const zLength = num(obj.zLength);
  if ([x, y, z, xLength, yLength, zLength].some((v) => Number.isNaN(v))) return null;
  if (xLength <= 0 || yLength <= 0 || zLength <= 0) return null;
  return { x, y, z, xLength, yLength, zLength };
}

function extractSurfaceGeometryBoxes(configJson) {
  const conf = configJson && configJson.configuration;
  if (!conf || typeof conf !== 'object') return [];
  const boxes = [];
  const pushList = (list) => {
    if (!Array.isArray(list)) return;
    for (const item of list) {
      const box = parseGeometryBox(item);
      if (box) boxes.push(box);
    }
  };
  if (conf.roof && conf.roof.geometryList) pushList(conf.roof.geometryList);
  if (conf.wall && conf.wall.geometryList) pushList(conf.wall.geometryList);
  if (conf.unitBase && Array.isArray(conf.unitBase.unitBaseGeometryList)) {
    for (const entry of conf.unitBase.unitBaseGeometryList) {
      const box = parseGeometryBox(entry && entry.geometry);
      if (box) boxes.push(box);
    }
  }
  return boxes;
}

function surfaceNumberFromPath(filePath) {
  return path.basename(filePath, path.extname(filePath));
}

function buildSurfaceFromConfig(configJson, filePath, folderPath, sourceType, configPath) {
  const boxes = extractSurfaceGeometryBoxes(configJson);
  if (boxes.length === 0) return null;
  const conf = configJson.configuration || {};
  const meta = extractSurfaceScanMeta(configJson);
  const surfaceNumber = surfaceNumberFromPath(filePath);
  return {
    surfaceNumber,
    filePath,
    relativePath: path.relative(folderPath, filePath),
    sourceType,
    configPath: configPath || null,
    partNumber: conf.partNumber || surfaceNumber,
    surfaceType: conf.surfaceType || '',
    surfaceUnitSide: conf.surfaceUnitSide || '',
    configurationKind: meta.configurationKind || '',
    skidNumber: meta.skidNumber,
    skidId: meta.skidId,
    boxes,
  };
}

function sendScanProgress(event, payload) {
  if (!event?.sender || event.sender.isDestroyed()) return;
  event.sender.send('scan-progress', payload);
}

async function scanIamSurfaces(folderPath, onProgress) {
  onProgress?.({ phase: 'discovering', message: 'Searching for 391Z assemblies…' });
  const iamPaths = walk391ZIamFiles(folderPath);
  if (iamPaths.length === 0) {
    return { surfaces: [], errors: [], scanSource: 'none' };
  }

  onProgress?.({
    phase: 'discovered',
    total: iamPaths.length,
    message: `Found ${iamPaths.length} assemblies — starting Inventor read…`,
  });

  const surfaces = [];
  const errors = [];
  const seenNumbers = new Set();

  let readResult;
  try {
    readResult = await readConfigsFromIams(__dirname, iamPaths, onProgress);
  } catch (e) {
    return {
      surfaces: [],
      errors: iamPaths.map((filePath) => ({ filePath, error: e.message || String(e) })),
      scanSource: 'iam',
    };
  }

  const configByPath = new Map(
    (readResult.surfaces || []).map((entry) => [path.normalize(entry.iamPath), entry.config])
  );

  for (const err of readResult.errors || []) {
    errors.push({ filePath: err.iamPath, error: err.error });
  }

  onProgress?.({ phase: 'building', current: 0, total: iamPaths.length, message: 'Building surface models…' });

  for (let i = 0; i < iamPaths.length; i++) {
    const filePath = iamPaths[i];
    onProgress?.({
      phase: 'building',
      current: i + 1,
      total: iamPaths.length,
      filePath,
      skid: extractSkidHintFromPath(filePath),
      surface: surfaceNumberFromPath(filePath),
    });

    const configJson = configByPath.get(path.normalize(filePath));
    if (!configJson) continue;

    const surfaceNumber = surfaceNumberFromPath(filePath);
    if (seenNumbers.has(surfaceNumber)) {
      errors.push({ filePath, error: `Duplicate surface number ${surfaceNumber}` });
      continue;
    }
    seenNumbers.add(surfaceNumber);

    const surface = buildSurfaceFromConfig(configJson, filePath, folderPath, 'iam');
    if (!surface) {
      errors.push({ filePath, error: 'No geometry boxes found in surface config' });
      continue;
    }
    surfaces.push(surface);
  }

  return { surfaces, errors, scanSource: 'iam', readSource: readResult.source || 'inventor' };
}

function scanJsonSurfaces(folderPath, onProgress) {
  onProgress?.({ phase: 'discovering', message: 'Searching for CONFIG_JSON files…' });
  const jsonPaths = walkJsonFiles(folderPath);
  onProgress?.({
    phase: 'discovered',
    total: jsonPaths.length,
    message: `Found ${jsonPaths.length} JSON file(s)…`,
  });

  const surfaces = [];
  const errors = [];
  const seenNumbers = new Set();

  for (let i = 0; i < jsonPaths.length; i++) {
    const filePath = jsonPaths[i];
    onProgress?.({
      phase: 'reading',
      current: i + 1,
      total: jsonPaths.length,
      filePath,
      skid: extractSkidHintFromPath(filePath),
      surface: surfaceNumberFromPath(filePath),
    });

    const surfaceNumber = surfaceNumberFromPath(filePath);
    if (seenNumbers.has(surfaceNumber)) {
      errors.push({ filePath, error: `Duplicate surface number ${surfaceNumber}` });
      continue;
    }
    seenNumbers.add(surfaceNumber);

    let configJson;
    try {
      configJson = JSON.parse(fs.readFileSync(filePath, 'utf8'));
    } catch (e) {
      errors.push({ filePath, error: e.message || String(e) });
      continue;
    }

    const surface = buildSurfaceFromConfig(configJson, filePath, folderPath, 'json');
    if (!surface) {
      errors.push({ filePath, error: 'No geometry boxes found' });
      continue;
    }
    surfaces.push(surface);
  }

  return { surfaces, errors, scanSource: 'json' };
}

function createWindow() {
  folderScansAllowed = false;
  mainWindow = new BrowserWindow({
    width: 1440,
    height: 900,
    minWidth: 1024,
    minHeight: 640,
    title: `Unit Progress Tracker v${APP_VERSION}`,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      additionalArguments: [`--usv-version=${APP_VERSION}`],
    },
  });
  mainWindow.webContents.session.clearCache().catch(() => {});
  mainWindow.loadFile(path.join(__dirname, 'index.html'));

  // Ctrl+W closes the window in Chromium; block it so Ctrl (descend) + W (forward) is safe in FPS mode.
  mainWindow.webContents.on('before-input-event', (event, input) => {
    if (input.type !== 'keyDown') return;
    const key = String(input.key || '').toLowerCase();
    if (input.control && !input.meta && key === 'w') {
      event.preventDefault();
    }
  });
}

app.whenReady().then(() => {
  app.setName('Unit Progress Tracker');
  createWindow();
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

function parentWindowFromEvent(event) {
  return BrowserWindow.fromWebContents(event.sender) || mainWindow;
}

ipcMain.handle('pick-folder', async (event) => {
  const result = await dialog.showOpenDialog(parentWindowFromEvent(event), {
    title: 'Choose folder containing 391Z surface assemblies (.iam)',
    properties: ['openDirectory'],
  });
  if (result.canceled || !result.filePaths.length) return null;
  return result.filePaths[0];
});

ipcMain.handle('load-options', async () => {
  const stored = readJsonFile(getUserOptionsPath(), null);
  if (!stored) return defaultOptions();
  return {
    ...defaultOptions(),
    ...stored,
    surfaceOpacity:
      typeof stored.surfaceOpacity === 'number'
        ? Math.min(1, Math.max(0.25, stored.surfaceOpacity))
        : defaultOptions().surfaceOpacity,
    states: (Array.isArray(stored.states) && stored.states.length ? stored.states : defaultOptions().states).map(
      (s) => ({
        ...s,
        fillType: s.fillType || 'solid',
      })
    ),
    checklistItems:
      Array.isArray(stored.checklistItems) && stored.checklistItems.length
        ? stored.checklistItems
        : defaultOptions().checklistItems,
    listDisplay: {
      ...defaultOptions().listDisplay,
      ...(stored.listDisplay && typeof stored.listDisplay === 'object' ? stored.listDisplay : {}),
    },
    bomListDisplay: {
      ...defaultOptions().bomListDisplay,
      ...(stored.bomListDisplay && typeof stored.bomListDisplay === 'object' ? stored.bomListDisplay : {}),
    },
    layout: {
      ...defaultOptions().layout,
      ...(stored.layout && typeof stored.layout === 'object' ? stored.layout : {}),
    },
    uiTheme: {
      ...defaultOptions().uiTheme,
      colors: {
        ...defaultOptions().uiTheme.colors,
        ...(stored.uiTheme && typeof stored.uiTheme === 'object' && stored.uiTheme.colors ? stored.uiTheme.colors : {}),
      },
      ...(stored.uiTheme && typeof stored.uiTheme === 'object' ? stored.uiTheme : {}),
    },
    viewer: {
      ...defaultOptions().viewer,
      mouseButtons: {
        ...defaultOptions().viewer.mouseButtons,
        ...(stored.viewer && typeof stored.viewer === 'object' && stored.viewer.mouseButtons
          ? stored.viewer.mouseButtons
          : {}),
      },
      fpsKeys: {
        ...defaultOptions().viewer.fpsKeys,
        ...(stored.viewer && typeof stored.viewer === 'object' && stored.viewer.fpsKeys ? stored.viewer.fpsKeys : {}),
      },
      ...(stored.viewer && typeof stored.viewer === 'object' ? stored.viewer : {}),
    },
  };
});

ipcMain.handle('save-options', async (_event, options) => {
  writeJsonFile(getUserOptionsPath(), options);
  return { ok: true };
});

ipcMain.handle('get-app-version', async () => ({ version: APP_VERSION }));

ipcMain.handle('allow-folder-scans', async () => {
  folderScansAllowed = true;
  return { ok: true };
});

ipcMain.handle('cancel-scan', async () => {
  cancelActiveScan();
  return { ok: true };
});

ipcMain.handle('scan-folder', async (event, { folderPath }) => {
  if (!folderScansAllowed) {
    throw new Error('Folder scan blocked during startup. Close and reopen the app, then use Open Folder.');
  }
  if (!folderPath || !fs.existsSync(folderPath)) {
    throw new Error('Folder not found');
  }

  beginScanSession();
  const onProgress = (payload) => sendScanProgress(event, payload);

  let scanResult = await scanIamSurfaces(folderPath, onProgress);
  if (scanResult.scanSource === 'none') {
    scanResult = scanJsonSurfaces(folderPath, onProgress);
  }

  onProgress({ phase: 'done', message: 'Finishing…' });

  const { surfaces, errors, scanSource } = scanResult;
  surfaces.sort((a, b) => a.surfaceNumber.localeCompare(b.surfaceNumber, undefined, { numeric: true }));

  const rawProject = readJsonFile(getProjectDataPath(folderPath), {
    version: 2,
    sourceFolder: folderPath,
    surfaces: {},
    retired: {},
  });
  const projectData = normalizeProjectData(rawProject, folderPath);

  writeGeometryCache(folderPath, surfaces, scanSource);

  return { folderPath, surfaces, errors, projectData, scanSource: scanSource || 'none' };
});

ipcMain.handle('load-project-folder', async (_event, { folderPath, projectPayload }) => {
  if (!folderPath || !fs.existsSync(folderPath)) {
    throw new Error('Folder not found');
  }

  let surfaces = [];
  if (Array.isArray(projectPayload?.geometrySurfaces) && projectPayload.geometrySurfaces.length) {
    surfaces = projectPayload.geometrySurfaces;
  } else {
    surfaces = readGeometryCache(folderPath);
  }

  const rawProject = projectPayload
    ? projectPayload
    : readJsonFile(getProjectDataPath(folderPath), {
        version: 2,
        sourceFolder: folderPath,
        surfaces: {},
        retired: {},
      });
  const projectData = normalizeProjectData(rawProject, folderPath);

  return {
    folderPath,
    surfaces,
    errors: [],
    projectData,
    scanSource: surfaces.length > 0 ? (projectPayload?.geometrySurfaces?.length ? 'saved' : 'cache') : 'none',
    fromCache: surfaces.length > 0,
  };
});

function normalizeProjectData(raw, folderPath) {
  const base = raw && typeof raw === 'object' ? raw : {};
  const surfaces = base.surfaces && typeof base.surfaces === 'object' ? { ...base.surfaces } : {};
  const retired = base.retired && typeof base.retired === 'object' ? { ...base.retired } : {};
  for (const [key, record] of Object.entries(surfaces)) {
    surfaces[key] = {
      stateId: record?.stateId || null,
      checklist: record?.checklist && typeof record.checklist === 'object' ? { ...record.checklist } : {},
      notes: record?.notes || '',
      updatedAt: record?.updatedAt || null,
      hidden: Boolean(record?.hidden),
      previousNumbers: Array.isArray(record?.previousNumbers) ? [...record.previousNumbers] : [],
    };
  }
  const bom = base.bom && typeof base.bom === 'object' ? { ...base.bom } : null;
  return {
    version: 2,
    sourceFolder: folderPath || base.sourceFolder || null,
    updatedAt: base.updatedAt || null,
    surfaces,
    retired,
    bom,
  };
}

function rememberRecentProject(folderPath) {
  if (!folderPath) return;
  const stored = readJsonFile(getRecentProjectsPath(), { recent: [] });
  const recent = Array.isArray(stored.recent) ? stored.recent.filter((r) => r.folderPath !== folderPath) : [];
  recent.unshift({
    folderPath,
    label: path.basename(folderPath) || folderPath,
    lastOpened: new Date().toISOString(),
  });
  writeJsonFile(getRecentProjectsPath(), {
    recent: recent.slice(0, MAX_RECENT),
  });
}

ipcMain.handle('load-recent-projects', async () => {
  const stored = readJsonFile(getRecentProjectsPath(), { recent: [] });
  const recent = Array.isArray(stored.recent) ? stored.recent : [];
  return { recent: recent.slice(0, MAX_RECENT) };
});

ipcMain.handle('remember-project', async (_event, { folderPath }) => {
  rememberRecentProject(folderPath);
  return { ok: true };
});

ipcMain.handle('pick-import-file', async (event) => {
  const result = await dialog.showOpenDialog(parentWindowFromEvent(event), {
    title: 'Import surface export JSON',
    properties: ['openFile'],
    filters: [{ name: 'JSON', extensions: ['json'] }],
  });
  if (result.canceled || !result.filePaths.length) return { canceled: true };
  const filePath = result.filePaths[0];
  try {
    const data = JSON.parse(fs.readFileSync(filePath, 'utf8'));
    return { canceled: false, filePath, data };
  } catch (e) {
    throw new Error(`Invalid JSON: ${e.message || e}`);
  }
});

ipcMain.handle('save-project-data', async (_event, { folderPath, projectData }) => {
  if (!folderPath) throw new Error('No folder path');
  projectData.updatedAt = new Date().toISOString();
  projectData.sourceFolder = folderPath;
  projectData.version = 2;
  const filePath = getProjectDataPath(folderPath);
  writeJsonFileAtomic(filePath, projectData);
  rememberRecentProject(folderPath);
  return { ok: true, path: filePath };
});

ipcMain.handle('save-project-as', async (event, { folderPath, payload }) => {
  const defaultName = `unit-surface-project-${new Date().toISOString().slice(0, 10)}.json`;
  const result = await dialog.showSaveDialog(parentWindowFromEvent(event), {
    title: 'Save project as…',
    defaultPath: path.join(folderPath || app.getPath('documents'), defaultName),
    filters: [{ name: 'JSON', extensions: ['json'] }],
  });
  if (result.canceled || !result.filePath) return { ok: false, canceled: true };
  writeJsonFileAtomic(result.filePath, payload);
  return { ok: true, path: result.filePath };
});

ipcMain.handle('save-project-file', async (_event, { filePath, payload }) => {
  if (!filePath) throw new Error('No project file path');
  writeJsonFileAtomic(filePath, payload);
  return { ok: true, path: filePath };
});

ipcMain.handle('pick-load-project', async (event) => {
  const result = await dialog.showOpenDialog(parentWindowFromEvent(event), {
    title: 'Load project…',
    properties: ['openFile'],
    filters: [{ name: 'JSON', extensions: ['json'] }],
  });
  if (result.canceled || !result.filePaths.length) return { canceled: true };
  const filePath = result.filePaths[0];
  try {
    const data = JSON.parse(fs.readFileSync(filePath, 'utf8'));
    return { canceled: false, filePath, data };
  } catch (e) {
    throw new Error(`Invalid project JSON: ${e.message || e}`);
  }
});

ipcMain.handle('export-data', async (event, { folderPath, format, payload }) => {
  const ext = format === 'md' ? 'md' : 'json';
  const defaultName = `surface-export-${new Date().toISOString().slice(0, 10)}.${ext}`;
  const result = await dialog.showSaveDialog(parentWindowFromEvent(event), {
    title: `Export surface data as ${ext.toUpperCase()}`,
    defaultPath: path.join(folderPath || app.getPath('documents'), defaultName),
    filters: [{ name: ext.toUpperCase(), extensions: [ext] }],
  });
  if (result.canceled || !result.filePath) return { ok: false, canceled: true };
  const content = format === 'md' ? payload.markdown : JSON.stringify(payload.json, null, 2);
  fs.writeFileSync(result.filePath, content, 'utf8');
  return { ok: true, path: result.filePath };
});

ipcMain.handle('make-state-id', async (_event, { name, existingIds }) => {
  let base = slugId(name);
  const used = new Set(existingIds || []);
  if (!used.has(base)) return base;
  let i = 2;
  while (used.has(`${base}-${i}`)) i += 1;
  return `${base}-${i}`;
});

const BOM_KEEP_FIELDS = [
  'Part Number',
  'Quantity',
  'Unit',
  'Skid',
  'Segment',
  'Description',
  'Ext. Description',
];

function parseBomXlsxFile(filePath) {
  const workbook = XLSX.readFile(filePath);
  const sheet = workbook.Sheets.Sheet1;
  if (!sheet) throw new Error('Sheet1 not found in workbook');
  const rows = XLSX.utils.sheet_to_json(sheet, { defval: '' });
  return rows
    .map((row) => {
      const out = {};
      for (const key of BOM_KEEP_FIELDS) {
        out[key] = row[key] != null ? String(row[key]).trim() : '';
      }
      return out;
    })
    .filter((row) => row['Part Number']);
}

function resolveShellFolderPath(rootPath, relativePath) {
  const rootNorm = path.resolve(rootPath);
  const safeRelative = String(relativePath || '')
    .replace(/\.\./g, '')
    .replace(/^[/\\]+/, '')
    .split(/[/\\]/)
    .filter(Boolean)
    .join(path.sep);
  const full = path.join(rootNorm, safeRelative);
  if (!full.startsWith(rootNorm)) {
    throw new Error('Invalid folder path');
  }
  return full;
}

ipcMain.handle('pick-import-bom-xlsx', async (event) => {
  const result = await dialog.showOpenDialog(parentWindowFromEvent(event), {
    title: 'Import BOM spreadsheet',
    properties: ['openFile'],
    filters: [{ name: 'Excel', extensions: ['xlsx', 'xls'] }],
  });
  if (result.canceled || !result.filePaths.length) return { canceled: true };
  const filePath = result.filePaths[0];
  try {
    const rows = parseBomXlsxFile(filePath);
    return { canceled: false, filePath, rows };
  } catch (e) {
    throw new Error(`Could not read BOM: ${e.message || e}`);
  }
});

ipcMain.handle('pick-shell-root', async (event) => {
  const result = await dialog.showOpenDialog(parentWindowFromEvent(event), {
    title: 'Choose Inventor export root folder',
    properties: ['openDirectory', 'createDirectory'],
  });
  if (result.canceled || !result.filePaths.length) return { canceled: true };
  return { canceled: false, folderPath: result.filePaths[0] };
});

ipcMain.handle('create-shell-folders', async (_event, { rootPath, relativePaths }) => {
  if (!rootPath || !fs.existsSync(rootPath)) {
    throw new Error('Inventor root folder not found');
  }
  const paths = Array.isArray(relativePaths) ? relativePaths : [];
  const created = [];
  for (const relativePath of paths) {
    const full = resolveShellFolderPath(rootPath, relativePath);
    fs.mkdirSync(full, { recursive: true });
    created.push(full);
  }
  return { ok: true, count: created.length };
});

ipcMain.handle('path-exists', async (_event, { targetPath }) => {
  if (!targetPath) return { exists: false };
  return { exists: fs.existsSync(targetPath) };
});

ipcMain.handle('open-shell-folder', async (_event, { rootPath, relativePath }) => {
  if (!rootPath) throw new Error('Inventor root not set');
  const full = resolveShellFolderPath(rootPath, relativePath);
  if (!fs.existsSync(full)) {
    throw new Error(`Folder not found:\n${full}\n\nIf the Inventor root moved, use Relocate Inventor root…`);
  }
  const err = await shell.openPath(full);
  if (err) throw new Error(err);
  return { ok: true, path: full };
});
