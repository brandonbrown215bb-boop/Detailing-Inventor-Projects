/** Fields needed to render 3D surfaces and list tags after IAM folder is gone. */
export function surfaceGeometryForSave(surface) {
  if (!surface) return null;
  return {
    surfaceNumber: surface.surfaceNumber,
    filePath: surface.filePath || '',
    relativePath: surface.relativePath || '',
    sourceType: surface.sourceType || 'iam',
    configPath: surface.configPath || null,
    partNumber: surface.partNumber || surface.surfaceNumber,
    surfaceType: surface.surfaceType || '',
    surfaceUnitSide: surface.surfaceUnitSide || '',
    configurationKind: surface.configurationKind || '',
    skidNumber: surface.skidNumber ?? null,
    skidId: surface.skidId ?? null,
    boxes: Array.isArray(surface.boxes) ? surface.boxes : [],
  };
}

/** Status + checklist templates stored per project (not global APPDATA). */
export function extractProjectOptions(options) {
  if (!options || typeof options !== 'object') return null;
  const states = Array.isArray(options.states)
    ? options.states.map((s) => ({
        id: s.id,
        name: s.name,
        color: s.color,
        fillType: s.fillType || 'solid',
      }))
    : [];
  const checklistItems = Array.isArray(options.checklistItems)
    ? options.checklistItems.map((c) => ({
        id: c.id,
        label: c.label,
      }))
    : [];
  if (!states.length && !checklistItems.length) return null;
  return { states, checklistItems };
}

export function parseProjectOptions(raw) {
  if (!raw || typeof raw !== 'object') {
    return { states: [], checklistItems: [] };
  }
  const states = Array.isArray(raw.states)
    ? raw.states
        .filter((s) => s && s.id)
        .map((s) => ({
          id: String(s.id),
          name: String(s.name || s.id),
          color: s.color || '#64748b',
          fillType: s.fillType || 'solid',
        }))
    : [];
  const checklistItems = Array.isArray(raw.checklistItems)
    ? raw.checklistItems
        .filter((c) => c && c.id)
        .map((c) => ({
          id: String(c.id),
          label: String(c.label || c.id),
        }))
    : [];
  return { states, checklistItems };
}

export function buildProjectSavePayload(folderPath, geometrySurfaces, projectData, scanSource, options) {
  const surfaces = projectData?.surfaces && typeof projectData.surfaces === 'object' ? projectData.surfaces : {};
  const retired = projectData?.retired && typeof projectData.retired === 'object' ? projectData.retired : {};
  const bom = projectData?.bom && typeof projectData.bom === 'object' ? projectData.bom : null;
  const geometry = (geometrySurfaces || [])
    .map(surfaceGeometryForSave)
    .filter((s) => s && s.boxes.length > 0);
  const projectOptions = extractProjectOptions(options);

  const payload = {
    version: 3,
    savedAt: new Date().toISOString(),
    sourceFolder: folderPath || null,
    scanSource: scanSource || 'unknown',
    geometryCachedAt: new Date().toISOString(),
    geometrySurfaces: geometry,
    surfaces,
    retired,
    bom,
    updatedAt: projectData?.updatedAt || new Date().toISOString(),
  };
  if (projectOptions) payload.projectOptions = projectOptions;
  return payload;
}

export function parseSavedProjectFile(data) {
  if (!data || typeof data !== 'object') return null;

  const sourceFolder = data.sourceFolder || data.folderPath || null;
  const geometrySurfaces = Array.isArray(data.geometrySurfaces) ? data.geometrySurfaces : [];

  let trackingSurfaces = null;
  let retired = null;
  let bom = null;

  if (data.projectData && typeof data.projectData === 'object') {
    trackingSurfaces = data.projectData.surfaces;
    retired = data.projectData.retired;
    bom = data.projectData.bom;
  } else if (data.surfaces && typeof data.surfaces === 'object' && !Array.isArray(data.surfaces)) {
    trackingSurfaces = data.surfaces;
    retired = data.retired;
    bom = data.bom;
  }

  if (!trackingSurfaces && geometrySurfaces.length === 0) return null;

  return {
    sourceFolder,
    geometrySurfaces,
    scanSource: data.scanSource || (geometrySurfaces.length ? 'saved' : 'unknown'),
    projectOptions: data.projectOptions || null,
    projectPayload: {
      version: 2,
      sourceFolder,
      surfaces: trackingSurfaces || {},
      retired: retired || {},
      bom: bom || null,
      updatedAt: data.updatedAt || data.savedAt || null,
    },
  };
}
