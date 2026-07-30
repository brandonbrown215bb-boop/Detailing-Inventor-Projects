import {
  buildShellFolderPlan,
  summarizeMisplacedPanels,
  isCustomSqAssembly,
  normalizeBomRow,
  is391Part,
  buildEntryKey,
  BOM_KEEP_FIELDS,
  parseSkidSegmentOrder,
  normalizeSegmentCode,
} from './bom-folder-maker.js';
import {
  applyBomListDisplay,
  getBomFilterOptions,
  BOM_SORT_OPTIONS,
  normalizeBomListDisplay,
} from './bom-list-display.js';

export function bomEntryKey(entry) {
  return entry?.entryKey || buildEntryKey(
    entry?.partNumber,
    entry?.skid,
    entry?.segment,
    entry?.description,
    entry?.extDescription
  );
}

export function bomForPersistence(bom) {
  if (!bom) return null;
  return {
    sourceFile: bom.sourceFile || null,
    importedAt: bom.importedAt || null,
    unit: bom.unit || null,
    rows: Array.isArray(bom.rows) ? bom.rows : [],
    manualRows: Array.isArray(bom.manualRows) ? bom.manualRows : [],
    removedKeys: Array.isArray(bom.removedKeys) ? bom.removedKeys : [],
    shellRoot: bom.shellRoot || null,
    foldersCreatedAt: bom.foldersCreatedAt || null,
    entryRecords:
      bom.entryRecords && typeof bom.entryRecords === 'object'
        ? { ...bom.entryRecords }
        : {},
  };
}

export function normalizeBomEntryRecord(record) {
  const r = record && typeof record === 'object' ? record : {};
  return {
    checklist: r.checklist && typeof r.checklist === 'object' ? { ...r.checklist } : {},
    notes: r.notes || '',
    updatedAt: r.updatedAt || null,
  };
}

function normalizeEntryRecords(raw) {
  const base = raw && typeof raw === 'object' ? raw : {};
  const out = {};
  for (const [key, record] of Object.entries(base)) {
    out[key] = normalizeBomEntryRecord(record);
  }
  return out;
}

export function defaultBomState() {
  return {
    sourceFile: null,
    importedAt: null,
    unit: null,
    rows: [],
    manualRows: [],
    removedKeys: [],
    shellRoot: null,
    plan: null,
    foldersCreatedAt: null,
    entryRecords: {},
  };
}

function allSourceRows(bom) {
  const imported = Array.isArray(bom?.rows) ? bom.rows : [];
  const manual = Array.isArray(bom?.manualRows) ? bom.manualRows : [];
  return [...imported, ...manual];
}

function manualRowKeys(bom) {
  const keys = new Set();
  for (const row of bom?.manualRows || []) {
    keys.add(buildEntryKey(
      row['Part Number'],
      row.Skid,
      row.Segment,
      row.Description,
      row['Ext. Description']
    ));
  }
  return keys;
}

export function buildBomPlan(bom) {
  const rows = allSourceRows(bom);
  if (!rows.length) return null;

  const plan = buildShellFolderPlan(rows, { shellRoot: bom?.shellRoot || null });
  const removed = new Set(bom?.removedKeys || []);
  const manualKeys = manualRowKeys(bom);

  plan.entries = plan.entries
    .filter((entry) => !removed.has(bomEntryKey(entry)))
    .map((entry) => ({
      ...entry,
      isManual: manualKeys.has(bomEntryKey(entry)),
    }));

  plan.stats.folderCount = plan.entries.length;
  plan.stats.manualCount = plan.entries.filter((e) => e.isManual).length;
  plan.stats.removedCount = removed.size;

  return plan;
}

export function normalizeBomState(raw) {
  const base = raw && typeof raw === 'object' ? raw : {};
  const bom = {
    sourceFile: base.sourceFile || null,
    importedAt: base.importedAt || null,
    unit: base.unit || null,
    rows: Array.isArray(base.rows) ? base.rows : [],
    manualRows: Array.isArray(base.manualRows) ? base.manualRows : [],
    removedKeys: Array.isArray(base.removedKeys) ? base.removedKeys : [],
    shellRoot: base.shellRoot || null,
    foldersCreatedAt: base.foldersCreatedAt || null,
    entryRecords: normalizeEntryRecords(base.entryRecords),
  };
  bom.plan = allSourceRows(bom).length ? buildBomPlan(bom) : null;
  return bom;
}

