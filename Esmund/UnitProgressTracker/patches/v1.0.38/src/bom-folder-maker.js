/** BOM 391- Inventor export folder planning for Unit Progress Tracker. */

import { resolveSegmentFolderFromConfig } from './unit-config-parser.js';

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
const DUPLICATE_REF_SUFFIX = ' [ref Skid ';

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

function folderForBracketToken(order, tokenNorm) {
  for (const entry of order) {
    if (entry.normalized === tokenNorm) return entry.folderPrefix;
    const partIndex = entry.normalizedParts.findIndex((part) => part === tokenNorm);
    if (partIndex >= 0) {
      const matchedCode = entry.slashParts[partIndex] || entry.code;
      return `${String(entry.order).padStart(2, '0')} ${matchedCode}`;
    }
  }
  return null;
}

function resolveSegmentFolderFallback(segment, order) {
  const seg = String(segment || '').trim();
  const prefix = seg.split(' - ')[0]?.trim() || seg;
  if (!prefix) return null;
  const slot = order.length > 0 ? String(order.length + 1).padStart(2, '0') : '99';
  return `${slot} ${prefix}`;
}

export function resolveSegmentFolder(skid, segment, { unitConfig = null } = {}) {
  const seg = String(segment || '').trim();
  if (!seg || seg === '<--') return null;

  const skidNum = parseSkidNumber(skid);
  if (unitConfig && skidNum) {
    const fromConfig = resolveSegmentFolderFromConfig(skidNum, segment, unitConfig);
    if (fromConfig) return fromConfig;
  }

  const prefix = seg.split(' - ')[0]?.trim() || seg;
  const normalized = normalizeSegmentCode(prefix);
  const order = parseSkidSegmentOrder(skid);
  if (!order.length) return null;

  const exact = order.find((entry) => entry.normalized === normalized);
  if (exact) return exact.folderPrefix;

  for (const entry of order) {
    const partIndex = entry.normalizedParts.findIndex((part) => part === normalized);
    if (partIndex >= 0) {
      const matchedCode = entry.slashParts[partIndex] || entry.code;
      return `${String(entry.order).padStart(2, '0')} ${matchedCode}`;
    }
  }

  return resolveSegmentFolderFallback(seg, order);
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

function joinPath(root, relative) {
  const sep = root.includes('\\') ? '\\' : '/';
  return `${root.replace(/[\\/]+$/, '')}${sep}${relative.replace(/\//g, sep)}`;
}

function rowEntryKey(row) {
  return buildEntryKey(
    row['Part Number'],
    row.Skid,
    row.Segment,
    row.Description,
    row['Ext. Description']
  );
}

function createEntryFromRow(row, folderNameUse, shellRoot, resolveOptions = {}) {
  const skidNum = parseSkidNumber(row.Skid);
  const segmentFolder = resolveSegmentFolder(row.Skid, row.Segment, resolveOptions);
  if (!skidNum || !segmentFolder) {
    return { skipped: { row, reason: !skidNum ? 'unrecognized skid' : 'unmatched segment' } };
  }

  const assemblyFolder = sanitizeAssemblyFolderName(row.Description, row['Ext. Description']);
  const partNumber = row['Part Number'];
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

  const assemblyFolderFinal = relativePath.split('/').pop();

  return {
    entry: {
      entryKey: rowEntryKey(row),
      partNumber,
      quantity: row.Quantity,
      unit: row.Unit,
      skid: row.Skid,
      segment: row.Segment,
      description: row.Description,
      extDescription: row['Ext. Description'],
      segmentFolder,
      assemblyFolder: assemblyFolderFinal,
      relativePath,
      absolutePath: shellRoot ? joinPath(shellRoot, relativePath) : null,
      isCustomSq: isCustomSqAssembly(row),
      isDuplicateRef: false,
      isModelPrimary: false,
      modelRelativePath: null,
      modelEntryKey: null,
      modelSkid: null,
    },
    dedupeKey: entryKey(partNumber, row.Skid, segmentFolder, assemblyFolder),
  };
}

/**
 * Same part on multiple skids: lowest skid keeps the model folder; higher skids get ref stubs.
 */
export function applyCrossSkidDuplicateRefs(entries, { shellRoot = null } = {}) {
  if (!entries?.length) return entries;

  const byPart = new Map();
  for (const entry of entries) {
    if (!byPart.has(entry.partNumber)) byPart.set(entry.partNumber, []);
    byPart.get(entry.partNumber).push(entry);
  }

  for (const group of byPart.values()) {
    const bySkidNum = new Map();
    for (const entry of group) {
      const skidNum = parseSkidNumber(entry.skid);
      if (!skidNum) continue;
      if (!bySkidNum.has(skidNum)) bySkidNum.set(skidNum, []);
      bySkidNum.get(skidNum).push(entry);
    }
    if (bySkidNum.size < 2) continue;

    const sortedSkids = [...bySkidNum.keys()].sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
    const primarySkid = sortedSkids[0];
    const primaryEntry = [...bySkidNum.get(primarySkid)].sort((a, b) =>
      a.relativePath.localeCompare(b.relativePath, undefined, { numeric: true, sensitivity: 'base' })
    )[0];

    primaryEntry.isModelPrimary = true;
    primaryEntry.modelRelativePath = primaryEntry.relativePath;
    primaryEntry.modelEntryKey = primaryEntry.entryKey;
    primaryEntry.modelSkid = primaryEntry.skid;

    for (const skidNum of sortedSkids) {
      const bucket = bySkidNum.get(skidNum);
      for (const entry of bucket) {
        entry.modelRelativePath = primaryEntry.relativePath;
        entry.modelEntryKey = primaryEntry.entryKey;
        entry.modelSkid = primaryEntry.skid;

        if (skidNum === primarySkid) {
          entry.isDuplicateRef = false;
          if (entry === primaryEntry) entry.isModelPrimary = true;
          continue;
        }

        entry.isDuplicateRef = true;
        entry.isModelPrimary = false;
        const primaryLabel = String(parseInt(primarySkid, 10));
        const stubSuffix = `${DUPLICATE_REF_SUFFIX}${primaryLabel}]`;
        if (!entry.assemblyFolder.includes(DUPLICATE_REF_SUFFIX)) {
          entry.assemblyFolder = `${entry.assemblyFolder}${stubSuffix}`;
          const segmentPath = entry.relativePath.split('/').slice(0, -1).join('/');
          entry.relativePath = `${segmentPath}/${entry.assemblyFolder}`;
          entry.absolutePath = shellRoot ? joinPath(shellRoot, entry.relativePath) : null;
        }
      }
    }
  }

  return entries;
}

function collectEligible391Rows(rows) {
  const normalized = (rows || []).map(normalizeBomRow).filter(Boolean);
  const p391 = normalized.filter((row) => is391Part(row['Part Number']));
  const eligible = [];
  for (const row of p391) {
    if (isMisplacedCoilPanel(row)) continue;
    if (isExcludedFromShellMaker(row)) continue;
    eligible.push(row);
  }
  return eligible;
}

function partNumbersOnMultipleSkids(rows) {
  const byPart = new Map();
  for (const row of rows) {
    const partNumber = row['Part Number'];
    const skidNum = parseSkidNumber(row.Skid);
    if (!partNumber || !skidNum) continue;
    if (!byPart.has(partNumber)) byPart.set(partNumber, new Set());
    byPart.get(partNumber).add(skidNum);
  }
  const shared = new Set();
  for (const [partNumber, skids] of byPart) {
    if (skids.size >= 2) shared.add(partNumber);
  }
  return shared;
}

export function buildShellFolderPlan(rows, { shellRoot = null, unitConfig = null } = {}) {
  const normalized = (rows || []).map(normalizeBomRow).filter(Boolean);
  const p391 = normalized.filter((row) => is391Part(row['Part Number']));

  const excluded = [];
  const misplaced = [];
  const skipped = [];
  const seenKeys = new Set();
  const seenRowKeys = new Set();
  const folderNameUse = new Map();
  const entries = [];
  const resolveOptions = { unitConfig };

  const sharedPartNumbers = partNumbersOnMultipleSkids(collectEligible391Rows(normalized));

  for (const row of p391) {
    if (isMisplacedCoilPanel(row)) {
      misplaced.push(row);
      continue;
    }
    if (isExcludedFromShellMaker(row)) {
      excluded.push(row);
      continue;
    }

    const result = createEntryFromRow(row, folderNameUse, shellRoot, resolveOptions);
    if (result.skipped) {
      skipped.push(result.skipped);
      continue;
    }

    const { entry, dedupeKey } = result;
    const rowKey = rowEntryKey(row);
    if (seenRowKeys.has(rowKey)) continue;
    if (seenKeys.has(dedupeKey)) continue;

    seenRowKeys.add(rowKey);
    seenKeys.add(dedupeKey);
    entries.push(entry);
  }

  // Recover BOM lines for shared parts that did not produce an entry (e.g. dedupe edge cases).
  if (sharedPartNumbers.size > 0) {
    const existingRowKeys = new Set(entries.map((e) => e.entryKey));
    for (const row of p391) {
      if (!sharedPartNumbers.has(row['Part Number'])) continue;
      if (isMisplacedCoilPanel(row) || isExcludedFromShellMaker(row)) continue;
      const rowKey = rowEntryKey(row);
      if (existingRowKeys.has(rowKey)) continue;

      const result = createEntryFromRow(row, folderNameUse, shellRoot, resolveOptions);
      if (result.skipped) {
        skipped.push(result.skipped);
        continue;
      }

      const { entry, dedupeKey } = result;
      if (seenKeys.has(dedupeKey)) {
        // Allow same folder name on another skid for shared parts.
        if (!seenRowKeys.has(rowKey)) {
          seenRowKeys.add(rowKey);
          entries.push(entry);
          existingRowKeys.add(rowKey);
        }
        continue;
      }

      seenKeys.add(dedupeKey);
      seenRowKeys.add(rowKey);
      entries.push(entry);
      existingRowKeys.add(rowKey);
    }
  }

  applyCrossSkidDuplicateRefs(entries, { shellRoot });

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
      duplicateRefCount: entries.filter((e) => e.isDuplicateRef).length,
      modelPrimaryCount: entries.filter((e) => e.isModelPrimary).length,
    },
  };
}

export function summarizeMisplacedPanels(misplaced) {
  if (!misplaced?.length) return '';
  const parts = misplaced.map((row) => `${row['Part Number']} (${row.Description})`);
  return `${misplaced.length} coil panel BOM line(s) have no segment (Segment = "<--") and were skipped: ${parts.join('; ')}`;
}
