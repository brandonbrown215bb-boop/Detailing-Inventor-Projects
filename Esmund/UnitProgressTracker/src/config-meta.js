export const DEFAULT_LIST_DISPLAY = {
  nameMode: 'both',
  showTypeTag: true,
  showSkidTag: true,
  showSideTag: true,
  sortMode: 'default',
};

export function normalizeListDisplay(raw) {
  const base = raw && typeof raw === 'object' ? raw : {};
  const nameMode = base.nameMode === 'long' || base.nameMode === 'short' ? base.nameMode : 'both';
  const sortMode =
    base.sortMode === 'skid' || base.sortMode === 'type' || base.sortMode === 'skid-type'
      ? base.sortMode
      : 'default';
  return {
    nameMode,
    showTypeTag: base.showTypeTag !== false,
    showSkidTag: base.showSkidTag !== false,
    showSideTag: base.showSideTag !== false,
    sortMode,
  };
}

export function getListDisplay(options) {
  return normalizeListDisplay(options?.listDisplay);
}

export function sortSurfacesForList(surfaces, sortMode) {
  const copy = [...surfaces];
  const byNumber = (a, b) => a.surfaceNumber.localeCompare(b.surfaceNumber, undefined, { numeric: true });

  if (sortMode === 'skid') {
    return copy.sort((a, b) => {
      const sa = a.skidId ?? 9999;
      const sb = b.skidId ?? 9999;
      if (sa !== sb) return sa - sb;
      return byNumber(a, b);
    });
  }

  if (sortMode === 'type') {
    return copy.sort((a, b) => {
      const ta = (a.configurationKind || '').localeCompare(b.configurationKind || '');
      if (ta !== 0) return ta;
      return byNumber(a, b);
    });
  }

  if (sortMode === 'skid-type') {
    return copy.sort((a, b) => {
      const sa = a.skidId ?? 9999;
      const sb = b.skidId ?? 9999;
      if (sa !== sb) return sa - sb;
      const ta = (a.configurationKind || '').localeCompare(b.configurationKind || '');
      if (ta !== 0) return ta;
      return byNumber(a, b);
    });
  }

  return copy.sort(byNumber);
}

export function formatSkidTag(surface) {
  if (surface?.skidId == null) return '';
  return `S${surface.skidId}`;
}

export function formatSkidDisplay(surface) {
  if (surface?.skidId == null) return '';
  return String(surface.skidId);
}

export function formatTypeTag(surface) {
  const kind = surface?.configurationKind || '';
  if (kind === 'UnitBase') return 'Base';
  return kind;
}

export function formatTypeDisplay(surface) {
  return formatTypeTag(surface);
}

export function formatSideTag(surface) {
  const side = surface?.surfaceUnitSide;
  if (!side) return '';
  return String(side).trim();
}

export function buildSurfaceInfoLines(surface) {
  if (!surface) return [];
  const lines = [];
  const skid = formatSkidDisplay(surface);
  if (skid) lines.push({ label: 'Skid', value: skid });
  if (surface.partNumber) lines.push({ label: 'Part', value: surface.partNumber });
  const type = formatTypeDisplay(surface);
  if (type) lines.push({ label: 'Type', value: type });
  const side = formatSideTag(surface);
  if (side) lines.push({ label: 'Side', value: side });
  return lines;
}
