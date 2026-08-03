import { buildExportPayload, exportToMarkdown } from './export.js';
import {
  migrateProjectData,
  normalizeSurfaceRecord,
  mergeScanWithProject,
  geometryFingerprint,
  linkPreviousSurface,
  importExportPayload,
  shortSurfaceLabel,
  getDisplayNumber,
  renumberSurfaceInPlace,
  replaceSurfaceWithScanned,
  refreshSurfaceGeometryRecord,
  addScannedSurfacesToProject,
  retireSurfaceFully,
  filterExcludedGeometry,
  listRemovedSurfaces,
} from './project-data.js';
import { buildProjectSavePayload, parseSavedProjectFile } from './project-save.js';
import {
  FILL_SOLID,
  FILL_TYPE_OPTIONS,
  normalizeStateAppearance,
  getSurfaceAppearanceFromState,
  getSwatchBackground,
} from './surface-wraps.js';
import {
  DEFAULT_LIST_DISPLAY,
  getListDisplay,
  sortSurfacesForList,
  formatSkidTag,
  formatSkidDisplay,
  formatTypeTag,
  formatSideTag,
  buildSurfaceInfoLines,
  normalizeListDisplay,
} from './config-meta.js';
import {
  DEFAULT_UI_THEME,
  DEFAULT_LAYOUT,
  FONT_FAMILY_OPTIONS,
  normalizeUiTheme,
  normalizeLayout,
  applyUiTheme,
  applyLayout,
} from './ui-theme.js';
import {
  DEFAULT_VIEWER_OPTIONS,
  MOUSE_BUTTONS,
  FPS_KEY_OPTIONS,
  fpsKeyLabel,
  getViewerOptions,
  normalizeViewerOptions,
} from './viewer-options.js';
import {
  renderBomPage,
  normalizeBomState,
  bomStateFromImport,
  attachShellRoot,
  attachUnitConfig,
  bomForPersistence,
  addManualBomEntry,
  removeBomEntry,
  populateBomAddSkidSelect,
  populateBomAddSegmentSelect,
  bomEntryKey,
  normalizeBomEntryRecord,
} from './bom-page.js';
import {
  DEFAULT_BOM_LIST_DISPLAY,
  getBomListDisplay,
  normalizeBomListDisplay,
} from './bom-list-display.js';
import { parseUnitConfigXml } from './unit-config-parser.js';

const api = window.unitSurfaceViewer;
let viewer = null;
let eventsBound = false;

const els = {
  appMain: document.getElementById('appMain'),
  openFolderBtn: document.getElementById('openFolderBtn'),
  fitViewBtn: document.getElementById('fitViewBtn'),
  fileMenuBtn: document.getElementById('fileMenuBtn'),
  fileMenuPanel: document.getElementById('fileMenuPanel'),
  menuSaveProject: document.getElementById('menuSaveProject'),
  menuSaveProjectAs: document.getElementById('menuSaveProjectAs'),
  menuLoadProject: document.getElementById('menuLoadProject'),
  menuRecent: document.getElementById('menuRecent'),
  menuRescan: document.getElementById('menuRescan'),
  menuAddSurfaces: document.getElementById('menuAddSurfaces'),
  menuImportJson: document.getElementById('menuImportJson'),
  menuImportBom: document.getElementById('menuImportBom'),
  menuExportJson: document.getElementById('menuExportJson'),
  menuExportMd: document.getElementById('menuExportMd'),
  optionsBtn: document.getElementById('optionsBtn'),
  leftResizer: document.getElementById('leftResizer'),
  rightResizer: document.getElementById('rightResizer'),
  folderLabel: document.getElementById('folderLabel'),
  appVersion: document.getElementById('appVersion'),
  scanProgress: document.getElementById('scanProgress'),
  scanProgressTrack: document.getElementById('scanProgressTrack'),
  scanProgressFill: document.getElementById('scanProgressFill'),
  scanProgressText: document.getElementById('scanProgressText'),
  scanOverlay: document.getElementById('scanOverlay'),
  scanOverlayTrack: document.getElementById('scanOverlayTrack'),
  scanOverlayFill: document.getElementById('scanOverlayFill'),
  scanOverlayText: document.getElementById('scanOverlayText'),
  scanCancelBtn: document.getElementById('scanCancelBtn'),
  appStatus: document.getElementById('appStatus'),
  surfaceCount: document.getElementById('surfaceCount'),
  loadErrors: document.getElementById('loadErrors'),
  surfaceList: document.getElementById('surfaceList'),
  removedSurfacesSection: document.getElementById('removedSurfacesSection'),
  removedSurfaceList: document.getElementById('removedSurfaceList'),
  removedCount: document.getElementById('removedCount'),
  listNameMode: document.getElementById('listNameMode'),
  listSortMode: document.getElementById('listSortMode'),
  listShowTypeTag: document.getElementById('listShowTypeTag'),
  listShowSkidTag: document.getElementById('listShowSkidTag'),
  listShowSideTag: document.getElementById('listShowSideTag'),
  viewerHost: document.getElementById('viewerHost'),
  viewerEmpty: document.getElementById('viewerEmpty'),
  legend: document.getElementById('legend'),
  opacityControl: document.getElementById('opacityControl'),
  opacitySlider: document.getElementById('opacitySlider'),
  opacityValue: document.getElementById('opacityValue'),
  surfaceInfoPopup: document.getElementById('surfaceInfoPopup'),
  showAllBtn: document.getElementById('showAllBtn'),
  optionsOpacitySlider: document.getElementById('optionsOpacitySlider'),
  optionsOpacityValue: document.getElementById('optionsOpacityValue'),
  optionsShowGrid: document.getElementById('optionsShowGrid'),
  optionsFpsControls: document.getElementById('optionsFpsControls'),
  mouseMapEditor: document.getElementById('mouseMapEditor'),
  stickerEditor: document.getElementById('stickerEditor'),
  themeEditor: document.getElementById('themeEditor'),
  detailPanel: document.getElementById('detailPanel'),
  detailTitle: document.getElementById('detailTitle'),
  detailMeta: document.getElementById('detailMeta'),
  detailSurfaceFields: document.getElementById('detailSurfaceFields'),
  renumberHistorySection: document.getElementById('renumberHistorySection'),
  closeDetailBtn: document.getElementById('closeDetailBtn'),
  stateSelect: document.getElementById('stateSelect'),
  checklist: document.getElementById('checklist'),
  notesInput: document.getElementById('notesInput'),
  historyList: document.getElementById('historyList'),
  linkPreviousSelect: document.getElementById('linkPreviousSelect'),
  linkRenumberBtn: document.getElementById('linkRenumberBtn'),
  linkReplaceBtn: document.getElementById('linkReplaceBtn'),
  renumberInput: document.getElementById('renumberInput'),
  applyRenumberBtn: document.getElementById('applyRenumberBtn'),
  replaceFromFolderBtn: document.getElementById('replaceFromFolderBtn'),
  retireSurfaceBtn: document.getElementById('retireSurfaceBtn'),
  fileKeyNote: document.getElementById('fileKeyNote'),
  optionsDialog: document.getElementById('optionsDialog'),
  optionsForm: document.getElementById('optionsForm'),
  optionsCloseBtn: document.getElementById('optionsCloseBtn'),
  resetOptionsBtn: document.getElementById('resetOptionsBtn'),
  addStateBtn: document.getElementById('addStateBtn'),
  addChecklistBtn: document.getElementById('addChecklistBtn'),
  statesEditor: document.getElementById('statesEditor'),
  checklistEditor: document.getElementById('checklistEditor'),
  recentDialog: document.getElementById('recentDialog'),
  recentList: document.getElementById('recentList'),
  recentCloseBtn: document.getElementById('recentCloseBtn'),
  recentCloseFooterBtn: document.getElementById('recentCloseFooterBtn'),
  viewSurfacesTab: document.getElementById('viewSurfacesTab'),
  viewBomTab: document.getElementById('viewBomTab'),
  surfaceListView: document.getElementById('surfaceListView'),
  surfacePanelActions: document.getElementById('surfacePanelActions'),
  bomPanel: document.getElementById('bomPanel'),
  bomAddDialog: document.getElementById('bomAddDialog'),
  bomAddForm: document.getElementById('bomAddForm'),
  bomAddCloseBtn: document.getElementById('bomAddCloseBtn'),
  bomAddCancelBtn: document.getElementById('bomAddCancelBtn'),
  bomAddError: document.getElementById('bomAddError'),
  bomAddPart: document.getElementById('bomAddPart'),
  bomAddSkid: document.getElementById('bomAddSkid'),
  bomAddSegment: document.getElementById('bomAddSegment'),
  bomAddDesc: document.getElementById('bomAddDesc'),
  bomAddExt: document.getElementById('bomAddExt'),
  bomAddQty: document.getElementById('bomAddQty'),
};

const state = {
  folderPath: null,
  surfaces: [],
  projectData: { version: 2, surfaces: {}, retired: {} },
  options: null,
  selectedSurfaceNumber: null,
  saveTimer: null,
  draftOptions: null,
  loadErrors: [],
  scanSource: 'none',
  projectDirty: false,
  scanBusy: false,
  activeView: 'surfaces',
  detailMode: null,
  selectedBomEntryKey: null,
  projectFilePath: null,
  viewingRemovedKey: null,
};

function defaultOptions() {
  return {
    version: 3,
    surfaceOpacity: 0.9,
    listDisplay: { ...DEFAULT_LIST_DISPLAY },
    layout: { ...DEFAULT_LAYOUT },
    uiTheme: structuredClone(DEFAULT_UI_THEME),
    viewer: structuredClone(DEFAULT_VIEWER_OPTIONS),
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
    bomListDisplay: { ...DEFAULT_BOM_LIST_DISPLAY },
  };
}

function resetScanUi() {
  state.scanBusy = false;
  if (els.scanProgress) els.scanProgress.hidden = true;
  if (els.scanOverlay) els.scanOverlay.hidden = true;
  if (els.scanCancelBtn) els.scanCancelBtn.hidden = true;
}

function setAppStatus(message, isError = false) {
  if (!els.appStatus) return;
  els.appStatus.textContent = message || '';
  els.appStatus.hidden = !message || state.scanBusy;
  els.appStatus.classList.toggle('error', Boolean(isError && message));
}

function formatScanProgressText(payload) {
  if (!payload) return 'Scanning…';
  if (payload.message) return payload.message;
  const { phase, current, total, skid, surface } = payload;
  if (phase === 'discovering') return 'Searching folder for surfaces…';
  if (phase === 'discovered' && total != null) return `Found ${total} file(s)…`;
  if (phase === 'building' && total) {
    const label = surface || '';
    const skidPart = skid ? `${skid}: ` : '';
    return `Building ${skidPart}${current}/${total}${label ? ` — ${label}` : ''}`;
  }
  if (total && current != null) {
    const label = surface || '';
    const skidPart = skid ? `${skid}: ` : '';
    return `Reading ${skidPart}${current}/${total}${label ? ` — ${label}` : ''}`;
  }
  return 'Scanning…';
}

function setScanProgress(active, payload = null) {
  state.scanBusy = active;
  const text = formatScanProgressText(payload);
  const hasCounts = payload && payload.total > 0 && payload.current != null;

  const applyBar = (trackEl, fillEl) => {
    if (!trackEl || !fillEl) return;
    if (hasCounts) {
      trackEl.classList.remove('indeterminate');
      const pct = Math.max(4, Math.min(100, Math.round((payload.current / payload.total) * 100)));
      fillEl.style.width = `${pct}%`;
    } else {
      trackEl.classList.add('indeterminate');
      fillEl.style.width = '35%';
    }
  };

  if (!els.scanProgress) return;
  if (!active) {
    resetScanUi();
    if (els.scanProgressTrack) els.scanProgressTrack.classList.remove('indeterminate');
    return;
  }

  els.scanProgress.hidden = false;
  if (els.appStatus) els.appStatus.hidden = true;
  if (els.scanProgressText) els.scanProgressText.textContent = text;
  if (els.scanCancelBtn) els.scanCancelBtn.hidden = false;
  applyBar(els.scanProgressTrack, els.scanProgressFill);

  if (els.scanOverlay) {
    els.scanOverlay.hidden = false;
    if (els.scanOverlayText) els.scanOverlayText.textContent = text;
    applyBar(els.scanOverlayTrack, els.scanOverlayFill);
  }
}