export function bomStateFromImport(filePath, rows, prior = null) {
  const unit = rows.find((r) => r.Unit && !String(r['Part Number']).startsWith('391-'))?.Unit
    || rows.find((r) => r.Unit)?.Unit
    || prior?.unit
    || null;
  return normalizeBomState({
    sourceFile: filePath,
    importedAt: new Date().toISOString(),
    unit,
    rows,
    manualRows: prior?.manualRows || [],
    removedKeys: [],
    shellRoot: prior?.shellRoot || null,
    foldersCreatedAt: prior?.foldersCreatedAt || null,
    entryRecords: prior?.entryRecords || {},
  });
}

export function attachShellRoot(bom, shellRoot) {
  return normalizeBomState({ ...bom, shellRoot });
}

export function createManualBomRow(fields, unit) {
  return {
    ...normalizeBomRow({
      'Part Number': fields.partNumber,
      Quantity: fields.quantity || '1',
      Unit: fields.unit || unit || '',
      Skid: fields.skid,
      Segment: fields.segment,
      Description: fields.description,
      'Ext. Description': fields.extDescription || '',
    }),
    _manual: true,
  };
}

export function addManualBomEntry(bom, fields) {
  const row = createManualBomRow(fields, bom?.unit);
  if (!is391Part(row['Part Number'])) {
    throw new Error('Part number must start with 391-');
  }
  if (!row.Skid || !row.Segment || !row.Description) {
    throw new Error('Skid, segment, and description are required.');
  }

  const next = normalizeBomState({
    ...bom,
    manualRows: [...(bom?.manualRows || []), row],
  });

  const key = buildEntryKey(row['Part Number'], row.Skid, row.Segment, row.Description, row['Ext. Description']);
  const created = next.plan?.entries?.find((entry) => bomEntryKey(entry) === key);
  if (!created) {
    throw new Error('Entry could not be placed — check skid bracket matches segment (e.g. FR in [FR-MB]).');
  }

  return next;
}

export function removeBomEntry(bom, entry) {
  const key = bomEntryKey(entry);
  if (entry?.isManual) {
    return normalizeBomState({
      ...bom,
      manualRows: (bom?.manualRows || []).filter((row) =>
        buildEntryKey(row['Part Number'], row.Skid, row.Segment, row.Description, row['Ext. Description']) !== key
      ),
    });
  }

  const removedKeys = [...(bom?.removedKeys || [])];
  if (!removedKeys.includes(key)) removedKeys.push(key);

  return normalizeBomState({
    ...bom,
    removedKeys,
  });
}

export function collectBomFieldOptions(bom) {
  const skids = new Set();
  const segments = new Set();
  for (const row of allSourceRows(bom)) {
    if (row.Skid) skids.add(row.Skid);
    if (row.Segment && row.Segment !== '<--') segments.add(row.Segment);
  }
  return {
    skids: [...skids].sort((a, b) => a.localeCompare(b, undefined, { numeric: true })),
    segments: [...segments].sort((a, b) => a.localeCompare(b, undefined, { numeric: true })),
  };
}

/** Segment choices for the add-entry dialog — only segments valid for the selected skid. */
export function getSegmentOptionsForSkid(bom, skid) {
  const skidTrim = String(skid || '').trim();
  if (!skidTrim) return [];

  const byCode = new Map();
  for (const row of allSourceRows(bom)) {
    if (String(row.Skid).trim() !== skidTrim) continue;
    if (!row.Segment || row.Segment === '<--') continue;
    const code = normalizeSegmentCode(row.Segment.split(' - ')[0]?.trim() || row.Segment);
    byCode.set(code, row.Segment);
  }

  const order = parseSkidSegmentOrder(skidTrim);
  if (!order.length) {
    return [...byCode.values()].sort().map((value) => ({ value, label: value }));
  }

  return order.map((entry) => {
    const value = byCode.get(entry.normalized) || `${entry.code} - ${entry.code}`;
    const label = byCode.get(entry.normalized)
      ? `${entry.folderPrefix} — ${value}`
      : `${entry.folderPrefix} — ${entry.code}`;
    return { value, label };
  });
}

