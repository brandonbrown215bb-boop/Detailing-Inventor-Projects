export function migrateProjectData(raw, folderPath) {
  const base = raw && typeof raw === 'object' ? raw : {};
  const surfaces = base.surfaces && typeof base.surfaces === 'object' ? { ...base.surfaces } : {};
  const retired = base.retired && typeof base.retired === 'object' ? { ...base.retired } : {};

  for (const [key, record] of Object.entries(surfaces)) {
    surfaces[key] = normalizeSurfaceRecord(record);
  }

  const bom = base.bom && typeof base.bom === 'object' ? { ...base.bom } : null;
  const projectOptions =
    base.projectOptions && typeof base.projectOptions === 'object' ? { ...base.projectOptions } : null;

  return {
    version: 2,
    sourceFolder: folderPath || base.sourceFolder || null,
    updatedAt: base.updatedAt || null,
    surfaces,
    retired,
    bom,
    projectOptions,
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

/** File keys whose geometry should not load from cache (removed or replaced IAM only). */
export function getExcludedGeometryKeys(projectData) {
  const excluded = new Set();
  for (const entry of Object.values(projectData?.retired || {})) {
    const key = entry.fileKey;
    if (!key) continue;
    const type = entry.transferType;
    // Renumber keeps the same IAM file — geometry must stay visible after reload.
    if (type === 'removed' || type === 'replaced') {
      excluded.add(key);
    }
  }
  return excluded;
}

/** Remove a mistaken previous surface number from history (IAM geometry unchanged). */
export function removeHistoryNumber(projectData, fileKey, historyNumber) {
  const trimmed = String(historyNumber || '').trim();
  if (!trimmed) throw new Error('No history number specified');

  const surfaces = { ...(projectData.surfaces || {}) };
  const retired = { ...(projectData.retired || {}) };
  const record = surfaces[fileKey];
  if (!record) throw new Error('Surface not found');

  const prev = record.previousNumbers || [];
  if (!prev.includes(trimmed)) {
    throw new Error(`"${trimmed}" is not in this surface's history`);
  }

  const currentDisplay = getDisplayNumber(fileKey, record);
  const previousNumbers = prev.filter((n) => n !== trimmed);

  surfaces[fileKey] = {
    ...record,
    previousNumbers,
    updatedAt: new Date().toISOString(),
  };

  if (retired[trimmed]) {
    delete retired[trimmed];
  }

  for (const [key, entry] of Object.entries(retired)) {
    if (entry.supersededBy === trimmed) {
      retired[key] = {
        ...entry,
        supersededBy: currentDisplay,
      };
    }
  }

  return { ...projectData, surfaces, retired };
}

export function filterExcludedGeometry(surfaces, projectData) {
  const excluded = getExcludedGeometryKeys(projectData);
  if (!excluded.size) return surfaces;
  return (surfaces || []).filter((s) => !excluded.has(s.surfaceNumber));
}

export function listRemovedSurfaces(projectData) {
  const retired = projectData?.retired || {};
  return Object.entries(retired)
    .filter(([, entry]) => entry.transferType === 'removed')
    .map(([displayKey, entry]) => ({
      displayKey,
      fileKey: entry.fileKey || displayKey,
      retiredAt: entry.retiredAt || null,
      snapshot: entry.snapshot || null,
    }))
    .sort((a, b) => a.displayKey.localeCompare(b.displayKey, undefined, { numeric: true }));
}

/**
 * Replace active surface geometry from an incremental folder scan.
 * Retires the old tracking number; new surface inherits display number and history link.
 */
export function replaceSurfaceWithScanned(projectData, oldKey, newSurface, { transferType = 'replaced' } = {}) {
  const surfaces = { ...(projectData.surfaces || {}) };
  const retired = { ...(projectData.retired || {}) };
  const oldRecord = surfaces[oldKey] || normalizeSurfaceRecord(null);
  const oldDisplay = getDisplayNumber(oldKey, oldRecord);
  const newKey = newSurface.surfaceNumber;

  if (surfaces[newKey] && newKey !== oldKey) {
    throw new Error(`Surface "${newKey}" is already in this project`);
  }

  retired[oldDisplay] = {
    ...(retired[oldDisplay] || {}),
    retiredAt: new Date().toISOString(),
    supersededBy: newKey,
    transferType,
    fileKey: oldKey,
    geometryFingerprint: oldRecord.geometryFingerprint || geometryFingerprint(newSurface),
    snapshot: normalizeSurfaceRecord({ ...oldRecord, displayNumber: oldDisplay }),
  };
  delete surfaces[oldKey];

  const previousNumbers = [...(oldRecord.previousNumbers || [])];
  if (!previousNumbers.includes(oldDisplay)) previousNumbers.push(oldDisplay);
  if (oldKey !== oldDisplay && !previousNumbers.includes(oldKey)) previousNumbers.push(oldKey);

  surfaces[newKey] = {
    ...normalizeSurfaceRecord(null),
    displayNumber: oldDisplay,
    previousNumbers,
    geometryFingerprint: geometryFingerprint(newSurface),
    updatedAt: new Date().toISOString(),
  };

  return { ...projectData, surfaces, retired };
}

/** Update geometry on an existing surface key (same IAM name, new boxes). */
export function refreshSurfaceGeometryRecord(projectData, surfaceKey, newSurface) {
  const surfaces = { ...(projectData.surfaces || {}) };
  const record = surfaces[surfaceKey] || normalizeSurfaceRecord(null);
  surfaces[surfaceKey] = {
    ...record,
    geometryFingerprint: geometryFingerprint(newSurface),
    updatedAt: new Date().toISOString(),
  };
  return { ...projectData, surfaces };
}

/** Add tracking records for newly scanned surfaces (File → Add surface). */
export function addScannedSurfacesToProject(projectData, newSurfaces, activeKeys) {
  const surfaces = { ...(projectData.surfaces || {}) };
  const active = new Set(activeKeys || []);
  const added = [];

  for (const surface of newSurfaces) {
    const key = surface.surfaceNumber;
    if (active.has(key)) {
      throw new Error(`Surface "${key}" is already in the project`);
    }
    if (surfaces[key] && active.has(key)) {
      throw new Error(`Surface "${key}" is already in the project`);
    }
    surfaces[key] = {
      ...(surfaces[key] || normalizeSurfaceRecord(null)),
      geometryFingerprint: geometryFingerprint(surface),
      updatedAt: new Date().toISOString(),
    };
    added.push(key);
  }

  return { projectData: { ...projectData, surfaces }, added };
}

/** Fully retire a surface — removed from main list; snapshot kept for Removed section. */
export function retireSurfaceFully(projectData, surfaceKey, surfaceGeometry) {
  const surfaces = { ...(projectData.surfaces || {}) };
  const retired = { ...(projectData.retired || {}) };
  const record = surfaces[surfaceKey];
  if (!record) throw new Error('Surface not found');

  const display = getDisplayNumber(surfaceKey, record);
  retired[display] = {
    retiredAt: new Date().toISOString(),
    supersededBy: null,
    transferType: 'removed',
    fileKey: surfaceKey,
    geometryFingerprint: record.geometryFingerprint || geometryFingerprint(surfaceGeometry),
    snapshot: normalizeSurfaceRecord({ ...record, displayNumber: display }),
  };
  delete surfaces[surfaceKey];
  return { ...projectData, surfaces, retired };
}