function setScanUiBusy(busy) {
  state.scanBusy = busy;
  if (els.openFolderBtn) els.openFolderBtn.disabled = busy;
  if (els.fileMenuBtn) els.fileMenuBtn.disabled = busy;
  if (els.menuRescan) els.menuRescan.disabled = busy || !state.folderPath;
  if (els.menuAddSurfaces) els.menuAddSurfaces.disabled = busy || !state.folderPath;
  if (els.menuLoadProject) els.menuLoadProject.disabled = busy;
  if (els.scanCancelBtn) els.scanCancelBtn.disabled = !busy;
}

async function cancelActiveScan() {
  if (!state.scanBusy || !api?.cancelScan) return;
  if (els.scanCancelBtn) els.scanCancelBtn.disabled = true;
  if (els.scanOverlayText) els.scanOverlayText.textContent = 'Cancelling scan…';
  try {
    await api.cancelScan();
  } catch (err) {
    console.warn('Cancel scan failed', err);
  }
}

function requireApi() {
  if (!api) {
    throw new Error('App bridge unavailable. Run with npm start (Electron), not by opening index.html in a browser.');
  }
}

async function getViewer() {
  if (viewer) return viewer;
  const mod = await import('./viewer3d.bundle.js');
  viewer = new mod.UnitViewer3d(els.viewerHost);
  viewer.onSurfacePick = (surfaceNumber) => selectSurface(surfaceNumber, { fromViewer: true });
  viewer.onSurfaceHide = (surfaceNumber) => setSurfaceHidden(surfaceNumber, true);
  viewer.onSurfaceInfoRequest = (surfaceNumber, clientX, clientY) =>
    showSurfaceInfoPopup(surfaceNumber, clientX, clientY);
  if (state.options) viewer.setViewerOptions(getViewerOptions(state.options));
  return viewer;
}

function closeFileMenu() {
  els.fileMenuPanel.hidden = true;
  els.fileMenuBtn.setAttribute('aria-expanded', 'false');
}

function toggleFileMenu() {
  const open = els.fileMenuPanel.hidden;
  els.fileMenuPanel.hidden = !open;
  els.fileMenuBtn.setAttribute('aria-expanded', open ? 'true' : 'false');
}

function setupFileMenu() {
  els.fileMenuBtn.addEventListener('click', (e) => {
    e.stopPropagation();
    toggleFileMenu();
  });
  document.addEventListener('click', (e) => {
    if (!els.fileMenuPanel.hidden && !e.target.closest('.menu-dropdown')) closeFileMenu();
  });
  els.menuSaveProject.addEventListener('click', () => {
    closeFileMenu();
    void saveProjectNow();
  });
  els.menuSaveProjectAs.addEventListener('click', () => {
    closeFileMenu();
    void saveProjectAs();
  });
  els.menuLoadProject.addEventListener('click', () => {
    closeFileMenu();
    void loadProjectFile();
  });
  els.menuRecent.addEventListener('click', () => {
    closeFileMenu();
    void openRecentDialog();
  });
  els.menuRescan.addEventListener('click', () => {
    closeFileMenu();
    void rescanFolder();
  });
  if (els.menuAddSurfaces) {
    els.menuAddSurfaces.addEventListener('click', () => {
      closeFileMenu();
      void addSurfacesFromFolders();
    });
  }
  els.menuImportJson.addEventListener('click', () => {
    closeFileMenu();
    void importProjectJson();
  });
  if (els.menuImportBom) {
    els.menuImportBom.addEventListener('click', () => {
      closeFileMenu();
      void importBomSpreadsheet();
    });
  }
  els.menuExportJson.addEventListener('click', () => {
    closeFileMenu();
    void exportProject('json');
  });
  els.menuExportMd.addEventListener('click', () => {
    closeFileMenu();
    void exportProject('md');
  });
}

function setupPanelResizers() {
  setupOneResizer(els.leftResizer, 'left');
  setupOneResizer(els.rightResizer, 'right');
}

function setupOneResizer(handle, side) {
  if (!handle) return;
  let startX = 0;
  let startWidth = 0;

  const onMove = (e) => {
    const dx = e.clientX - startX;
    const layout = normalizeLayout(state.options?.layout);
    if (side === 'left') {
      layout.leftWidth = startWidth + dx;
    } else {
      layout.rightWidth = startWidth - dx;
    }
    state.options.layout = layout;
    applyLayout(layout);
    if (viewer) viewer.resize();
  };

  const onUp = () => {
    handle.classList.remove('dragging');
    document.removeEventListener('pointermove', onMove);
    document.removeEventListener('pointerup', onUp);
    scheduleOptionsSave();
  };

  handle.addEventListener('pointerdown', (e) => {
    e.preventDefault();
    if (!state.options) state.options = defaultOptions();
    startX = e.clientX;
    startWidth = side === 'left' ? normalizeLayout(state.options.layout).leftWidth : normalizeLayout(state.options.layout).rightWidth;
    handle.classList.add('dragging');
    document.addEventListener('pointermove', onMove);
    document.addEventListener('pointerup', onUp);
  });
}

function applyOptionsToUi(options) {
  applyUiTheme(options.uiTheme);
  applyLayout(options.layout);
  if (viewer) viewer.setViewerOptions(getViewerOptions(options));
}

function markProjectDirty() {
  state.projectDirty = true;
}

function markProjectClean() {
  state.projectDirty = false;
}

function buildCurrentProjectPayload() {
  return buildProjectSavePayload(
    state.folderPath,
    state.surfaces,
    state.projectData,
    state.scanSource
  );
}

async function writeFolderProjectCache() {
  if (!state.folderPath) return null;
  return api.saveProjectData(state.folderPath, state.projectData);
}

async function writeProjectFile() {
  if (!state.projectFilePath) return null;
  return api.saveProjectFile(state.projectFilePath, buildCurrentProjectPayload());
}

async function saveProjectNow() {
  requireApi();
  if (!state.folderPath && !state.projectFilePath) {
    alert('Open a folder or load a project before saving.');
    return;
  }
  clearTimeout(state.saveTimer);
  try {
    const folderResult = await writeFolderProjectCache();
    const fileResult = await writeProjectFile();
    markProjectClean();
    const savedPath = fileResult?.path || folderResult?.path;
    setAppStatus(`Saved to ${savedPath}`);
    setTimeout(() => setAppStatus(''), 4000);
  } catch (err) {
    setAppStatus(`Save failed: ${err.message || err}`, true);
  }
}

async function saveProjectAs() {
  requireApi();
  if (!state.folderPath && state.surfaces.length === 0) {
    alert('Open a folder before saving.');
    return;
  }
  clearTimeout(state.saveTimer);
  if (state.folderPath) {
    await writeFolderProjectCache();
  }
  const result = await api.saveProjectAs(state.folderPath, buildCurrentProjectPayload());
  if (result.canceled) return;
  state.projectFilePath = result.path;
  markProjectClean();
  setAppStatus(`Saved project to ${result.path}`);
  setTimeout(() => setAppStatus(''), 4000);
}

async function loadProjectFile() {
  requireApi();
  const result = await api.pickLoadProject();
  if (result.canceled) return;

  const parsed = parseSavedProjectFile(result.data);
  if (!parsed) {
    alert('Unrecognized project file format.');
    return;
  }

  await applySavedProject(parsed, { filePath: result.filePath });
  setTimeout(() => setAppStatus(''), 5000);
}

async function applySavedProject(parsed, { filePath = '', quiet = false } = {}) {
  let surfaces = parsed.geometrySurfaces || [];
  let projectData = migrateProjectData(parsed.projectPayload, parsed.sourceFolder);

  if (surfaces.length === 0 && parsed.sourceFolder) {
    try {
      const fromFolder = await api.loadProjectFolder(parsed.sourceFolder, {
        ...parsed.projectPayload,
        geometrySurfaces: parsed.geometrySurfaces,
      });
      surfaces = fromFolder.surfaces || [];
      projectData = migrateProjectData(fromFolder.projectData, parsed.sourceFolder);
    } catch (err) {
      console.warn('Could not load folder cache', err);
    }
  }

  state.folderPath = parsed.sourceFolder || null;
  state.projectFilePath = filePath || null;
  projectData = migrateProjectData(projectData, parsed.sourceFolder);
  state.surfaces = filterExcludedGeometry(surfaces, projectData);
  state.loadErrors = [];
  state.scanSource = state.surfaces.length ? parsed.scanSource || 'saved' : 'none';
  state.projectData = mergeScanWithProject(state.surfaces, projectData);

  for (const surface of state.surfaces) {
    const fp = geometryFingerprint(surface);
    for (const num of Object.keys(state.projectData.retired || {})) {
      if (!state.projectData.retired[num].geometryFingerprint) {
        const snap = state.projectData.retired[num].snapshot;
        if (snap?.geometryFingerprint) {
          state.projectData.retired[num].geometryFingerprint = snap.geometryFingerprint;
        }
      }
    }
    const record = getSurfaceRecord(surface.surfaceNumber);
    record.geometryFingerprint = fp;
  }

  if (parsed.sourceFolder) {
    try {
      await api.rememberProject(parsed.sourceFolder);
    } catch (err) {
      console.warn('Could not update recent projects', err);
    }
  }

  markProjectClean();
  applyLoadedFolder({ quiet });

  const fileName = filePath ? filePath.replace(/^.*[/\\]/, '') : '';
  if (fileName && !state.folderPath) {
    els.folderLabel.textContent = fileName;
    els.folderLabel.title = filePath;
  }

  if (!quiet) {
    if (surfaces.length > 0) {
      const offline = !parsed.sourceFolder ? ' (offline)' : '';
      setAppStatus(`Loaded ${surfaces.length} surfaces from saved file${offline}.`);
    } else if (parsed.sourceFolder) {
      setAppStatus('Project tracking loaded — no geometry in file. Use Open Folder or Rescan if IAMs exist.');
    } else {
      setAppStatus('Project tracking loaded — no geometry in file.');
    }
  }
}

function closeSurfaceInfoPopup() {
  if (!els.surfaceInfoPopup || els.surfaceInfoPopup.hidden) return;
  els.surfaceInfoPopup.hidden = true;
  detachSurfaceInfoPopupOutside();
}

function onSurfaceInfoPopupOutside(event) {
  if (els.surfaceInfoPopup.hidden) return;
  if (els.surfaceInfoPopup.contains(event.target)) return;
  closeSurfaceInfoPopup();
}

function attachSurfaceInfoPopupOutside() {
  detachSurfaceInfoPopupOutside();
  requestAnimationFrame(() => {
    document.addEventListener('pointerdown', onSurfaceInfoPopupOutside, true);
  });
}

function detachSurfaceInfoPopupOutside() {
  document.removeEventListener('pointerdown', onSurfaceInfoPopupOutside, true);
}

function setupSurfaceInfoPopup() {
  if (!els.surfaceInfoPopup) return;
  els.surfaceInfoPopup.addEventListener('dblclick', (event) => {
    if (event.button !== 0) return;
    event.stopPropagation();
    closeSurfaceInfoPopup();
  });
}