export function populateBomAddSkidSelect(selectEl, bom) {
  if (!selectEl) return;
  const current = selectEl.value;
  selectEl.innerHTML = '<option value="">Select skid…</option>';
  for (const skid of collectBomFieldOptions(bom).skids) {
    const option = document.createElement('option');
    option.value = skid;
    option.textContent = skid;
    selectEl.appendChild(option);
  }
  if (current && [...selectEl.options].some((opt) => opt.value === current)) {
    selectEl.value = current;
  }
}

export function populateBomAddSegmentSelect(selectEl, bom, skid) {
  if (!selectEl) return;
  const options = getSegmentOptionsForSkid(bom, skid);
  selectEl.innerHTML = '';
  if (!options.length) {
    selectEl.disabled = true;
    const placeholder = document.createElement('option');
    placeholder.value = '';
    placeholder.textContent = skid ? 'No segments for this skid' : 'Select skid first…';
    selectEl.appendChild(placeholder);
    return;
  }

  selectEl.disabled = false;
  const placeholder = document.createElement('option');
  placeholder.value = '';
  placeholder.textContent = 'Select segment…';
  selectEl.appendChild(placeholder);
  for (const opt of options) {
    const option = document.createElement('option');
    option.value = opt.value;
    option.textContent = opt.label;
    selectEl.appendChild(option);
  }
}

