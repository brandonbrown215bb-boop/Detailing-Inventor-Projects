/** Parse unit Config.xml for BOM folder placement (Ce3 shippingSkidList model). */

function normalizeSegmentCode(code) {
  return String(code || '').replace(/[^A-Za-z0-9]/g, '').toUpperCase();
}

function normalizeSegmentGuid(s) {
  if (!s) return '';
  return String(s).trim().replace(/[{}]/g, '').toUpperCase();
}

function parseSegmentList(xml) {
  const segmentList = xml.querySelector('segmentList');
  if (!segmentList) return { segments: [], warnings: ['No segmentList in Config.xml'] };

  const rawSegments = [];
  const totalCounts = {};

  for (const seg of segmentList.children) {
    if (!seg.tagName.toLowerCase().startsWith('segment_')) continue;
    const typeCode = seg.tagName.replace('segment_', '');
    totalCounts[typeCode] = (totalCounts[typeCode] || 0) + 1;
    rawSegments.push({
      typeCode,
      segmentType: seg.querySelector('segmentType')?.textContent?.trim() || typeCode,
      segmentId: normalizeSegmentGuid(seg.querySelector('segmentID')?.textContent),
    });
  }

  const iteration = {};
  for (const seg of rawSegments) {
    if (totalCounts[seg.typeCode] > 1) {
      iteration[seg.typeCode] = (iteration[seg.typeCode] || 0) + 1;
      seg.tagName = `${seg.typeCode}-${iteration[seg.typeCode]}`;
    } else {
      seg.tagName = seg.typeCode;
    }
  }

  return { segments: rawSegments, warnings: [] };
}

function buildSkidsFromShippingSkidList(xml, rawSegments) {
  const warnings = [];
  const listEl = xml.querySelector('shippingSkidList');
  if (!listEl || !listEl.children.length) {
    return { skids: null, warnings: ['No shippingSkidList in Config.xml'] };
  }

  const byId = new Map();
  for (const seg of rawSegments) {
    if (seg.segmentId) byId.set(seg.segmentId, seg);
  }

  const skids = [];
  let skidIndex = 0;

  for (const skidEl of listEl.children) {
    if (!/^shippingSkid$/i.test(skidEl.tagName)) continue;
    skidIndex += 1;

    const refs = [];
    for (const ref of skidEl.querySelectorAll('segmentReference')) {
      const seq = parseInt(ref.querySelector('sequence')?.textContent ?? '0', 10);
      const sid = normalizeSegmentGuid(ref.querySelector('segmentID')?.textContent);
      if (sid) refs.push({ seq, id: sid });
    }
    refs.sort((a, b) => a.seq - b.seq);

    const segments = [];
    for (const { id } of refs) {
      const seg = byId.get(id);
      if (seg) {
        segments.push({ ...seg });
      } else {
        warnings.push(`Skid ${skidIndex}: segment ${id.slice(0, 8)}… not found in segmentList`);
      }
    }

    if (segments.length) {
      const tagNames = segments.map((s) => s.tagName);
      skids.push({
        id: skids.length + 1,
        bracket: tagNames.join('-'),
        segments: segments.map((seg, idx) => ({
          order: idx + 1,
          tagName: seg.tagName,
          typeCode: seg.typeCode,
          segmentType: seg.segmentType,
          normalized: normalizeSegmentCode(seg.tagName),
          folderPrefix: `${String(idx + 1).padStart(2, '0')} ${seg.tagName}`,
        })),
      });
    }
  }

  return {
    skids: skids.length ? skids : null,
    warnings,
  };
}

/**
 * Parse Config.xml text into a compact structure for BOM folder placement.
 * @returns {{ sourceFile?: string, importedAt?: string, skids: Array, warnings: string[], projectId: string|null }}
 */