function showSurfaceInfoPopup(surfaceNumber, clientX, clientY) {
  const surface = state.surfaces.find((s) => s.surfaceNumber === surfaceNumber);
  if (!surface || !els.surfaceInfoPopup) return;
  const lines = buildSurfaceInfoLines(surface);
  if (!lines.length) return;

  els.surfaceInfoPopup.innerHTML = lines
    .map(
      (line) =>
        `<div class="info-line"><span class="info-label">${escapeHtml(line.label)}:</span><span class="info-value">${escapeHtml(line.value)}</span></div>`
    )
    .join('');
  els.surfaceInfoPopup.hidden = false;

  const pad = 12;
  const popupRect = els.surfaceInfoPopup.getBoundingClientRect();
  let left = clientX + pad;
  let top = clientY + pad;
  if (left + popupRect.width > window.innerWidth - pad) left = clientX - popupRect.width - pad;
  if (top + popupRect.height > window.innerHeight - pad) top = clientY - popupRect.height - pad;
  els.surfaceInfoPopup.style.left = `${Math.max(pad, left)}px`;
  els.surfaceInfoPopup.style.top = `${Math.max(pad, top)}px`;

  attachSurfaceInfoPopupOutside();
}

function bindEvents() {
  if (eventsBound) return;
  eventsBound = true;

  setupFileMenu();
  setupPanelResizers();
  setupSurfaceInfoPopup();

  els.openFolderBtn.addEventListener('click', () => void openFolder());
  if (els.scanCancelBtn) els.scanCancelBtn.addEventListener('click', () => void cancelActiveScan());
  els.fitViewBtn.addEventListener('click', () => {
    if (viewer) viewer.fitView();
  });
  els.showAllBtn.addEventListener('click', () => showAllSurfaces());
  els.optionsBtn.addEventListener('click', () => openOptions());
  els.optionsCloseBtn.addEventListener('click', () => els.optionsDialog.close());
  els.optionsForm.addEventListener('submit', (e) => {
    e.preventDefault();
    void saveOptionsFromEditor();
  });
  els.resetOptionsBtn.addEventListener('click', () => void resetOptionsDefaults());
  els.addStateBtn.addEventListener('click', () => void addStateRow());
  els.addChecklistBtn.addEventListener('click', addChecklistRow);
  els.closeDetailBtn.addEventListener('click', closeDetailPanel);
  els.stateSelect.addEventListener('change', onStateChange);
  els.notesInput.addEventListener('input', onNotesInput);
  els.linkRenumberBtn.addEventListener('click', () => void linkSelectedPrevious('renumber'));
  els.linkReplaceBtn.addEventListener('click', () => void linkSelectedPrevious('replaced'));
  els.applyRenumberBtn.addEventListener('click', () => void applyManualRenumber());
  if (els.replaceFromFolderBtn) {
    els.replaceFromFolderBtn.addEventListener('click', () => void replaceSurfaceFromFolder());
  }
  if (els.retireSurfaceBtn) {
    els.retireSurfaceBtn.addEventListener('click', () => void retireCurrentSurface());
  }
  els.recentCloseBtn.addEventListener('click', () => els.recentDialog.close());
  els.recentCloseFooterBtn.addEventListener('click', () => els.recentDialog.close());
  els.opacitySlider.addEventListener('input', onOpacitySliderInput);
  els.optionsOpacitySlider.addEventListener('input', onOptionsOpacitySliderInput);
  els.listNameMode.addEventListener('change', onListDisplayChange);
  els.listSortMode.addEventListener('change', onListDisplayChange);
  els.listShowTypeTag.addEventListener('change', onListDisplayChange);
  els.listShowSkidTag.addEventListener('change', onListDisplayChange);
  els.listShowSideTag.addEventListener('change', onListDisplayChange);

  if (els.viewSurfacesTab) {
    els.viewSurfacesTab.addEventListener('click', () => setActiveView('surfaces'));
  }
  if (els.viewBomTab) {
    els.viewBomTab.addEventListener('click', () => setActiveView('bom'));
  }

  setupBomAddDialog();

  window.addEventListener('beforeunload', (e) => {
    if (state.projectDirty) {
      e.preventDefault();
      e.returnValue = '';
    }
  });
}

function getSurfaceOpacity() {
  const value = state.options?.surfaceOpacity;
  return typeof value === 'number' ? Math.min(1, Math.max(0.25, value)) : 0.9;
}

function opacityToPercent(opacity) {
  return `${Math.round(opacity * 100)}%`;
}

function syncOpacityControls() {
  const opacity = getSurfaceOpacity();
  const pct = Math.round(opacity * 100);
  if (els.opacitySlider) {
    els.opacitySlider.value = String(pct);
    els.opacityValue.textContent = opacityToPercent(opacity);
  }
  if (els.optionsOpacitySlider && !state.draftOptions) {
    els.optionsOpacitySlider.value = String(pct);
    els.optionsOpacityValue.textContent = opacityToPercent(opacity);
  }
  if (els.opacityControl) {
    els.opacityControl.hidden = state.surfaces.length === 0;
  }
}

function onOpacitySliderInput() {
  const opacity = Number(els.opacitySlider.value) / 100;
  els.opacityValue.textContent = opacityToPercent(opacity);
  state.options.surfaceOpacity = opacity;
  if (viewer) viewer.setSurfaceOpacity(opacity);
  scheduleOptionsSave();
}

function onOptionsOpacitySliderInput() {
  if (!state.draftOptions) return;
  const opacity = Number(els.optionsOpacitySlider.value) / 100;
  els.optionsOpacityValue.textContent = opacityToPercent(opacity);
  state.draftOptions.surfaceOpacity = opacity;
}

function ensureListDisplayOptions() {
  if (!state.options) state.options = defaultOptions();
  state.options.listDisplay = normalizeListDisplay(state.options.listDisplay);
}

function syncListDisplayControls() {
  ensureListDisplayOptions();
  const ld = state.options.listDisplay;
  if (els.listNameMode) els.listNameMode.value = ld.nameMode;
  if (els.listSortMode) els.listSortMode.value = ld.sortMode;
  if (els.listShowTypeTag) els.listShowTypeTag.checked = ld.showTypeTag;
  if (els.listShowSkidTag) els.listShowSkidTag.checked = ld.showSkidTag;
  if (els.listShowSideTag) els.listShowSideTag.checked = ld.showSideTag;
}

function onListDisplayChange() {
  ensureListDisplayOptions();
  state.options.listDisplay = normalizeListDisplay({
    nameMode: els.listNameMode.value,
    sortMode: els.listSortMode.value,
    showTypeTag: els.listShowTypeTag.checked,
    showSkidTag: els.listShowSkidTag.checked,
    showSideTag: els.listShowSideTag.checked,
  });
  renderSurfaceList();
  scheduleOptionsSave();
}

let optionsSaveTimer = null;
function scheduleOptionsSave() {
  if (!api) return;
  clearTimeout(optionsSaveTimer);
  optionsSaveTimer = setTimeout(async () => {
    await api.saveOptions(state.options);
  }, 400);
}

async function init() {
  bindEvents();
  resetScanUi();
  if (els.appVersion && api?.appVersion) els.appVersion.textContent = `v${api.appVersion}`;
  state.options = defaultOptions();

  if (!api) {
    setAppStatus('Run with npm start — do not open index.html in a browser.', true);
    return;
  }

  try {
    state.options = await api.loadOptions();
    state.options.listDisplay = normalizeListDisplay(state.options.listDisplay);
    state.options.layout = normalizeLayout(state.options.layout);
    state.options.uiTheme = normalizeUiTheme(state.options.uiTheme);
    state.options.viewer = normalizeViewerOptions(state.options.viewer);
    state.options.bomListDisplay = normalizeBomListDisplay(state.options.bomListDisplay);
    setAppStatus('');
  } catch (err) {
    console.error(err);
    setAppStatus(`Could not load saved options: ${err.message || err}`, true);
  }

  applyOptionsToUi(state.options);
  renderLegend();
  syncOpacityControls();
  syncListDisplayControls();
  setFolderUiEnabled(false);

  await api.allowFolderScans();
}

function defaultSurfaceRecord() {
  const defaultStateId = state.options.states[0]?.id || 'current';
  return normalizeSurfaceRecord({ stateId: defaultStateId });
}

function getSurfaceRecord(surfaceNumber) {
  if (!state.projectData.surfaces[surfaceNumber]) {
    state.projectData.surfaces[surfaceNumber] = defaultSurfaceRecord();
  }
  return state.projectData.surfaces[surfaceNumber];
}

function isSurfaceHidden(surfaceNumber) {
  return Boolean(state.projectData.surfaces[surfaceNumber]?.hidden);
}

function getStateById(stateId) {
  return state.options.states.find((s) => s.id === stateId) || state.options.states[0];
}

function getColorForSurface(surfaceNumber) {
  return getSurfaceAppearanceForSurface(surfaceNumber).color;
}

function getSurfaceAppearanceForSurface(surfaceNumber) {
  const record = state.projectData.surfaces[surfaceNumber];
  const stateId = record?.stateId || state.options.states[0]?.id;
  const st = getStateById(stateId);
  return getSurfaceAppearanceFromState(st);
}

function applySwatchStyle(el, appearanceOrState) {
  const appearance = appearanceOrState?.fillType
    ? normalizeStateAppearance(appearanceOrState)
    : getSurfaceAppearanceFromState(appearanceOrState);
  const swatch = getSwatchBackground(appearance);
  el.style.backgroundColor = swatch.backgroundColor;
  el.style.backgroundImage = swatch.backgroundImage || '';
}

function scheduleSave() {
  if (!state.folderPath || !api) return;
  markProjectDirty();
  clearTimeout(state.saveTimer);
  state.saveTimer = setTimeout(async () => {
    await api.saveProjectData(state.folderPath, state.projectData);
    markProjectClean();
  }, 400);
}

function getBomState() {
  return normalizeBomState(state.projectData?.bom);
}

function setBomState(nextBom) {
  const priorRecords = state.projectData?.bom?.entryRecords;
  state.projectData.bom = bomForPersistence({
    ...nextBom,
    entryRecords: nextBom?.entryRecords || priorRecords || {},
  });
  scheduleSave();
  renderBomPanel();
  if (state.detailMode === 'bom' && state.selectedBomEntryKey) {
    const entry = findBomEntryByKey(state.selectedBomEntryKey);
    if (entry) openBomDetailPanel(entry);
    else closeDetailPanel();
  }
}

function findBomEntryByKey(entryKey) {
  if (!entryKey) return null;
  return getBomState()?.plan?.entries?.find((entry) => bomEntryKey(entry) === entryKey) || null;
}

function getBomEntryRecord(entryKey) {
  if (!state.projectData.bom) state.projectData.bom = bomForPersistence({ entryRecords: {} });
  if (!state.projectData.bom.entryRecords) state.projectData.bom.entryRecords = {};
  if (!state.projectData.bom.entryRecords[entryKey]) {
    state.projectData.bom.entryRecords[entryKey] = normalizeBomEntryRecord(null);
  }
  return state.projectData.bom.entryRecords[entryKey];
}

function setActiveView(view) {
  const bomActive = view === 'bom';
  if (bomActive !== (state.activeView === 'bom')) {
    closeDetailPanel();
  }
  state.activeView = bomActive ? 'bom' : 'surfaces';
  if (els.viewSurfacesTab) {
    els.viewSurfacesTab.classList.toggle('active', !bomActive);
    els.viewSurfacesTab.setAttribute('aria-pressed', bomActive ? 'false' : 'true');
  }
  if (els.viewBomTab) {
    els.viewBomTab.classList.toggle('active', bomActive);
    els.viewBomTab.setAttribute('aria-pressed', bomActive ? 'true' : 'false');
  }
  if (els.surfaceListView) els.surfaceListView.hidden = bomActive;
  if (els.surfacePanelActions) els.surfacePanelActions.hidden = bomActive;
  if (els.bomPanel) els.bomPanel.hidden = !bomActive;
  if (els.appMain) els.appMain.classList.toggle('bom-view-active', bomActive);
  if (bomActive) renderBomPanel();
}