export function renderBomPage(container, bom, handlers, listDisplay = null, viewOptions = null) {
  if (!container) return;
  container.innerHTML = '';

  const display = normalizeBomListDisplay(listDisplay);
  const selectedEntryKey = viewOptions?.selectedEntryKey || null;
  const hasSource = allSourceRows(bom).length > 0;

  const toolbar = document.createElement('div');
  toolbar.className = 'bom-toolbar';

  const importBtn = document.createElement('button');
  importBtn.type = 'button';
  importBtn.className = 'btn small primary';
  importBtn.textContent = 'Import BOM…';
  importBtn.addEventListener('click', () => handlers.onImport?.());

  const addBtn = document.createElement('button');
  addBtn.type = 'button';
  addBtn.className = 'btn small';
  addBtn.textContent = 'Add entry…';
  addBtn.addEventListener('click', () => handlers.onAddEntry?.());

  const createBtn = document.createElement('button');
  createBtn.type = 'button';
  createBtn.className = 'btn small';
  createBtn.textContent = 'Create Shell folders…';
  createBtn.disabled = !bom?.plan?.entries?.length;
  createBtn.addEventListener('click', () => handlers.onCreateFolders?.());

  const rootBtn = document.createElement('button');
  rootBtn.type = 'button';
  rootBtn.className = 'btn small';
  rootBtn.textContent = bom?.shellRoot ? 'Relocate shell root…' : 'Set shell root…';
  rootBtn.disabled = !hasSource;
  rootBtn.addEventListener('click', () => handlers.onPickShellRoot?.());

  toolbar.append(importBtn, addBtn, createBtn, rootBtn);
  container.appendChild(toolbar);

  const meta = document.createElement('div');
  meta.className = 'bom-meta';
  if (bom?.sourceFile) {
    meta.innerHTML = `<div><span class="bom-meta-label">BOM:</span> ${escapeHtml(shortPath(bom.sourceFile))}</div>`;
  }
  if (bom?.unit) {
    meta.innerHTML += `<div><span class="bom-meta-label">Unit:</span> ${escapeHtml(bom.unit)}</div>`;
  }
  if (bom?.shellRoot) {
    meta.innerHTML += `<div><span class="bom-meta-label">Shell root:</span> ${escapeHtml(bom.shellRoot)}</div>`;
  } else if (hasSource) {
    meta.innerHTML += `<div class="bom-hint">Pick a shell root before creating folders or opening export targets.</div>`;
  }
  if (bom?.foldersCreatedAt) {
    meta.innerHTML += `<div class="bom-meta-muted">Folders created ${new Date(bom.foldersCreatedAt).toLocaleString()}</div>`;
  }
  if (!hasSource) {
    meta.innerHTML += '<div class="bom-hint">Import a BOM xlsx or add entries manually for 391- shell export folders.</div>';
  }
  container.appendChild(meta);

  const plan = bom?.plan;
  if (plan?.misplaced?.length) {
    const alert = document.createElement('div');
    alert.className = 'bom-alert';
    alert.textContent = summarizeMisplacedPanels(plan.misplaced);
    container.appendChild(alert);
  }

  if (plan?.stats) {
    const stats = document.createElement('div');
    stats.className = 'bom-stats';
    const manualPart = plan.stats.manualCount ? ` · ${plan.stats.manualCount} manual` : '';
    const removedPart = plan.stats.removedCount ? ` · ${plan.stats.removedCount} removed` : '';
    stats.textContent =
      `${plan.stats.folderCount} export folder${plan.stats.folderCount === 1 ? '' : 's'} · ` +
      `${plan.stats.excludedCount} excluded · ` +
      `${plan.stats.customSqCount} custom (SQ)${manualPart}${removedPart}`;
    container.appendChild(stats);
  }

  if (!plan?.entries?.length) {
    const empty = document.createElement('div');
    empty.className = 'bom-empty';
    empty.textContent = hasSource
      ? 'No 391- assemblies in the export list. Add an entry or adjust filters.'
      : 'No BOM loaded.';
    container.appendChild(empty);
    return;
  }

  const filteredEntries = applyBomListDisplay(plan.entries, display);
  const filterOptions = getBomFilterOptions(plan.entries);

  const controls = document.createElement('div');
  controls.className = 'bom-list-controls';

  const searchLabel = document.createElement('label');
  searchLabel.className = 'bom-list-control bom-list-control-wide';
  const searchCaption = document.createElement('span');
  searchCaption.className = 'bom-list-control-label';
  searchCaption.textContent = 'Search';
  const searchInput = document.createElement('input');
  searchInput.type = 'search';
  searchInput.className = 'bom-list-control-input';
  searchInput.placeholder = 'Part, description, folder…';
  searchInput.value = display.searchText;
  searchInput.addEventListener('input', () => {
    handlers.onListDisplayChange?.({ searchText: searchInput.value });
  });
  searchLabel.append(searchCaption, searchInput);
  controls.appendChild(searchLabel);

  const sortLabel = document.createElement('label');
  sortLabel.className = 'bom-list-control';
  const sortCaption = document.createElement('span');
  sortCaption.className = 'bom-list-control-label';
  sortCaption.textContent = 'Sort';
  const sortSelect = document.createElement('select');
  sortSelect.className = 'bom-list-control-input';
  for (const opt of BOM_SORT_OPTIONS) {
    const option = document.createElement('option');
    option.value = opt.id;
    option.textContent = opt.label;
    sortSelect.appendChild(option);
  }
  sortSelect.value = display.sortMode;
  sortSelect.addEventListener('change', () => {
    handlers.onListDisplayChange?.({ sortMode: sortSelect.value });
  });
  sortLabel.append(sortCaption, sortSelect);
  controls.appendChild(sortLabel);

  const skidLabel = document.createElement('label');
  skidLabel.className = 'bom-list-control';
  const skidCaption = document.createElement('span');
  skidCaption.className = 'bom-list-control-label';
  skidCaption.textContent = 'Skid';
  const skidSelect = document.createElement('select');
  skidSelect.className = 'bom-list-control-input';
  const skidAll = document.createElement('option');
  skidAll.value = '';
  skidAll.textContent = 'All skids';
  skidSelect.appendChild(skidAll);
  for (const [value, label] of filterOptions.skids) {
    const option = document.createElement('option');
    option.value = value;
    option.textContent = label;
    skidSelect.appendChild(option);
  }
  skidSelect.value = display.skidFilter;
  skidSelect.addEventListener('change', () => {
    handlers.onListDisplayChange?.({ skidFilter: skidSelect.value });
  });
  skidLabel.append(skidCaption, skidSelect);
  controls.appendChild(skidLabel);

  const segmentLabel = document.createElement('label');
  segmentLabel.className = 'bom-list-control';
  const segmentCaption = document.createElement('span');
  segmentCaption.className = 'bom-list-control-label';
  segmentCaption.textContent = 'Segment';
  const segmentSelect = document.createElement('select');
  segmentSelect.className = 'bom-list-control-input';
  const segmentAll = document.createElement('option');
  segmentAll.value = '';
  segmentAll.textContent = 'All segments';
  segmentSelect.appendChild(segmentAll);
  for (const [value, label] of filterOptions.segments) {
    const option = document.createElement('option');
    option.value = value;
    option.textContent = label;
    segmentSelect.appendChild(option);
  }
  segmentSelect.value = display.segmentFilter;
  segmentSelect.addEventListener('change', () => {
    handlers.onListDisplayChange?.({ segmentFilter: segmentSelect.value });
  });
  segmentLabel.append(segmentCaption, segmentSelect);
  controls.appendChild(segmentLabel);

  const sqLabel = document.createElement('label');
  sqLabel.className = 'bom-list-control bom-list-control-check';
  const sqCheck = document.createElement('input');
  sqCheck.type = 'checkbox';
  sqCheck.checked = display.customSqOnly;
  sqCheck.addEventListener('change', () => {
    handlers.onListDisplayChange?.({ customSqOnly: sqCheck.checked });
  });
  sqLabel.append(sqCheck, document.createTextNode(' SQ custom only'));
  controls.appendChild(sqLabel);

  container.appendChild(controls);

  const visibleStats = document.createElement('div');
  visibleStats.className = 'bom-stats';
  visibleStats.textContent =
    filteredEntries.length === plan.entries.length
      ? `Showing ${filteredEntries.length} assembl${filteredEntries.length === 1 ? 'y' : 'ies'}`
      : `Showing ${filteredEntries.length} of ${plan.entries.length} assemblies`;
  container.appendChild(visibleStats);

  if (!filteredEntries.length) {
    const empty = document.createElement('div');
    empty.className = 'bom-empty';
    empty.textContent = 'No assemblies match the current filters.';
    container.appendChild(empty);
    return;
  }

  const list = document.createElement('ul');
  list.className = 'bom-list';

  for (const entry of filteredEntries) {
    const li = document.createElement('li');
    li.className = 'bom-row';
    if (entry.isCustomSq) li.classList.add('bom-row-custom');
    if (entry.isManual) li.classList.add('bom-row-manual');
    if (selectedEntryKey && bomEntryKey(entry) === selectedEntryKey) {
      li.classList.add('bom-row-selected');
    }

    const main = document.createElement('button');
    main.type = 'button';
    main.className = 'bom-row-main';
    main.title = 'Open checklist and notes';

    const head = document.createElement('div');
    head.className = 'bom-row-head';
    head.innerHTML = `<span class="bom-part">${escapeHtml(entry.partNumber)}</span>`;
    if (entry.isManual) {
      const tag = document.createElement('span');
      tag.className = 'bom-tag-manual';
      tag.textContent = 'Manual';
      head.appendChild(tag);
    }
    if (entry.isCustomSq) {
      const tag = document.createElement('span');
      tag.className = 'bom-tag-sq';
      tag.textContent = 'SQ custom';
      head.appendChild(tag);
    }

    const desc = document.createElement('div');
    desc.className = 'bom-row-desc';
    desc.textContent = entry.extDescription
      ? `${entry.description} · ${entry.extDescription}`
      : entry.description;

    const loc = document.createElement('div');
    loc.className = 'bom-row-loc';
    loc.textContent = entry.relativePath;

    main.append(head, desc, loc);
    main.addEventListener('click', () => handlers.onSelectEntry?.(entry));

    const actions = document.createElement('div');
    actions.className = 'bom-row-actions';

    const openBtn = document.createElement('button');
    openBtn.type = 'button';
    openBtn.className = 'btn small';
    openBtn.textContent = 'Open folder';
    openBtn.disabled = !bom.shellRoot;
    openBtn.title = bom.shellRoot ? 'Open this assembly folder in File Explorer' : 'Set shell root first';
    openBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      handlers.onOpenFolder?.(entry);
    });

    const removeBtn = document.createElement('button');
    removeBtn.type = 'button';
    removeBtn.className = 'btn small bom-remove-btn';
    removeBtn.textContent = 'Remove';
    removeBtn.title = entry.isManual ? 'Delete this manual entry' : 'Remove from export list';
    removeBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      handlers.onRemoveEntry?.(entry);
    });

    actions.append(openBtn, removeBtn);
    li.append(main, actions);
    list.appendChild(li);
  }

  container.appendChild(list);
}

function shortPath(filePath) {
  const parts = String(filePath || '').split(/[\\/]/);
  if (parts.length <= 2) return filePath;
  return `…/${parts.slice(-2).join('/')}`;
}

function escapeHtml(text) {
  return String(text)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

export { isCustomSqAssembly, BOM_KEEP_FIELDS };
