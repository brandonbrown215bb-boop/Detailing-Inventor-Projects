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

export function buildProjectSavePayload(folderPath, geometrySurfaces, projectData, scanSource) {
  const surfaces = projectData?.surfaces && typeof projectData.surfaces === 'object' ? projectData.surfaces : {};
  const retired = projectData?.retired && typeof projectData.retired === 'object' ? projectData.retired : {};
  const bom = projectData?.bom && typeof projectData.bom === 'object' ? projectData.bom : null;
  const geometry = (geometrySurfaces || [])
    .map(surfaceGeometryForSave)
    .filter((s) => s && s.boxes.length > 0);

  return {
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