function renderBomPanel({ preserveSearchFocus = false } = {}) {
  if (!els.bomPanel) return;
  const active = document.activeElement;
  const searchHadFocus =
    preserveSearchFocus &&
    active &&
    active.type === 'search' &&
    els.bomPanel.contains(active);
  const selectionStart = searchHadFocus ? active.selectionStart : null;
  const selectionEnd = searchHadFocus ? active.selectionEnd : null;

  const bom = getBomState();
  renderBomPage(els.bomPanel, bom, {
    onImport: () => void importBomSpreadsheet(),
    onImportConfig: () => void importUnitConfig(),
    onAddEntry: () => openBomAddDialog(),
    onCreateFolders: () => void createShellFolders(),
    onPickShellRoot: () => void pickShellRoot({ relocate: Boolean(bom.shellRoot) }),
    onOpenFolder: (entry) => void openBomAssemblyFolder(entry),
    onRemoveEntry: (entry) => void removeBomEntryFromList(entry),
    onSelectEntry: (entry) => selectBomEntry(entry),
    onListDisplayChange: (patch) => onBomListDisplayChange(patch),
  }, getBomListDisplay(state.options), {
    selectedEntryKey: state.selectedBomEntryKey,
  });

  if (searchHadFocus) {
    const searchInput = els.bomPanel.querySelector('input[type="search"]');
    if (searchInput) {
      searchInput.focus();
      if (selectionStart != null && selectionEnd != null) {
        searchInput.setSelectionRange(selectionStart, selectionEnd);
      }
    }
  }
}

let bomListDisplayTimer = null;
function onBomListDisplayChange(patch) {
  if (!state.options) state.options = defaultOptions();
  state.options.bomListDisplay = normalizeBomListDisplay({
    ...getBomListDisplay(state.options),
    ...(patch && typeof patch === 'object' ? patch : {}),
  });
  renderBomPanel({ preserveSearchFocus: patch?.searchText != null });
  clearTimeout(bomListDisplayTimer);
  const delay = patch?.searchText != null ? 350 : 0;
  bomListDisplayTimer = setTimeout(async () => {
    if (api) await api.saveOptions(state.options);
  }, delay);
}

async function importBomSpreadsheet() {
  requireApi();
  try {
    const result = await api.pickImportBomXlsx();
    if (result.canceled) return;
    const prior = getBomState();
    const bom = bomStateFromImport(result.filePath, result.rows, prior);
    setBomState(bom);
    setActiveView('bom');
    const misplaced = bom.plan?.misplaced?.length || 0;
    let message = `Imported BOM (${bom.plan?.stats?.folderCount || 0} Inventor folders planned).`;
    if (misplaced) message += ` ${misplaced} coil panel line(s) need segment fixes.`;
    setAppStatus(message);
    setTimeout(() => setAppStatus(''), 6000);
  } catch (err) {
    setAppStatus(`BOM import failed: ${err.message || err}`, true);
    alert(`BOM import failed:\n\n${err.message || err}`);
  }
}

async function importUnitConfig() {
  requireApi();
  try {
    const result = await api.pickImportUnitConfig();
    if (result.canceled) return;
    const unitConfig = parseUnitConfigXml(result.text, {
      sourceFile: result.filePath,
      importedAt: new Date().toISOString(),
    });
    const bom = attachUnitConfig(getBomState(), unitConfig);
    setBomState(bom);
    setActiveView('bom');
    const skidCount = unitConfig.skids.length;
    let message = `Imported unit Config (${skidCount} shipping skid${skidCount === 1 ? '' : 's'}).`;
    if (unitConfig.warnings?.length) {
      message += ` ${unitConfig.warnings.length} warning(s) — see BOM view.`;
    }
    if (bom.plan?.stats?.folderCount != null && (bom.rows?.length || bom.manualRows?.length)) {
      message += ` ${bom.plan.stats.folderCount} folders planned.`;
    }
    setAppStatus(message);
    setTimeout(() => setAppStatus(''), 6000);
  } catch (err) {
    setAppStatus(`Config import failed: ${err.message || err}`, true);
    alert(`Config import failed:\n\n${err.message || err}`);
  }
}

function setBomAddError(message) {
  if (!els.bomAddError) return;
  if (!message) {
    els.bomAddError.hidden = true;
    els.bomAddError.textContent = '';
    return;
  }
  els.bomAddError.hidden = false;
  els.bomAddError.textContent = message;
}

function setupBomAddDialog() {
  if (!els.bomAddForm || !els.bomAddDialog) return;

  const closeDialog = () => {
    setBomAddError('');
    els.bomAddDialog.close();
  };

  els.bomAddCloseBtn?.addEventListener('click', closeDialog);
  els.bomAddCancelBtn?.addEventListener('click', closeDialog);
  els.bomAddDialog.addEventListener('cancel', (e) => {
    e.preventDefault();
    closeDialog();
  });

  els.bomAddSkid?.addEventListener('change', () => {
    setBomAddError('');
    populateBomAddSegmentSelect(els.bomAddSegment, getBomState(), els.bomAddSkid.value);
  });

  els.bomAddForm.addEventListener('submit', (e) => {
    e.preventDefault();
    setBomAddError('');
    const fields = {
      partNumber: els.bomAddPart?.value.trim() || '',
      skid: els.bomAddSkid?.value.trim() || '',
      segment: els.bomAddSegment?.value.trim() || '',
      description: els.bomAddDesc?.value.trim() || '',
      extDescription: els.bomAddExt?.value.trim() || '',
      quantity: els.bomAddQty?.value.trim() || '1',
      unit: getBomState()?.unit || '',
    };
    try {
      const next = addManualBomEntry(getBomState(), fields);
      setBomState(next);
      closeDialog();
      setActiveView('bom');
      setAppStatus(`Added ${fields.partNumber} to export list.`);
      setTimeout(() => setAppStatus(''), 4000);
    } catch (err) {
      setBomAddError(err.message || String(err));
      if (String(err.message || '').includes('segment')) {
        els.bomAddSegment?.focus();
      } else {
        els.bomAddPart?.focus();
      }
    }
  });
}

function openBomAddDialog() {
  if (!els.bomAddDialog || !els.bomAddForm) return;
  const bom = getBomState();
  els.bomAddForm.reset();
  if (els.bomAddQty) els.bomAddQty.value = '1';
  setBomAddError('');
  populateBomAddSkidSelect(els.bomAddSkid, bom);
  populateBomAddSegmentSelect(els.bomAddSegment, bom, '');
  els.bomAddDialog.showModal();
  requestAnimationFrame(() => els.bomAddPart?.focus());
}

function removeBomEntryFromList(entry) {
  const label = entry?.partNumber || 'this entry';
  const prompt = entry?.isManual
    ? `Delete manual entry ${label}?`
    : `Remove ${label} from the Inventor export list?\n\n(Re-import the BOM to restore removed entries.)`;
  if (!confirm(prompt)) return;
  try {
    if (state.selectedBomEntryKey === bomEntryKey(entry)) {
      closeDetailPanel();
    }
    const next = removeBomEntry(getBomState(), entry);
    setBomState(next);
    setAppStatus(`Removed ${label} from export list.`);
    setTimeout(() => setAppStatus(''), 4000);
  } catch (err) {
    alert(err.message || err);
  }
}

function selectBomEntry(entry) {
  if (!entry) return;
  state.selectedBomEntryKey = bomEntryKey(entry);
  state.selectedSurfaceNumber = null;
  state.detailMode = 'bom';
  if (viewer) viewer.setSelection(null);
  renderBomPanel();
  openBomDetailPanel(entry);
}

function setDetailPanelMode(mode) {
  if (els.detailSurfaceFields) els.detailSurfaceFields.hidden = mode !== 'surface';
  if (els.renumberHistorySection) els.renumberHistorySection.hidden = mode !== 'surface';
  els.notesInput.placeholder =
    mode === 'bom' ? 'Add notes for this assembly…' : 'Add notes for this surface…';
}

function renderDetailChecklist(record, { readonly = false } = {}) {
  els.checklist.innerHTML = '';
  for (const item of state.options.checklistItems) {
    const li = document.createElement('li');
    const label = document.createElement('label');
    const input = document.createElement('input');
    input.type = 'checkbox';
    input.dataset.itemId = item.id;
    input.checked = Boolean(record.checklist && record.checklist[item.id]);
    input.disabled = readonly;
    if (!readonly) input.addEventListener('change', onChecklistChange);
    label.append(input, document.createTextNode(item.label));
    li.appendChild(label);
    els.checklist.appendChild(li);
  }
}

function openBomDetailPanel(entry) {
  if (!entry) return;
  const record = getBomEntryRecord(bomEntryKey(entry));

  setDetailPanelMode('bom');
  els.detailPanel.hidden = false;
  els.detailTitle.textContent = entry.partNumber;
  els.detailMeta.innerHTML = [
    `<div>${escapeHtml(entry.description)}${entry.extDescription ? ` · ${escapeHtml(entry.extDescription)}` : ''}</div>`,
    entry.skid ? `<div>Skid: ${escapeHtml(entry.skid)}</div>` : '',
    entry.segment ? `<div>Segment: ${escapeHtml(entry.segment)}</div>` : '',
    entry.segmentFolder ? `<div>Segment folder: ${escapeHtml(entry.segmentFolder)}</div>` : '',
    entry.relativePath ? `<div>Export path: ${escapeHtml(entry.relativePath)}</div>` : '',
    entry.isDuplicateRef && entry.modelRelativePath
      ? `<div class="meta-flag">Model folder: ${escapeHtml(entry.modelRelativePath)}</div>`
      : '',
    entry.isModelPrimary ? '<div class="meta-flag">Primary model location (shared part)</div>' : '',
    entry.isManual ? '<div class="meta-flag">Manual entry</div>' : '',
    entry.isCustomSq ? '<div class="meta-flag">SQ custom assembly</div>' : '',
  ].join('');

  renderDetailChecklist(record);
  els.notesInput.value = record.notes || '';
}

async function pickShellRoot({ relocate = false } = {}) {
  requireApi();
  const bom = getBomState();
  if (!bom.rows.length) {
    alert('Import a BOM first.');
    return;
  }
  const result = await api.pickShellRoot();
  if (result.canceled) return;
  setBomState(attachShellRoot(bom, result.folderPath));
  setAppStatus(relocate ? `Inventor root relocated to ${result.folderPath}` : `Inventor root set to ${result.folderPath}`);
  setTimeout(() => setAppStatus(''), 5000);
}

async function ensureShellRootAvailable() {
  const bom = getBomState();
  if (!bom.shellRoot) {
    await pickShellRoot();
    return getBomState().shellRoot;
  }
  const check = await api.pathExists(bom.shellRoot);
  if (check.exists) return bom.shellRoot;
  const retry = confirm(
    `The Inventor root folder was not found:\n${bom.shellRoot}\n\nChoose a new location?`
  );
  if (!retry) return null;
  await pickShellRoot({ relocate: true });
  return getBomState().shellRoot;
}

async function createShellFolders() {
  requireApi();
  const bom = getBomState();
  if (!bom.plan?.entries?.length) {
    alert('Import a BOM with matching 391- assemblies first.');
    return;
  }
  const rootPath = await ensureShellRootAvailable();
  if (!rootPath) return;
  const folderSpecs = bom.plan.entries.map((entry) => ({
    relativePath: entry.relativePath,
    referenceRelativePath: entry.isDuplicateRef ? entry.modelRelativePath : null,
  }));
  try {
    const result = await api.createShellFolders(rootPath, folderSpecs);
    const next = attachShellRoot({
      ...bom,
      foldersCreatedAt: new Date().toISOString(),
    }, rootPath);
    setBomState(next);
    setAppStatus(`Created ${result.count} Inventor folder${result.count === 1 ? '' : 's'} under ${rootPath}`);
    setTimeout(() => setAppStatus(''), 6000);
  } catch (err) {
    setAppStatus(`Create folders failed: ${err.message || err}`, true);
    alert(`Create folders failed:\n\n${err.message || err}`);
  }
}

