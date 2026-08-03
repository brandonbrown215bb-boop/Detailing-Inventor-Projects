/** BOM 391- Inventor export folder planning for Unit Progress Tracker. */

export const INVENTOR_EXPORT_ROOT = 'Inventor';

export const BOM_KEEP_FIELDS = [
  'Part Number',
  'Quantity',
  'Unit',
  'Skid',
  'Segment',
  'Description',
  'Ext. Description',
];

/** Description substrings excluded from tonight's shell folder maker. */
export const SHELL_EXCLUSION_PATTERNS = [
  'DRAIN PAN NIPPLE KIT',
  'ASY F GA-SPC',
  'ISO PLT',
  'OS LATCH ASSY',
  'IS LATCH ASSY',
  'TEST COVER',
  'SUMP DRAIN',
  'FLOOR DRAIN',
  'DOOR',
];

const ILLEGAL_FOLDER_CHARS = /[\\/:*?"<>|]/g;
const MAX_ASSEMBLY_FOLDER_LEN = 120;

export function normalizeBomRow(row) {
  if (!row || typeof row !== 'object') return null;
  const out = {};
  for (const key of BOM_KEEP_FIELDS) {
    out[key] = row[key] != null ? String(row[key]).trim() : '';
  }
  return out;
}

export function is391Part(partNumber) {
  return String(partNumber || '').trim().startsWith('391-');
}

export function combinedDescriptionText(row) {
  const desc = String(row?.Description || '').trim();
  const ext = String(row?.['Ext. Description'] || '').trim();
  return ext ? `${desc} ${ext}` : desc;
}

export function isExcludedFromShellMaker(row) {
  const text = combinedDescriptionText(row).toUpperCase();
  return SHELL_EXCLUSION_PATTERNS.some((pattern) => text.includes(pattern));
}

export function isMisplacedCoilPanel(row) {
  return is391Part(row?.['Part Number']) && String(row?.Segment || '').trim() === '<--';
}

export function isCustomSqAssembly(row) {
  const text = combinedDescriptionText(row);
  return /\bSQ\b/i.test(text) || /^SQ/i.test(String(row?.Description || '').trim());
}

export function parseSkidNumber(skid) {
  const match = String(skid || '').trim().match(/^(\d+)/);
  return match ? match[1].padStart(2, '0') : null;
}

export function normalizeSegmentCode(code) {
  return String(code || '').replace(/[^A-Za-z0-9]/g, '').toUpperCase();
}

function cleanBracketToken(token) {
  return String(token || '').trim().replace(/^\(+/, '').replace(/\)+$/, '').trim();
}

/** Strip outer parens from bracket content: (XA3-XA2/CC1-RF) → XA3-XA2/CC1-RF */
function extractBracketContent(skid) {
  const match = String(skid || '').match(/\[([^\]]*)\]/);
  if (!match) return '';
  let raw = match[1].trim();
  if (raw.startsWith('(') && raw.endsWith(')')) {
    raw = raw.slice(1, -1).trim();
  }
  return raw;
}

function tokenSlashParts(code) {
  return code.split('/').map(cleanBracketToken).filter(Boolean);
}

/** Bracket tokens are listed in reverse segment order (FR-MB → 01 MB, 02 FR). */
export function parseSkidSegmentOrder(skid) {
  const raw = extractBracketContent(skid);
  if (!raw) return [];
  const tokens = raw.split('-').map(cleanBracketToken).filter(Boolean);
  const reversed = [...tokens].reverse();
  return reversed.map((code, index) => {
    const slashParts = tokenSlashParts(code);
    return {
      order: index + 1,
      code,
      folderPrefix: `${String(index + 1).padStart(2, '0')} ${code}`,
      normalized: normalizeSegmentCode(code),
      normalizedParts: slashParts.map(normalizeSegmentCode),
      slashParts,
    };
  });
}

export function resolveSegmentFolder(skid, segment) {
  const seg = String(segment || '').trim();
  if (!seg || seg === '<--') return null;
  const prefix = seg.split(' - ')[0]?.trim() || seg;
  const normalized = normalizeSegmentCode(prefix);
  const order = parseSkidSegmentOrder(skid);

  const exact = order.find((entry) => entry.normalized === normalized);
  if (exact) return exact.folderPrefix;

  for (const entry of order) {
    const partIndex = entry.normalizedParts.findIndex((part) => part === normalized);
    if (partIndex >= 0) {
      const matchedCode = entry.slashParts[partIndex] || entry.code;
      return `${String(entry.order).padStart(2, '0')} ${matchedCode}`;
    }
  }
  return null;
}

