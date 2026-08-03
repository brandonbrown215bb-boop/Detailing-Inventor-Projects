import { parseSkidNumber } from './bom-folder-maker.js';

export const DEFAULT_BOM_LIST_DISPLAY = {
  sortMode: 'shell',
  searchText: '',
  skidFilter: '',
  segmentFilter: '',
  customSqOnly: false,
};

export const BOM_SORT_OPTIONS = [
  { id: 'shell', label: 'Inventor nest' },
  { id: 'part', label: 'Part number' },
  { id: 'description', label: 'Description' },
  { id: 'skid', label: 'Skid' },
  { id: 'segment', label: 'Segment' },
  { id: 'folder', label: 'Folder path' },
];

export function normalizeBomListDisplay(raw) {
  const base = raw && typeof raw === 'object' ? raw : {};
  const sortMode = BOM_SORT_OPTIONS.some((opt) => opt.id === base.sortMode) ? base.sortMode : 'shell';
  return {
    sortMode,
    searchText: typeof base.searchText === 'string' ? base.searchText : '',
    skidFilter: typeof base.skidFilter === 'string' ? base.skidFilter : '',
    segmentFilter: typeof base.segmentFilter === 'string' ? base.segmentFilter : '',
    customSqOnly: Boolean(base.customSqOnly),
  };
}

export function getBomListDisplay(options) {
  return normalizeBomListDisplay(options?.bomListDisplay);
}

function entrySkidNumber(entry) {
  return parseSkidNumber(entry?.skid) || '';
}

function comparePartNumbers(a, b) {
  return a.partNumber.localeCompare(b.partNumber, undefined, { numeric: true });
}

export function filterBomEntries(entries, display) {
  const d = normalizeBomListDisplay(display);
  let result = [...(entries || [])];

  if (d.customSqOnly) {
    result = result.filter((entry) => entry.isCustomSq);
  }

  if (d.skidFilter) {
    result = result.filter((entry) => entrySkidNumber(entry) === d.skidFilter);
  }

  if (d.segmentFilter) {
    result = result.filter((entry) => entry.segmentFolder === d.segmentFilter);
  }

  const query = d.searchText.trim().toLowerCase();
  if (query) {
    result = result.filter((entry) => {
      const haystack = [
        entry.partNumber,
        entry.description,
        entry.extDescription,
        entry.segment,
        entry.skid,
        entry.segmentFolder,
        entry.relativePath,
        entry.assemblyFolder,
      ]
        .join(' ')
        .toLowerCase();
      return haystack.includes(query);
    });
  }

  return result;
}

export function sortBomEntries(entries, sortMode) {
  const copy = [...(entries || [])];
  const mode = normalizeBomListDisplay({ sortMode }).sortMode;

  if (mode === 'part') {
    return copy.sort(comparePartNumbers);
  }

  if (mode === 'description') {
    return copy.sort((a, b) => {
      const textA = `${a.description} ${a.extDescription}`.trim();
      const textB = `${b.description} ${b.extDescription}`.trim();
      const byDesc = textA.localeCompare(textB, undefined, { sensitivity: 'base' });
      return byDesc || comparePartNumbers(a, b);
    });
  }

  if (mode === 'skid') {
    return copy.sort((a, b) => {
      const bySkid = entrySkidNumber(a).localeCompare(entrySkidNumber(b), undefined, { numeric: true });
      if (bySkid !== 0) return bySkid;
      const bySegment = a.segmentFolder.localeCompare(b.segmentFolder, undefined, { numeric: true });
      return bySegment || comparePartNumbers(a, b);
    });
  }

  if (mode === 'segment') {
    return copy.sort((a, b) => {
      const bySegment = a.segmentFolder.localeCompare(b.segmentFolder, undefined, { numeric: true });
      return bySegment || comparePartNumbers(a, b);
    });
  }

  if (mode === 'folder') {
    return copy.sort((a, b) =>
      a.relativePath.localeCompare(b.relativePath, undefined, { numeric: true, sensitivity: 'base' })
    );
  }

  return copy.sort((a, b) => {
    const bySkid = entrySkidNumber(a).localeCompare(entrySkidNumber(b), undefined, { numeric: true });
    if (bySkid !== 0) return bySkid;
    const bySegment = a.segmentFolder.localeCompare(b.segmentFolder, undefined, { numeric: true });
    if (bySegment !== 0) return bySegment;
    return a.assemblyFolder.localeCompare(b.assemblyFolder, undefined, { sensitivity: 'base' });
  });
}

export function applyBomListDisplay(entries, display) {
  return sortBomEntries(filterBomEntries(entries, display), display?.sortMode);
}

export function getBomFilterOptions(entries) {
  const skids = new Map();
  const segments = new Map();

  for (const entry of entries || []) {
    const skidNum = entrySkidNumber(entry);
    if (skidNum) skids.set(skidNum, `Skid ${skidNum}`);
    if (entry.segmentFolder) segments.set(entry.segmentFolder, entry.segmentFolder);
  }

  return {
    skids: [...skids.entries()].sort((a, b) => a[0].localeCompare(b[0], undefined, { numeric: true })),
    segments: [...segments.entries()].sort((a, b) => a[0].localeCompare(b[0], undefined, { numeric: true })),
  };
}