async function openBomAssemblyFolder(entry) {
  requireApi();
  const rootPath = await ensureShellRootAvailable();
  if (!rootPath || !entry?.relativePath) return;
  const openPath = entry.isDuplicateRef && entry.modelRelativePath
    ? entry.modelRelativePath
    : entry.relativePath;
  try {
    await api.openShellFolder(rootPath, openPath);
  } catch (err) {
    const retry = confirm(`${err.message || err}\n\nChoose a new Inventor root?`);
    if (retry) {
      await pickShellRoot({ relocate: true });
      const nextRoot = getBomState().shellRoot;
      if (nextRoot) {
        try {
          await api.openShellFolder(nextRoot, openPath);
        } catch (err2) {
          alert(err2.message || err2);
        }
      }
    }
  }
}

function setFolderUiEnabled(enabled) {
  els.fitViewBtn.disabled = !enabled;
  els.showAllBtn.disabled = !enabled;
  if (els.menuRescan) els.menuRescan.disabled = !enabled || !state.folderPath;
  if (els.menuAddSurfaces) els.menuAddSurfaces.disabled = !enabled || !state.folderPath;
  if (els.menuImportJson) els.menuImportJson.disabled = !enabled || !state.folderPath;
  if (els.menuExportJson) els.menuExportJson.disabled = !enabled;
  if (els.menuExportMd) els.menuExportMd.disabled = !enabled;
  if (els.menuImportBom) els.menuImportBom.disabled = false;
}

async function openFolder() {
  try {
    requireApi();
    if (state.scanBusy) return;
    els.openFolderBtn.disabled = true;
    setAppStatus('Choose a folder…');
    const folderPath = await api.pickFolder();
    if (!folderPath) {
      setAppStatus('');
      return;
    }
    await loadFolder(folderPath);
    setAppStatus('');
  } catch (err) {
    console.error(err);
    setAppStatus(`Open folder failed: ${err.message || err}`, true);
    alert(`Open folder failed:\n\n${err.message || err}`);
  } finally {
    els.openFolderBtn.disabled = false;
  }
}

async function rescanFolder() {
  if (!state.folderPath || state.scanBusy) return;
  await loadFolder(state.folderPath, { rescan: true });
}

async function persistGeometryCache() {
  if (!state.folderPath || !api?.writeGeometryCache) return;
  await api.writeGeometryCache(state.folderPath, state.surfaces, state.scanSource);
}

async function runIncrementalScan(folderPaths, iamPaths = null) {
  requireApi();
  setScanUiBusy(true);
  try {
    setScanProgress(true, { phase: 'discovering', message: 'Scanning…' });
    const result = await api.scanSurfacesInFolders(folderPaths || [], (payload) => {
      setScanProgress(true, payload);
    }, iamPaths);
    if (result.errors?.length) {
      renderLoadErrors(result.errors);
    }
    return result;
  } finally {
    setScanProgress(false);
    setScanUiBusy(false);
  }
}

function formatIncrementalScanFailure(result) {
  const iamCount = result?.iamDiscovered ?? 0;
  const lines = [];

  if (iamCount === 0) {
    lines.push('No 391Z .iam file found at that location.');
    lines.push('Pick the folder that contains the .iam, or use Replace from IAM… to select the file directly.');
    lines.push('Expected name like 391Z010142-0123.iam');
  } else if (!result?.surfaces?.length) {
    lines.push(`Found ${iamCount} IAM file(s) but could not build surface geometry.`);
    if (result?.errors?.length) {
      lines.push('');
      for (const err of result.errors.slice(0, 4)) {
        const name = String(err.filePath || '').split(/[/\\]/).pop() || err.filePath;
        lines.push(`• ${name}: ${err.error}`);
      }
      if (result.errors.length > 4) lines.push(`…and ${result.errors.length - 4} more`);
    }
    lines.push('');
    lines.push('Common fixes:');
    lines.push('• Inventor must be installed; pywin32: pip install pywin32');
    lines.push('• IAM needs DOCUMENT_CONFIG_JSON (detailing export) or a same-name .json sidecar');
  }

  return lines.join('\n');
}

function activeSurfaceKeys() {
  return new Set(state.surfaces.map((s) => s.surfaceNumber));
}

async function replaceSurfaceFromFolder() {
  requireApi();
  if (!state.folderPath) {
    alert('Open the unit folder first.');
    return;
  }
  if (!state.selectedSurfaceNumber || state.viewingRemovedKey) {
    alert('Select an active surface to replace.');
    return;
  }

  const oldKey = state.selectedSurfaceNumber;
  const useFolder = confirm(
    'Replace surface geometry\n\nOK = pick the folder containing the 391Z .iam\nCancel = pick the .iam file directly (recommended)'
  );
  let result;
  if (useFolder) {
    const folderPath = await api.pickFolder();
    if (!folderPath) return;
    result = await runIncrementalScan([folderPath]);
  } else {
    const iamPath = await api.pickSurfaceIam();
    if (!iamPath) return;
    result = await runIncrementalScan([], [iamPath]);
  }

  try {
    if (!result?.surfaces?.length) {
      alert(formatIncrementalScanFailure(result));
      return;
    }
    if (result.surfaces.length !== 1) {
      alert(`Expected exactly one surface in the folder; found ${result.surfaces.length}.\n\nUse File → Add surface(s) from folder… to add multiple.`);
      return;
    }

    const newSurface = result.surfaces[0];
    const newKey = newSurface.surfaceNumber;

    if (newKey !== oldKey && activeSurfaceKeys().has(newKey)) {
      alert(`Surface "${newKey}" is already in this project.`);
      return;
    }

    if (newKey === oldKey) {
      const idx = state.surfaces.findIndex((s) => s.surfaceNumber === oldKey);
      if (idx >= 0) state.surfaces[idx] = newSurface;
      state.projectData = refreshSurfaceGeometryRecord(state.projectData, oldKey, newSurface);
    } else {
      state.surfaces = state.surfaces.filter((s) => s.surfaceNumber !== oldKey);
      state.surfaces.push(newSurface);
      state.surfaces.sort((a, b) =>
        a.surfaceNumber.localeCompare(b.surfaceNumber, undefined, { numeric: true })
      );
      state.projectData = replaceSurfaceWithScanned(state.projectData, oldKey, newSurface);
    }

    await persistGeometryCache();
    scheduleSave();
    await rebuildViewer();
    state.viewingRemovedKey = null;
    state.selectedSurfaceNumber = newKey;
    if (viewer) viewer.setSelection(newKey);
    openDetailPanel(newKey);
    renderSurfaceList();
    setAppStatus(`Replaced surface geometry — now tracking as ${getDisplayNumber(newKey, state.projectData.surfaces[newKey])}`);
    setTimeout(() => setAppStatus(''), 5000);
  } catch (err) {
    alert(`Replace failed:\n\n${err.message || err}`);
  }
}

async function addSurfacesFromFolders() {
  requireApi();
  if (!state.folderPath) {
    alert('Open the unit folder first.');
    return;
  }

  const folderPaths = await api.pickFolders();
  if (!folderPaths?.length) return;

  try {
    const result = await runIncrementalScan(folderPaths);
    if (!result?.surfaces?.length) {
      alert(formatIncrementalScanFailure(result));
      return;
    }

    const active = activeSurfaceKeys();
    const toAdd = [];
    const skipped = [];
    for (const surface of result.surfaces) {
      if (active.has(surface.surfaceNumber)) {
        skipped.push(surface.surfaceNumber);
      } else {
        toAdd.push(surface);
      }
    }

    if (!toAdd.length) {
      alert(`All ${result.surfaces.length} scanned surface(s) are already in the project.`);
      return;
    }

    if (skipped.length) {
      const proceed = confirm(
        `${skipped.length} surface(s) already in the project will be skipped:\n${skipped.slice(0, 8).join('\n')}${skipped.length > 8 ? '\n…' : ''}\n\nAdd ${toAdd.length} new surface(s)?`
      );
      if (!proceed) return;
    }

    const { projectData, added } = addScannedSurfacesToProject(
      state.projectData,
      toAdd,
      [...active]
    );
    state.projectData = projectData;
    state.surfaces = [...state.surfaces, ...toAdd];
    state.surfaces.sort((a, b) =>
      a.surfaceNumber.localeCompare(b.surfaceNumber, undefined, { numeric: true })
    );

    await persistGeometryCache();
    scheduleSave();
    await rebuildViewer();
    renderSurfaceList();
    setAppStatus(`Added ${added.length} surface${added.length === 1 ? '' : 's'} from folder scan`);
    setTimeout(() => setAppStatus(''), 5000);
  } catch (err) {
    alert(`Add surfaces failed:\n\n${err.message || err}`);
  }
}

async function retireCurrentSurface() {
  if (!state.selectedSurfaceNumber || state.viewingRemovedKey) {
    alert('Select an active surface to remove.');
    return;
  }

  const surfaceKey = state.selectedSurfaceNumber;
  const surface = state.surfaces.find((s) => s.surfaceNumber === surfaceKey);
  if (!surface) return;

  const display = getDisplayNumber(surfaceKey, state.projectData.surfaces[surfaceKey]);
  const ok = confirm(
    `Remove "${display}" from the main surface list?\n\nGeometry will be hidden. Status, checklist, and notes are kept under Removed.`
  );
  if (!ok) return;

  try {
    state.projectData = retireSurfaceFully(state.projectData, surfaceKey, surface);
    state.surfaces = state.surfaces.filter((s) => s.surfaceNumber !== surfaceKey);
    await persistGeometryCache();
    scheduleSave();
    await rebuildViewer();
    closeDetailPanel();
    renderSurfaceList();
    setAppStatus(`Removed ${display} — see Removed section`);
    setTimeout(() => setAppStatus(''), 5000);
  } catch (err) {
    alert(err.message || String(err));
  }
}

async function tryLoadFolderFromCache(folderPath) {
  const result = await api.loadProjectFolder(folderPath, null);
  if (!result?.surfaces?.length) return null;
  return result;
}

async function applyFolderLoadResult(folderPath, result, { quiet = false, rescan = false, fromCache = false } = {}) {
  state.folderPath = result.folderPath;
  state.projectFilePath = null;
  state.loadErrors = result.errors || [];
  state.scanSource = result.scanSource || 'none';
  state.projectData = migrateProjectData(result.projectData, result.folderPath);
  state.surfaces = filterExcludedGeometry(result.surfaces, state.projectData);
  state.projectData = mergeScanWithProject(state.surfaces, state.projectData);

  for (const surface of state.surfaces) {
    const fp = geometryFingerprint(surface);
    for (const num of Object.keys(state.projectData.retired || {})) {
      if (!state.projectData.retired[num].geometryFingerprint) {
        const snap = state.projectData.retired[num].snapshot;
        if (snap?.geometryFingerprint) {
          state.projectData.retired[num].geometryFingerprint = snap.geometryFingerprint;
        }
      }
    }
    const record = getSurfaceRecord(surface.surfaceNumber);
    record.geometryFingerprint = fp;
  }

  if (rescan) {
    scheduleSave();
    const added = state.surfaces.length;
    const sourceLabel =
      state.scanSource === 'iam'
        ? '391Z assemblies (via Inventor)'
        : state.scanSource === 'json'
          ? 'CONFIG_JSON files'
          : 'folder';
    if (!quiet) {
      setAppStatus(
        `Rescanned ${sourceLabel}: ${added} surface${added === 1 ? '' : 's'} (new files are added; missing ones are retired).`
      );
    }
  } else if (!quiet && state.surfaces.length > 0) {
    if (fromCache) {
      setAppStatus(
        `Loaded ${state.surfaces.length} surfaces from cache. Use File → Rescan to refresh from Inventor.`
      );
    } else {
      const sourceLabel = state.scanSource === 'iam' ? 'IAM via Inventor' : 'CONFIG_JSON';
      setAppStatus(`Loaded ${state.surfaces.length} surfaces from ${sourceLabel}.`);
    }
  }

  await api.rememberProject(folderPath);
  markProjectClean();
  applyLoadedFolder({ quiet });
}