export function sanitizeAssemblyFolderName(description, extDescription) {
  const parts = [String(description || '').trim(), String(extDescription || '').trim()].filter(Boolean);
  let name = parts.join(' ').replace(ILLEGAL_FOLDER_CHARS, ' ').replace(/\s+/g, ' ').trim();
  if (!name) name = 'Assembly';
  if (name.length > MAX_ASSEMBLY_FOLDER_LEN) {
    name = name.slice(0, MAX_ASSEMBLY_FOLDER_LEN).trim();
  }
  return name.replace(/[. ]+$/, '') || 'Assembly';
}

function entryKey(partNumber, skid, segmentFolder, assemblyFolder) {
  return `${partNumber}|${skid}|${segmentFolder}|${assemblyFolder}`;
}

export function buildEntryKey(partNumber, skid, segment, description, extDescription = '') {
  return [partNumber, skid, segment, description, extDescription || ''].join('|');
}

/**
 * Build shell folder plan from normalized BOM rows.
 * @returns {{
 *   entries: Array<object>,
 *   excluded: Array<object>,
 *   misplaced: Array<object>,
 *   skipped: Array<object>,
 *   stats: object
 * }}
 */
export function buildShellFolderPlan(rows, { shellRoot = null } = {}) {
  const normalized = (rows || []).map(normalizeBomRow).filter(Boolean);
  const p391 = normalized.filter((row) => is391Part(row['Part Number']));

  const excluded = [];
  const misplaced = [];
  const skipped = [];
  const seenKeys = new Set();
  const folderNameUse = new Map();
  const entries = [];

  for (const row of p391) {
    if (isMisplacedCoilPanel(row)) {
      misplaced.push(row);
      continue;
    }
    if (isExcludedFromShellMaker(row)) {
      excluded.push(row);
      continue;
    }

    const skidNum = parseSkidNumber(row.Skid);
    const segmentFolder = resolveSegmentFolder(row.Skid, row.Segment);
    if (!skidNum || !segmentFolder) {
      skipped.push({ row, reason: !skidNum ? 'unrecognized skid' : 'unmatched segment' });
      continue;
    }

    const assemblyFolder = sanitizeAssemblyFolderName(row.Description, row['Ext. Description']);
    const partNumber = row['Part Number'];
    const dedupeKey = entryKey(partNumber, row.Skid, segmentFolder, assemblyFolder);
    if (seenKeys.has(dedupeKey)) continue;
    seenKeys.add(dedupeKey);

    const segmentPath = `${INVENTOR_EXPORT_ROOT}/Skid ${skidNum}/${segmentFolder}`;
    const baseRelative = `${segmentPath}/${assemblyFolder}`;

    const useKey = `${segmentPath}|${assemblyFolder}`;
    const priorPart = folderNameUse.get(useKey);
    let relativePath = baseRelative;
    if (priorPart && priorPart !== partNumber) {
      relativePath = `${segmentPath}/${assemblyFolder} [${partNumber}]`;
    } else {
      folderNameUse.set(useKey, partNumber);
    }

    entries.push({
      entryKey: buildEntryKey(partNumber, row.Skid, row.Segment, row.Description, row['Ext. Description']),
      partNumber,
      quantity: row.Quantity,
      unit: row.Unit,
      skid: row.Skid,
      segment: row.Segment,
      description: row.Description,
      extDescription: row['Ext. Description'],
      segmentFolder,
      assemblyFolder: relativePath.split('/').pop(),
      relativePath,
      absolutePath: shellRoot ? joinPath(shellRoot, relativePath) : null,
      isCustomSq: isCustomSqAssembly(row),
    });
  }

  entries.sort((a, b) => {
    const skidA = parseSkidNumber(a.skid) || '';
    const skidB = parseSkidNumber(b.skid) || '';
    if (skidA !== skidB) return skidA.localeCompare(skidB, undefined, { numeric: true });
    if (a.segmentFolder !== b.segmentFolder) {
      return a.segmentFolder.localeCompare(b.segmentFolder, undefined, { numeric: true });
    }
    return a.assemblyFolder.localeCompare(b.assemblyFolder);
  });

  return {
    entries,
    excluded,
    misplaced,
    skipped,
    stats: {
      total391Rows: p391.length,
      folderCount: entries.length,
      excludedCount: excluded.length,
      misplacedCount: misplaced.length,
      skippedCount: skipped.length,
      customSqCount: entries.filter((e) => e.isCustomSq).length,
    },
  };
}

function joinPath(root, relative) {
  const sep = root.includes('\\') ? '\\' : '/';
  return `${root.replace(/[\\/]+$/, '')}${sep}${relative.replace(/\//g, sep)}`;
}

export function summarizeMisplacedPanels(misplaced) {
  if (!misplaced?.length) return '';
  const parts = misplaced.map((row) => `${row['Part Number']} (${row.Description})`);
  return `${misplaced.length} coil panel BOM line(s) have no segment (Segment = "<--") and were skipped: ${parts.join('; ')}`;
}