export function parseUnitConfigXml(xmlText, { sourceFile = null, importedAt = null } = {}) {
  const xml = new DOMParser().parseFromString(xmlText, 'text/xml');
  if (xml.querySelector('parsererror')) {
    throw new Error('Invalid Config.xml — could not parse XML');
  }

  const { segments, warnings: segWarnings } = parseSegmentList(xml);
  const { skids, warnings: skidWarnings } = buildSkidsFromShippingSkidList(xml, segments);
  const warnings = [...segWarnings, ...skidWarnings];

  if (!skids?.length) {
    throw new Error('Config.xml has no shipping skids — cannot map BOM segments');
  }

  const projectId =
    xml.querySelector('projectID')?.textContent?.trim()
    || xml.querySelector('unitOptions projectID')?.textContent?.trim()
    || null;

  return {
    sourceFile,
    importedAt: importedAt || new Date().toISOString(),
    projectId,
    skids,
    warnings,
  };
}

export function normalizeUnitConfig(raw) {
  if (!raw || typeof raw !== 'object' || !Array.isArray(raw.skids) || !raw.skids.length) {
    return null;
  }
  return {
    sourceFile: raw.sourceFile || null,
    importedAt: raw.importedAt || null,
    projectId: raw.projectId || null,
    warnings: Array.isArray(raw.warnings) ? [...raw.warnings] : [],
    skids: raw.skids.map((skid) => ({
      id: skid.id,
      bracket: skid.bracket || '',
      segments: (skid.segments || []).map((seg) => ({
        order: seg.order,
        tagName: seg.tagName,
        typeCode: seg.typeCode || seg.tagName,
        segmentType: seg.segmentType || seg.tagName,
        normalized: seg.normalized || normalizeSegmentCode(seg.tagName),
        folderPrefix: seg.folderPrefix || `${String(seg.order || 1).padStart(2, '0')} ${seg.tagName}`,
      })),
    })),
  };
}

/** Map skid number (01, 04) → ordered segment folder prefixes. */
export function buildSkidSegmentMap(unitConfig) {
  const map = new Map();
  const config = normalizeUnitConfig(unitConfig);
  if (!config) return map;

  for (const skid of config.skids) {
    const skidNum = String(skid.id).padStart(2, '0');
    map.set(skidNum, skid.segments);
  }
  return map;
}

export function segmentPrefixFromBomColumn(segment) {
  const seg = String(segment || '').trim();
  if (!seg || seg === '<--') return '';
  return seg.split(' - ')[0]?.trim() || seg;
}

/**
 * Resolve Inventor segment folder using unit Config (authoritative).
 * @returns {string|null} e.g. "01 FR"
 */
export function resolveSegmentFolderFromConfig(skidNum, segment, unitConfig) {
  const prefix = segmentPrefixFromBomColumn(segment);
  if (!prefix) return null;

  const normalized = normalizeSegmentCode(prefix);
  const segments = buildSkidSegmentMap(unitConfig).get(String(skidNum).padStart(2, '0'));
  if (!segments?.length) return null;

  const match = segments.find((s) => s.normalized === normalized);
  return match?.folderPrefix || null;
}

export function getConfigSegmentsForSkid(unitConfig, skidNum) {
  return buildSkidSegmentMap(unitConfig).get(String(skidNum).padStart(2, '0')) || [];
}

export function unitConfigForPersistence(unitConfig) {
  const normalized = normalizeUnitConfig(unitConfig);
  if (!normalized) return null;
  return {
    sourceFile: normalized.sourceFile,
    importedAt: normalized.importedAt,
    projectId: normalized.projectId,
    warnings: normalized.warnings,
    skids: normalized.skids.map(({ id, bracket, segments }) => ({
      id,
      bracket,
      segments: segments.map(({ order, tagName, typeCode, segmentType, normalized, folderPrefix }) => ({
        order,
        tagName,
        typeCode,
        segmentType,
        normalized,
        folderPrefix,
      })),
    })),
  };
}