async function loadFolder(folderPath, { quiet = false, rescan = false } = {}) {
  setScanUiBusy(true);
  let result = null;

  try {
    if (!rescan) {
      try {
        result = await tryLoadFolderFromCache(folderPath);
      } catch (err) {
        console.warn('Could not load cached geometry', err);
        result = null;
      }
    }

    if (!result || rescan) {
      setScanProgress(true, { phase: 'discovering', message: 'Searching folder for surfaces…' });
      result = await api.scanFolder(folderPath, (payload) => {
        setScanProgress(true, payload);
      });
    }

    await applyFolderLoadResult(folderPath, result, {
      quiet,
      rescan,
      fromCache: Boolean(!rescan && result?.fromCache),
    });
  } catch (err) {
    console.error(err);
    const message = err.message || String(err);
    setAppStatus(`Load failed: ${message}`, true);
    if (!quiet) alert(`Could not load folder:\n\n${message}`);
    throw err;
  } finally {
    setScanProgress(false);
    setScanUiBusy(false);
  }
}

function applyLoadedFolder({ quiet = false } = {}) {
  state.selectedSurfaceNumber = null;
  const folderLabel = state.folderPath || 'Saved project';
  els.folderLabel.textContent = folderLabel;
  els.folderLabel.title = state.folderPath || folderLabel;
  els.surfaceCount.textContent = String(state.surfaces.length);
  setFolderUiEnabled(state.surfaces.length > 0);

  renderLoadErrors(state.loadErrors);
  renderSurfaceList();
  renderLegend();
  renderBomPanel();
  syncOpacityControls();

  if (state.surfaces.length === 0) {
    els.viewerEmpty.hidden = true;
    if (viewer) viewer.dispose();
    viewer = null;
    if (!quiet) closeDetailPanel();
    return;
  }

  els.viewerEmpty.hidden = true;
  void rebuildViewer();
  closeDetailPanel();
}

function surfaceDisplayNumber(surface) {
  return getDisplayNumber(surface.surfaceNumber, state.projectData.surfaces[surface.surfaceNumber]);
}

function surfacesForViewer() {
  return state.surfaces.map((s) => ({
    ...s,
    displayNumber: surfaceDisplayNumber(s),
  }));
}

async function rebuildViewer() {
  const activeViewer = await getViewer();
  activeViewer.buildSurfaces(
    surfacesForViewer(),
    getSurfaceAppearanceForSurface,
    isSurfaceHidden,
    getSurfaceOpacity(),
    getViewerOptions(state.options)
  );
}

function setSurfaceHidden(surfaceNumber, hidden) {
  const record = getSurfaceRecord(surfaceNumber);
  record.hidden = hidden;
  record.updatedAt = new Date().toISOString();
  if (viewer) viewer.setSurfaceVisible(surfaceNumber, !hidden);
  renderSurfaceList();
  scheduleSave();
}

function toggleSurfaceHidden(surfaceNumber) {
  setSurfaceHidden(surfaceNumber, !isSurfaceHidden(surfaceNumber));
}

function showAllSurfaces() {
  let changed = false;
  for (const surface of state.surfaces) {
    const record = getSurfaceRecord(surface.surfaceNumber);
    if (record.hidden) {
      record.hidden = false;
      record.updatedAt = new Date().toISOString();
      changed = true;
    }
  }
  if (!changed) return;
  if (viewer) viewer.setAllSurfacesVisible(true);
  renderSurfaceList();
  scheduleSave();
}

function renderLoadErrors(errors) {
  if (!errors || errors.length === 0) {
    els.loadErrors.hidden = true;
    els.loadErrors.textContent = '';
    return;
  }
  els.loadErrors.hidden = false;
  const preview = errors.slice(0, 8).map((e) => `${e.filePath}: ${e.error}`);
  const suffix = errors.length > 8 ? `\n…and ${errors.length - 8} more` : '';
  els.loadErrors.textContent = `${preview.join('\n')}${suffix}`;
}

function renderSurfaceList() {
  els.surfaceList.innerHTML = '';
  ensureListDisplayOptions();
  const listDisplay = getListDisplay(state.options);
  const sorted = sortSurfacesForList(state.surfaces, listDisplay.sortMode);

  for (const surface of sorted) {
    const li = document.createElement('li');
    li.className = 'surface-list-item';

    const visBtn = document.createElement('button');
    visBtn.type = 'button';
    visBtn.className = 'visibility-toggle';
    const hidden = isSurfaceHidden(surface.surfaceNumber);
    visBtn.textContent = hidden ? '○' : '●';
    visBtn.title = hidden ? 'Show surface in 3D' : 'Hide surface in 3D';
    visBtn.setAttribute('aria-label', visBtn.title);
    visBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      toggleSurfaceHidden(surface.surfaceNumber);
    });

    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'surface-select-btn';
    btn.dataset.surfaceNumber = surface.surfaceNumber;
    const swatch = document.createElement('span');
    swatch.className = 'swatch';
    applySwatchStyle(swatch, getSurfaceAppearanceForSurface(surface.surfaceNumber));

    const displayNum = surfaceDisplayNumber(surface);
    const shortLabel = shortSurfaceLabel(displayNum);
    const parts = [swatch];

    if (listDisplay.nameMode !== 'short') {
      const name = document.createElement('span');
      name.className = 'name';
      name.textContent = displayNum;
      parts.push(name);
    }

    if (listDisplay.nameMode !== 'long') {
      const shortTag = document.createElement('span');
      shortTag.className = 'short-tag';
      if (listDisplay.nameMode === 'short') shortTag.classList.add('short-primary');
      shortTag.textContent = shortLabel;
      parts.push(shortTag);
    }

    if (listDisplay.showTypeTag) {
      const typeLabel = formatTypeTag(surface);
      if (typeLabel) {
        const typeTag = document.createElement('span');
        typeTag.className = 'meta-tag type-tag';
        typeTag.title = 'Configuration type';
        typeTag.textContent = typeLabel;
        parts.push(typeTag);
      }
    }

    if (listDisplay.showSkidTag) {
      const skidLabel = formatSkidTag(surface);
      if (skidLabel) {
        const skidTag = document.createElement('span');
        skidTag.className = 'meta-tag skid-tag';
        skidTag.title = 'Skid';
        skidTag.textContent = skidLabel;
        parts.push(skidTag);
      }
    }

    if (listDisplay.showSideTag) {
      const sideLabel = formatSideTag(surface);
      if (sideLabel) {
        const sideTag = document.createElement('span');
        sideTag.className = 'meta-tag side-tag';
        sideTag.title = 'Unit side';
        sideTag.textContent = sideLabel;
        parts.push(sideTag);
      }
    }

    if (displayNum !== surface.surfaceNumber) {
      const fileTag = document.createElement('span');
      fileTag.className = 'file-tag';
      fileTag.title = `JSON file: ${surface.surfaceNumber}`;
      fileTag.textContent = '↳ file';
      parts.push(fileTag);
    }

    btn.append(...parts);
    btn.addEventListener('click', () => selectSurface(surface.surfaceNumber));
    if (hidden) btn.classList.add('hidden-surface');
    if (surface.surfaceNumber === state.selectedSurfaceNumber) btn.classList.add('active');

    li.append(visBtn, btn);
    els.surfaceList.appendChild(li);
  }

  renderRemovedList();
}

function renderRemovedList() {
  if (!els.removedSurfaceList || !els.removedSurfacesSection) return;
  const removed = listRemovedSurfaces(state.projectData);
  if (els.removedCount) els.removedCount.textContent = String(removed.length);
  els.removedSurfacesSection.hidden = removed.length === 0;
  els.removedSurfaceList.innerHTML = '';

  for (const item of removed) {
    const li = document.createElement('li');
    li.className = 'surface-list-item';

    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'surface-select-btn';
    btn.dataset.removedKey = item.displayKey;

    const name = document.createElement('span');
    name.className = 'name';
    name.textContent = item.displayKey;

    const shortTag = document.createElement('span');
    shortTag.className = 'short-tag';
    shortTag.textContent = shortSurfaceLabel(item.displayKey);

    btn.append(name, shortTag);
    if (state.viewingRemovedKey === item.displayKey) btn.classList.add('active');
    btn.addEventListener('click', () => openRemovedDetailPanel(item.displayKey));

    li.appendChild(btn);
    els.removedSurfaceList.appendChild(li);
  }
}

function setDetailPanelReadonly(readonly) {
  if (els.detailPanel) els.detailPanel.classList.toggle('detail-readonly', readonly);
  if (els.stateSelect) els.stateSelect.disabled = readonly;
  if (els.notesInput) els.notesInput.readOnly = readonly;
  if (els.applyRenumberBtn) els.applyRenumberBtn.disabled = readonly;
  if (els.replaceFromFolderBtn) els.replaceFromFolderBtn.disabled = readonly;
  if (els.retireSurfaceBtn) els.retireSurfaceBtn.disabled = readonly;
  if (els.linkRenumberBtn) els.linkRenumberBtn.disabled = readonly;
  if (els.linkReplaceBtn) els.linkReplaceBtn.disabled = readonly;
  if (els.linkPreviousSelect) els.linkPreviousSelect.disabled = readonly;
  if (els.renumberInput) els.renumberInput.readOnly = readonly;
}

function openRemovedDetailPanel(displayKey) {
  const retired = state.projectData.retired?.[displayKey];
  if (!retired?.snapshot) return;

  state.viewingRemovedKey = displayKey;
  state.selectedSurfaceNumber = null;
  state.selectedBomEntryKey = null;
  state.detailMode = 'removed';

  const record = retired.snapshot;
  setDetailPanelMode('surface');
  setDetailPanelReadonly(true);
  els.detailPanel.hidden = false;
  els.detailTitle.textContent = displayKey;

  els.detailMeta.innerHTML = [
    '<div class="detail-readonly-flag">Removed surface — read-only snapshot</div>',
    `<div class="short-label">Label: ${escapeHtml(shortSurfaceLabel(displayKey))}</div>`,
    retired.fileKey && retired.fileKey !== displayKey
      ? `<div class="file-key">IAM: ${escapeHtml(retired.fileKey)}</div>`
      : '',
    retired.retiredAt ? `<div>Removed: ${escapeHtml(new Date(retired.retiredAt).toLocaleString())}</div>` : '',
    `<div>Type: ${escapeHtml(retired.transferType || 'removed')}</div>`,
  ].join('');

  if (els.renumberInput) els.renumberInput.value = displayKey;
  if (els.fileKeyNote) els.fileKeyNote.textContent = 'This surface was removed from the main list.';

  els.stateSelect.innerHTML = state.options.states
    .map((s) => `<option value="${escapeAttr(s.id)}">${escapeHtml(s.name)}</option>`)
    .join('');
  els.stateSelect.value = record.stateId || state.options.states[0]?.id;

  renderDetailChecklist(record, { readonly: true });
  els.notesInput.value = record.notes || '';

  if (els.historyList) {
    els.historyList.innerHTML = '';
    const items = record.previousNumbers || [];
    if (!items.length) {
      const li = document.createElement('li');
      li.className = 'history-empty';
      li.textContent = 'No linked previous numbers';
      els.historyList.appendChild(li);
    } else {
      for (const prev of items) {
        const li = document.createElement('li');
        li.innerHTML = `<span class="history-num">${escapeHtml(prev)}</span>`;
        els.historyList.appendChild(li);
      }
    }
  }

  if (viewer) viewer.setSelection(null);
  renderSurfaceList();
}

function renderLegend() {
  if (!state.options?.states?.length) {
    els.legend.hidden = true;
    return;
  }
  els.legend.hidden = false;
  const vo = getViewerOptions(state.options);
  const fpsHint = vo.fpsControlsEnabled
    ? `WASD move · ${fpsKeyLabel(vo.fpsKeys.sprint)} sprint · ${fpsKeyLabel(vo.fpsKeys.ascend)} up · ${fpsKeyLabel(vo.fpsKeys.descend)} down`
    : 'FPS movement off';
  els.legend.innerHTML = [
    ...state.options.states.map((s) => {
      const sw = getSwatchBackground(s);
      const bg = sw.backgroundImage
        ? `background-color:${sw.backgroundColor};background-image:${sw.backgroundImage}`
        : `background:${sw.backgroundColor}`;
      return `<div class="legend-row"><span class="legend-swatch" style="${bg}"></span><span>${escapeHtml(s.name)}</span></div>`;
    }),
    `<div class="legend-hint">Double-click surface for info · Double right-click to hide · ${fpsHint}</div>`,
  ].join('');
}

