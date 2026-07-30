export function migrateProjectData(raw, folderPath) {
  const base = raw && typeof raw === 'object' ? raw : {};
  const surfaces = base.surfaces && typeof base.surfaces === 'object' ? { ...base.surfaces } : {};
  const retired = base.retired && typeof base.retired === 'object' ? { ...base.retired } : {};

  for (const [key, record] of Object.entries(surfaces)) {
    surfaces[key] = normalizeSurfaceRecord(record);
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

export function normalizeSurfaceRecord(record) {
  const r = record && typeof record === 'object' ? { ...record } : {};
  return {
    stateId: r.stateId || null,
    checklist: r.checklist && typeof r.checklist === 'object' ? { ...r.checklist } : {},
    notes: r.notes || '',
    updatedAt: r.updatedAt || null,
    hidden: Boolean(r.hidden),
    displayNumber: r.displayNumber ? String(r.displayNumber).trim() : null,
    previousNumbers: Array.isArray(r.previousNumbers) ? [...r.previousNumbers] : [],
    geometryFingerprint: r.geometryFingerprint || null,
  };
}

export function getDisplayNumber(fileKey, record) {
  const display = record?.displayNumber;
  return display || fileKey;
}

export function geometryFingerprint(surface) {
  if (!surface?.boxes?.length) return '';
  return surface.boxes
    .map(
      (b) =>
        `${b.x.toFixed(3)},${b.y.toFixed(3)},${b.z.toFixed(3)},${b.xLength.toFixed(3)},${b.yLength.toFixed(3)},${b.zLength.toFixed(3)}`
    )
    .sort()
    .join('|');
}

export function shortSurfaceLabel(surfaceNumber) {
  const s = String(surfaceNumber || '');
  const dash = s.lastIndexOf('-');
  const suffix = dash >= 0 ? s.slice(dash + 1) : s;
  if (suffix.length <= 4) return suffix.padStart(4, '0');
  return suffix.slice(-4);
}

export function mergeScanWithProject(surfaces, projectData) {
  const currentNumbers = new Set(surfaces.map((s) => s.surfaceNumber));
  const retired = { ...(projectData.retired || {}) };
  const surfaceRecords = { ...(projectData.surfaces || {}) };

  for (const num of Object.keys(surfaceRecords)) {
    if (!currentNumbers.has(num) && !retired[num]) {
      const snap = surfaceRecords[num];
      retired[num] = {
        retiredAt: new Date().toISOString(),
        supersededBy: null,
        transferType: 'missing',
        geometryFingerprint: snap?.geometryFingerprint || null,
        snapshot: snap,
      };
      delete surfaceRecords[num];
    }
  }

  for (const surface of surfaces) {
    if (!surfaceRecords[surface.surfaceNumber]) {
      surfaceRecords[surface.surfaceNumber] = normalizeSurfaceRecord(null);
    }
  }

  return { ...projectData, surfaces: surfaceRecords, retired };
}

export function findRenumberCandidates(surface, projectData) {
  const fp = geometryFingerprint(surface);
  if (!fp) return [];
  const retired = projectData.retired || {};
  return Object.entries(retired)
    .filter(([, entry]) => entry.geometryFingerprint === fp)
    .map(([num]) => num);
}

export function linkPreviousSurface(projectData, currentNumber, previousNumber, { transferType = 'renumber' } = {}) {
  const record = projectData.surfaces[currentNumber];
  const retired = projectData.retired || {};
  if (!record) return projectData;

  const prevRecord =
    projectData.surfaces[previousNumber] ||
    retired[previousNumber]?.snapshot ||
    null;

  if (transferType === 'renumber' && prevRecord) {
    record.stateId = prevRecord.stateId || record.stateId;
    record.checklist = { ...(prevRecord.checklist || {}) };
    record.notes = prevRecord.notes || record.notes;
  }

  if (!record.previousNumbers.includes(previousNumber)) {
    record.previousNumbers = [...record.previousNumbers, previousNumber];
  }

  retired[previousNumber] = {
    ...(retired[previousNumber] || {}),
    retiredAt: new Date().toISOString(),
    supersededBy: currentNumber,
    transferType,
    geometryFingerprint: retired[previousNumber]?.geometryFingerprint || null,
    snapshot: prevRecord || retired[previousNumber]?.snapshot || null,
  };

  if (projectData.surfaces[previousNumber]) {
    delete projectData.surfaces[previousNumber];
  }

  record.updatedAt = new Date().toISOString();
  return { ...projectData, surfaces: { ...projectData.surfaces, [currentNumber]: record }, retired };
}

export function importExportPayload(projectData, payload, { overwrite = false } = {}) {
  const next = migrateProjectData(projectData, projectData.sourceFolder);
  if (!payload?.surfaces) return next;

  for (const row of payload.surfaces) {
    const num = row.surfaceNumber;
    if (!num) continue;
    const existing = next.surfaces[num] || normalizeSurfaceRecord(null);
    const imported = {
      stateId: row.state?.id || existing.stateId,
      checklist: {},
      notes: row.notes ?? existing.notes,
      updatedAt: row.updatedAt || new Date().toISOString(),
      hidden: existing.hidden,
      displayNumber: row.displayNumber ?? existing.displayNumber ?? null,
      previousNumbers: row.previousNumbers || existing.previousNumbers,
    };
    if (Array.isArray(row.checklist)) {
      for (const item of row.checklist) {
        if (item.id) imported.checklist[item.id] = Boolean(item.checked);
      }
    }
    next.surfaces[num] = overwrite ? imported : { ...existing, ...imported, checklist: { ...existing.checklist, ...imported.checklist } };
  }

  if (payload.retired) {
    next.retired = { ...next.retired, ...payload.retired };
  }

  return next;
}

export function renumberSurfaceInPlace(projectData, fileKey, newNumber) {
  const trimmed = String(newNumber || '').trim();
  if (!trimmed) throw new Error('Enter a surface number');

  const surfaces = { ...(projectData.surfaces || {}) };
  const record = surfaces[fileKey];
  if (!record) throw new Error('Surface not found');

  const oldDisplay = getDisplayNumber(fileKey, record);
  if (oldDisplay === trimmed) return projectData;

  for (const [key, rec] of Object.entries(surfaces)) {
    if (key === fileKey) continue;
    if (getDisplayNumber(key, rec) === trimmed) {
      throw new Error(`Surface number "${trimmed}" is already in use`);
    }
  }

  const retired = { ...(projectData.retired || {}) };
  retired[oldDisplay] = {
    retiredAt: new Date().toISOString(),
    supersededBy: trimmed,
    transferType: 'renumber',
    fileKey,
    geometryFingerprint: record.geometryFingerprint || null,
    snapshot: normalizeSurfaceRecord({ ...record, displayNumber: oldDisplay }),
  };

  const previousNumbers = [...(record.previousNumbers || [])];
  if (!previousNumbers.includes(oldDisplay)) previousNumbers.push(oldDisplay);

  surfaces[fileKey] = {
    ...record,
    displayNumber: trimmed,
    previousNumbers,
    updatedAt: new Date().toISOString(),
  };

  return { ...projectData, surfaces, retired };
}