function selectSurface(surfaceNumber) {
  state.selectedSurfaceNumber = surfaceNumber;
  state.selectedBomEntryKey = null;
  state.viewingRemovedKey = null;
  state.detailMode = 'surface';
  if (viewer) viewer.setSelection(surfaceNumber);
  renderSurfaceList();
  renderBomPanel();
  openDetailPanel(surfaceNumber);
}

function getActiveDisplayNumbers() {
  return new Set(state.surfaces.map((s) => surfaceDisplayNumber(s)));
}

function getRetiredNumbers() {
  const active = getActiveDisplayNumbers();
  const retired = state.projectData.retired || {};
  return Object.keys(retired)
    .filter((num) => !active.has(num) && retired[num]?.transferType !== 'removed')
    .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
}

function renderHistorySection(surfaceNumber) {
  const record = getSurfaceRecord(surfaceNumber);
  els.historyList.innerHTML = '';
  const items = record.previousNumbers || [];
  if (items.length === 0) {
    const li = document.createElement('li');
    li.className = 'history-empty';
    li.textContent = 'No linked previous numbers';
    els.historyList.appendChild(li);
  } else {
    for (const prev of items) {
      const li = document.createElement('li');
      const retired = state.projectData.retired?.[prev];
      const type = retired?.transferType || 'linked';
      li.innerHTML = `<span class="history-num">${escapeHtml(prev)}</span><span class="history-type">${escapeHtml(type)}</span>`;
      els.historyList.appendChild(li);
    }
  }

  const options = ['<option value="">Link retired surface…</option>'];
  for (const num of getRetiredNumbers()) {
    if (!items.includes(num)) {
      options.push(`<option value="${escapeAttr(num)}">${escapeHtml(num)}</option>`);
    }
  }
  els.linkPreviousSelect.innerHTML = options.join('');
  const hasRetired = getRetiredNumbers().some((num) => !(record.previousNumbers || []).includes(num));
  els.linkRenumberBtn.disabled = !hasRetired;
  els.linkReplaceBtn.disabled = !hasRetired;
}

function openDetailPanel(surfaceNumber) {
  const surface = state.surfaces.find((s) => s.surfaceNumber === surfaceNumber);
  if (!surface) return;
  const record = getSurfaceRecord(surfaceNumber);

  state.viewingRemovedKey = null;
  setDetailPanelMode('surface');
  setDetailPanelReadonly(false);
  els.detailPanel.hidden = false;
  const displayNum = surfaceDisplayNumber(surface);
  els.detailTitle.textContent = displayNum;
  els.detailMeta.innerHTML = [
    `<div class="short-label">Label: ${escapeHtml(shortSurfaceLabel(displayNum))}</div>`,
    displayNum !== surface.surfaceNumber
      ? `<div class="file-key">JSON file: ${escapeHtml(surface.surfaceNumber)}</div>`
      : '',
    surface.configurationKind ? `<div>Config: ${escapeHtml(formatTypeTag(surface))}</div>` : '',
    surface.skidId != null ? `<div>Skid: ${escapeHtml(formatSkidDisplay(surface))}</div>` : '',
    surface.partNumber ? `<div>Part: ${escapeHtml(surface.partNumber)}</div>` : '',
    surface.surfaceType ? `<div>Type: ${escapeHtml(surface.surfaceType)}</div>` : '',
    surface.surfaceUnitSide ? `<div>Side: ${escapeHtml(surface.surfaceUnitSide)}</div>` : '',
    surface.relativePath ? `<div>Path: ${escapeHtml(surface.relativePath)}</div>` : '',
    record.hidden ? '<div class="meta-flag">Hidden in 3D view</div>' : '',
  ].join('');

  els.renumberInput.value = displayNum;
  if (els.fileKeyNote) {
    els.fileKeyNote.textContent =
      displayNum !== surface.surfaceNumber
        ? `Tracking number differs from JSON filename (${surface.surfaceNumber}).`
        : 'Tracking number matches JSON filename.';
  }

  els.stateSelect.innerHTML = state.options.states
    .map((s) => `<option value="${escapeAttr(s.id)}">${escapeHtml(s.name)}</option>`)
    .join('');
  els.stateSelect.value = record.stateId || state.options.states[0]?.id;

  renderDetailChecklist(record);

  els.notesInput.value = record.notes || '';
  renderHistorySection(surfaceNumber);
}

function closeDetailPanel() {
  state.selectedSurfaceNumber = null;
  state.selectedBomEntryKey = null;
  state.viewingRemovedKey = null;
  state.detailMode = null;
  setDetailPanelReadonly(false);
  els.detailPanel.hidden = true;
  if (viewer) viewer.setSelection(null);
  renderSurfaceList();
  renderBomPanel();
}

async function linkSelectedPrevious(transferType) {
  if (!state.selectedSurfaceNumber) return;
  const previousNumber = els.linkPreviousSelect.value;
  if (!previousNumber) {
    alert('Choose a retired surface number to link.');
    return;
  }
  state.projectData = linkPreviousSurface(state.projectData, state.selectedSurfaceNumber, previousNumber, {
    transferType,
  });
  const surface = state.surfaces.find((s) => s.surfaceNumber === state.selectedSurfaceNumber);
  if (surface && transferType === 'renumber') {
    const fp = geometryFingerprint(surface);
    if (state.projectData.retired[previousNumber]) {
      state.projectData.retired[previousNumber].geometryFingerprint = fp;
    }
  }
  scheduleSave();
  await rebuildViewer();
  if (viewer && state.selectedSurfaceNumber) viewer.setSelection(state.selectedSurfaceNumber);
  openDetailPanel(state.selectedSurfaceNumber);
  renderSurfaceList();
}

async function applyManualRenumber() {
  if (!state.selectedSurfaceNumber) return;
  const fileKey = state.selectedSurfaceNumber;
  const newNumber = els.renumberInput.value;
  try {
    state.projectData = renumberSurfaceInPlace(state.projectData, fileKey, newNumber);
  } catch (err) {
    alert(err.message || String(err));
    return;
  }
  scheduleSave();
  await rebuildViewer();
  if (viewer) viewer.setSelection(fileKey);
  openDetailPanel(fileKey);
  renderSurfaceList();
}

function onStateChange() {
  if (state.detailMode !== 'surface' || !state.selectedSurfaceNumber) return;
  const record = getSurfaceRecord(state.selectedSurfaceNumber);
  record.stateId = els.stateSelect.value;
  record.updatedAt = new Date().toISOString();
  const appearance = getSurfaceAppearanceForSurface(state.selectedSurfaceNumber);
  if (viewer) viewer.setSurfaceAppearance(state.selectedSurfaceNumber, appearance);
  renderSurfaceList();
  scheduleSave();
}

function onChecklistChange(event) {
  const itemId = event.target.dataset.itemId;
  if (state.detailMode === 'bom' && state.selectedBomEntryKey) {
    const record = getBomEntryRecord(state.selectedBomEntryKey);
    record.checklist = record.checklist || {};
    record.checklist[itemId] = event.target.checked;
    record.updatedAt = new Date().toISOString();
    scheduleSave();
    return;
  }
  if (!state.selectedSurfaceNumber) return;
  const record = getSurfaceRecord(state.selectedSurfaceNumber);
  record.checklist = record.checklist || {};
  record.checklist[itemId] = event.target.checked;
  record.updatedAt = new Date().toISOString();
  scheduleSave();
}

function onNotesInput() {
  if (state.detailMode === 'bom' && state.selectedBomEntryKey) {
    const record = getBomEntryRecord(state.selectedBomEntryKey);
    record.notes = els.notesInput.value;
    record.updatedAt = new Date().toISOString();
    scheduleSave();
    return;
  }
  if (!state.selectedSurfaceNumber) return;
  const record = getSurfaceRecord(state.selectedSurfaceNumber);
  record.notes = els.notesInput.value;
  record.updatedAt = new Date().toISOString();
  scheduleSave();
}

async function openRecentDialog() {
  requireApi();
  const recent = await api.loadRecentProjects();
  els.recentList.innerHTML = '';
  const items = recent.recent || [];
  if (!items.length) {
    const li = document.createElement('li');
    li.className = 'recent-empty';
    li.textContent = 'No recent projects yet.';
    els.recentList.appendChild(li);
  } else {
    for (const entry of items) {
      const li = document.createElement('li');
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'recent-item-btn';
      btn.innerHTML = `<span class="recent-label">${escapeHtml(entry.label)}</span><span class="recent-path">${escapeHtml(entry.folderPath)}</span>`;
      btn.addEventListener('click', async () => {
        els.recentDialog.close();
        try {
          await loadFolder(entry.folderPath);
        } catch (err) {
          alert(`Could not open project:\n\n${err.message || err}`);
        }
      });
      li.appendChild(btn);
      els.recentList.appendChild(li);
    }
  }
  els.recentDialog.showModal();
}

async function importProjectJson() {
  requireApi();
  if (!state.folderPath) return;
  const result = await api.pickImportFile();
  if (result.canceled) return;
  const overwrite = confirm(
    'Merge imported data into the current project?\n\nOK = merge (keep existing where not in import)\nCancel = skip import'
  );
  if (!overwrite) return;
  state.projectData = importExportPayload(state.projectData, result.data, { overwrite: false });
  scheduleSave();
  await rebuildViewer();
  renderSurfaceList();
  if (state.selectedSurfaceNumber) openDetailPanel(state.selectedSurfaceNumber);
  setAppStatus(`Imported ${result.filePath}`);
  setTimeout(() => setAppStatus(''), 3000);
}

function openOptions() {
  try {
    if (!state.options) state.options = defaultOptions();
    state.draftOptions = structuredClone(state.options);
    renderOptionsEditor();
    els.optionsDialog.showModal();
  } catch (err) {
    console.error(err);
    alert(`Could not open options:\n\n${err.message || err}`);
  }
}

async function saveOptionsFromEditor() {
  collectOptionsFromEditor();
  state.options = state.draftOptions;
  state.options.listDisplay = normalizeListDisplay(state.options.listDisplay);
  state.options.layout = normalizeLayout(state.options.layout);
  state.options.uiTheme = normalizeUiTheme(state.options.uiTheme);
  state.options.viewer = normalizeViewerOptions(state.options.viewer);
  if (api) await api.saveOptions(state.options);
  state.draftOptions = null;
  els.optionsDialog.close();
  applyOptionsToUi(state.options);
  renderLegend();
  syncOpacityControls();
  syncListDisplayControls();
  if (state.surfaces.length) {
    await rebuildViewer();
    if (state.selectedSurfaceNumber) {
      viewer.setSelection(state.selectedSurfaceNumber);
      openDetailPanel(state.selectedSurfaceNumber);
    }
  }
  renderSurfaceList();
}

async function resetOptionsDefaults() {
  state.draftOptions = defaultOptions();
  renderOptionsEditor();
}

function renderOptionsEditor() {
  els.statesEditor.innerHTML = '';
  for (const item of state.draftOptions.states) {
    els.statesEditor.appendChild(createStateRow(item));
  }
  els.checklistEditor.innerHTML = '';
  for (const item of state.draftOptions.checklistItems) {
    els.checklistEditor.appendChild(createChecklistRow(item));
  }
  const opacity = state.draftOptions.surfaceOpacity ?? 0.9;
  const pct = Math.round(opacity * 100);
  els.optionsOpacitySlider.value = String(pct);
  els.optionsOpacityValue.textContent = opacityToPercent(opacity);

  const viewerOpts = normalizeViewerOptions(state.draftOptions.viewer);
  els.optionsShowGrid.checked = viewerOpts.showGrid;
  els.optionsFpsControls.checked = viewerOpts.fpsControlsEnabled;
  renderMouseMapEditor(viewerOpts);
  renderStickerEditor(viewerOpts);
  renderThemeEditor(normalizeUiTheme(state.draftOptions.uiTheme));
}

function renderStickerEditor(viewerOpts) {
  if (!els.stickerEditor) return;
  els.stickerEditor.innerHTML = '';
  const stickers = viewerOpts.stickers || DEFAULT_VIEWER_OPTIONS.stickers;

  const heading = document.createElement('p');
  heading.className = 'hint sticker-editor-heading';
  heading.textContent = 'Surface number stickers on 3D faces.';
  els.stickerEditor.appendChild(heading);

  const fontRow = document.createElement('div');
  fontRow.className = 'theme-row';
  const fontLabel = document.createElement('label');
  fontLabel.textContent = 'Sticker font';
  const fontSelect = document.createElement('select');
  fontSelect.id = 'stickerFontFamily';
  for (const opt of FONT_FAMILY_OPTIONS) {
    const o = document.createElement('option');
    o.value = opt.value;
    o.textContent = opt.label;
    fontSelect.appendChild(o);
  }
  fontSelect.value = FONT_FAMILY_OPTIONS.some((o) => o.value === stickers.fontFamily)
    ? stickers.fontFamily
    : FONT_FAMILY_OPTIONS[0].value;
  fontRow.append(fontLabel, fontSelect);
  els.stickerEditor.appendChild(fontRow);

  for (const field of [
    { key: 'textColor', label: 'Sticker text' },
    { key: 'backgroundColor', label: 'Sticker background' },
    { key: 'borderColor', label: 'Sticker border' },
  ]) {
    const row = document.createElement('div');
    row.className = 'theme-row';
    const label = document.createElement('label');
    label.textContent = field.label;
    const input = document.createElement('input');
    input.type = 'color';
    input.dataset.stickerColor = field.key;
    input.value = stickers[field.key] || '#ffffff';
    row.append(label, input);
    els.stickerEditor.appendChild(row);
  }
}

function renderMouseMapEditor(viewerOpts) {
  els.mouseMapEditor.innerHTML = '';
  const mb = viewerOpts.mouseButtons;
  for (const row of [
    { key: 'rotate', label: 'Rotate' },
    { key: 'pan', label: 'Pan' },
    { key: 'zoom', label: 'Zoom' },
  ]) {
    const wrap = document.createElement('div');
    wrap.className = 'mouse-map-row';
    const label = document.createElement('label');
    label.textContent = row.label;
    const select = document.createElement('select');
    select.dataset.mouseKey = row.key;
    for (const btn of MOUSE_BUTTONS) {
      const opt = document.createElement('option');
      opt.value = String(btn.id);
      opt.textContent = btn.label;
      select.appendChild(opt);
    }
    select.value = String(mb[row.key]);
    wrap.append(label, select);
    els.mouseMapEditor.appendChild(wrap);
  }

  const fk = viewerOpts.fpsKeys;
  for (const row of [
    { key: 'ascend', label: 'FPS ascend key' },
    { key: 'descend', label: 'FPS descend key' },
    { key: 'sprint', label: 'FPS sprint key' },
  ]) {
    const wrap = document.createElement('div');
    wrap.className = 'mouse-map-row';
    const label = document.createElement('label');
    label.textContent = row.label;
    const select = document.createElement('select');
    select.dataset.fpsKey = row.key;
    for (const keyOpt of FPS_KEY_OPTIONS) {
      const opt = document.createElement('option');
      opt.value = keyOpt.id;
      opt.textContent = keyOpt.label;
      select.appendChild(opt);
    }
    select.value = fk[row.key] || FPS_KEY_OPTIONS[0].id;
    wrap.append(label, select);
    els.mouseMapEditor.appendChild(wrap);
  }
}

function renderThemeEditor(theme) {
  els.themeEditor.innerHTML = '';

  const fontRow = document.createElement('div');
  fontRow.className = 'theme-row';
  const fontLabel = document.createElement('label');
  fontLabel.textContent = 'Font';
  const fontSelect = document.createElement('select');
  fontSelect.id = 'themeFontFamily';
  for (const opt of FONT_FAMILY_OPTIONS) {
    const o = document.createElement('option');
    o.value = opt.value;
    o.textContent = opt.label;
    fontSelect.appendChild(o);
  }
  fontSelect.value = FONT_FAMILY_OPTIONS.some((o) => o.value === theme.fontFamily)
    ? theme.fontFamily
    : FONT_FAMILY_OPTIONS[0].value;
  fontRow.append(fontLabel, fontSelect);
  els.themeEditor.appendChild(fontRow);

  const sizeRow = document.createElement('div');
  sizeRow.className = 'theme-row';
  const sizeLabel = document.createElement('label');
  sizeLabel.textContent = 'Font size';
  const sizeInput = document.createElement('input');
  sizeInput.type = 'number';
  sizeInput.id = 'themeFontSize';
  sizeInput.min = '11';
  sizeInput.max = '22';
  sizeInput.value = String(theme.fontSizePx);
  sizeRow.append(sizeLabel, sizeInput);
  els.themeEditor.appendChild(sizeRow);

  const colorFields = [
    { key: 'text', label: 'Main text' },
    { key: 'textMuted', label: 'Muted text' },
    { key: 'panelBg', label: 'Panel background' },
    { key: 'headerBg', label: 'Header background' },
    { key: 'accent', label: 'Accent' },
    { key: 'listText', label: 'List text' },
  ];
  for (const field of colorFields) {
    const row = document.createElement('div');
    row.className = 'theme-row';
    const label = document.createElement('label');
    label.textContent = field.label;
    const input = document.createElement('input');
    input.type = 'color';
    input.dataset.themeColor = field.key;
    input.value = theme.colors[field.key] || '#ffffff';
    row.append(label, input);
    els.themeEditor.appendChild(row);
  }
}

function createStateRow(item) {
  const row = document.createElement('div');
  row.className = 'editor-row state-row';
  row.dataset.stateId = item.id;
  const nameInput = document.createElement('input');
  nameInput.type = 'text';
  nameInput.value = item.name;
  nameInput.placeholder = 'State name';

  const fillSelect = document.createElement('select');
  fillSelect.className = 'fill-select';
  for (const opt of FILL_TYPE_OPTIONS) {
    const o = document.createElement('option');
    o.value = opt.id;
    o.textContent = opt.label;
    fillSelect.appendChild(o);
  }
  fillSelect.value = item.fillType || FILL_SOLID;

  const colorInput = document.createElement('input');
  colorInput.type = 'color';
  colorInput.value = item.color;
  colorInput.className = 'state-color-input';
  colorInput.title = 'Solid fill color';

  const preview = document.createElement('span');
  preview.className = 'state-preview-swatch';

  const syncRow = () => {
    const fillType = fillSelect.value;
    colorInput.hidden = fillType !== FILL_SOLID;
    applySwatchStyle(preview, { color: colorInput.value, fillType });
  };
  fillSelect.addEventListener('change', syncRow);
  colorInput.addEventListener('input', syncRow);
  syncRow();

  const removeBtn = document.createElement('button');
  removeBtn.type = 'button';
  removeBtn.className = 'btn small';
  removeBtn.textContent = 'Remove';
  removeBtn.addEventListener('click', () => {
    if (state.draftOptions.states.length <= 1) return;
    state.draftOptions.states = state.draftOptions.states.filter((s) => s.id !== item.id);
    renderOptionsEditor();
  });
  row.append(nameInput, fillSelect, colorInput, preview, removeBtn);
  row._nameInput = nameInput;
  row._fillSelect = fillSelect;
  row._colorInput = colorInput;
  return row;
}

function createChecklistRow(item) {
  const row = document.createElement('div');
  row.className = 'editor-row checklist-row';
  row.dataset.itemId = item.id;
  const labelInput = document.createElement('input');
  labelInput.type = 'text';
  labelInput.value = item.label;
  labelInput.placeholder = 'Checklist item';
  const removeBtn = document.createElement('button');
  removeBtn.type = 'button';
  removeBtn.className = 'btn small';
  removeBtn.textContent = 'Remove';
  removeBtn.addEventListener('click', () => {
    state.draftOptions.checklistItems = state.draftOptions.checklistItems.filter((c) => c.id !== item.id);
    renderOptionsEditor();
  });
  row.append(labelInput, removeBtn);
  row._labelInput = labelInput;
  return row;
}

async function addStateRow() {
  const name = 'New state';
  const existingIds = state.draftOptions.states.map((s) => s.id);
  const id = api ? await api.makeStateId(name, existingIds) : `state-${Date.now()}`;
  state.draftOptions.states.push({ id, name, color: '#64748b', fillType: FILL_SOLID });
  renderOptionsEditor();
}

function addChecklistRow() {
  const id = `item-${Date.now()}`;
  state.draftOptions.checklistItems.push({ id, label: 'New checklist item' });
  renderOptionsEditor();
}

function collectOptionsFromEditor() {
  const newStates = [];
  for (const row of els.statesEditor.querySelectorAll('.state-row')) {
    newStates.push({
      id: row.dataset.stateId,
      name: row._nameInput.value.trim() || 'Unnamed',
      color: row._colorInput.value,
      fillType: row._fillSelect.value || FILL_SOLID,
    });
  }
  const newChecklist = [];
  for (const row of els.checklistEditor.querySelectorAll('.checklist-row')) {
    newChecklist.push({
      id: row.dataset.itemId,
      label: row._labelInput.value.trim() || 'Unnamed item',
    });
  }
  state.draftOptions.states = newStates;
  state.draftOptions.checklistItems = newChecklist;
  state.draftOptions.surfaceOpacity = Number(els.optionsOpacitySlider.value) / 100;

  const mouseButtons = { ...DEFAULT_VIEWER_OPTIONS.mouseButtons };
  for (const select of els.mouseMapEditor.querySelectorAll('select[data-mouse-key]')) {
    mouseButtons[select.dataset.mouseKey] = Number(select.value);
  }
  const fpsKeys = { ...DEFAULT_VIEWER_OPTIONS.fpsKeys };
  for (const select of els.mouseMapEditor.querySelectorAll('select[data-fps-key]')) {
    fpsKeys[select.dataset.fpsKey] = select.value;
  }
  const stickers = { ...DEFAULT_VIEWER_OPTIONS.stickers };
  const stickerFont = document.getElementById('stickerFontFamily');
  if (stickerFont) stickers.fontFamily = stickerFont.value;
  if (els.stickerEditor) {
    for (const input of els.stickerEditor.querySelectorAll('input[data-sticker-color]')) {
      stickers[input.dataset.stickerColor] = input.value;
    }
  }
  state.draftOptions.viewer = normalizeViewerOptions({
    showGrid: els.optionsShowGrid.checked,
    fpsControlsEnabled: els.optionsFpsControls.checked,
    mouseButtons,
    fpsKeys,
    stickers,
  });

  const fontSelect = document.getElementById('themeFontFamily');
  const fontSize = document.getElementById('themeFontSize');
  const colors = { ...DEFAULT_UI_THEME.colors };
  for (const input of els.themeEditor.querySelectorAll('input[data-theme-color]')) {
    colors[input.dataset.themeColor] = input.value;
  }
  state.draftOptions.uiTheme = normalizeUiTheme({
    fontFamily: fontSelect?.value || DEFAULT_UI_THEME.fontFamily,
    fontSizePx: fontSize ? Number(fontSize.value) : DEFAULT_UI_THEME.fontSizePx,
    colors,
  });
}

async function exportProject(format) {
  requireApi();
  const payload = buildExportPayload(state.folderPath, state.surfaces, state.projectData, state.options);
  await api.exportData(state.folderPath, format, {
    json: payload,
    markdown: exportToMarkdown(payload),
  });
}

function escapeHtml(text) {
  return String(text)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function escapeAttr(text) {
  return escapeHtml(text).replace(/'/g, '&#39;');
}

init().catch((err) => {
  console.error(err);
  bindEvents();
  setAppStatus(`Failed to start: ${err.message || err}`, true);
  alert(`Failed to start app:\n\n${err.message || err}`);
});
