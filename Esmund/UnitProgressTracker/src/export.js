import { getDisplayNumber } from './project-data.js';

export function buildExportPayload(folderPath, surfaces, projectData, options) {
  const stateById = new Map((options.states || []).map((s) => [s.id, s]));
  const checklistById = new Map((options.checklistItems || []).map((c) => [c.id, c]));

  const rows = surfaces.map((surface) => {
    const record = projectData.surfaces[surface.surfaceNumber] || {};
    const state = stateById.get(record.stateId) || options.states[0];
    const checklist = (options.checklistItems || []).map((item) => ({
      id: item.id,
      label: item.label,
      checked: Boolean(record.checklist && record.checklist[item.id]),
    }));
    return {
      surfaceNumber: surface.surfaceNumber,
      displayNumber: getDisplayNumber(surface.surfaceNumber, record),
      partNumber: surface.partNumber,
      surfaceType: surface.surfaceType,
      surfaceUnitSide: surface.surfaceUnitSide,
      filePath: surface.relativePath || surface.filePath,
      state: state
        ? { id: state.id, name: state.name, color: state.color, fillType: state.fillType || 'solid' }
        : null,
      checklist,
      notes: record.notes || '',
      updatedAt: record.updatedAt || null,
      hidden: Boolean(record.hidden),
      previousNumbers: record.previousNumbers || [],
    };
  });

  return {
    version: 2,
    exportedAt: new Date().toISOString(),
    sourceFolder: folderPath,
    surfaces: rows,
    retired: projectData.retired || {},
  };
}

export function exportToMarkdown(payload) {
  const lines = [
    '# Unit Surface Export',
    '',
    `- **Source folder:** ${payload.sourceFolder}`,
    `- **Exported:** ${payload.exportedAt}`,
    `- **Surface count:** ${payload.surfaces.length}`,
    '',
  ];

  for (const surface of payload.surfaces) {
    lines.push(`## ${surface.surfaceNumber}`);
    lines.push('');
    if (surface.partNumber && surface.partNumber !== surface.surfaceNumber) {
      lines.push(`- **Part number:** ${surface.partNumber}`);
    }
    if (surface.surfaceType) lines.push(`- **Surface type:** ${surface.surfaceType}`);
    if (surface.surfaceUnitSide) lines.push(`- **Unit side:** ${surface.surfaceUnitSide}`);
    if (surface.filePath) lines.push(`- **File:** ${surface.filePath}`);
    if (surface.state) lines.push(`- **Status:** ${surface.state.name}`);
    if (surface.hidden) lines.push('- **Hidden in 3D:** yes');
    if (surface.previousNumbers?.length) {
      lines.push(`- **Previous numbers:** ${surface.previousNumbers.join(', ')}`);
    }
    lines.push('');
    lines.push('### Checklist');
    if (surface.checklist.length === 0) {
      lines.push('- _(none)_');
    } else {
      for (const item of surface.checklist) {
        lines.push(`- [${item.checked ? 'x' : ' '}] ${item.label}`);
      }
    }
    lines.push('');
    lines.push('### Notes');
    lines.push(surface.notes ? surface.notes : '_(none)_');
    lines.push('');
  }

  if (payload.retired && Object.keys(payload.retired).length) {
    lines.push('## Retired surfaces');
    lines.push('');
    for (const [num, entry] of Object.entries(payload.retired)) {
      lines.push(`- **${num}** → ${entry.supersededBy || '_(unlinked)_'} (${entry.transferType || 'unknown'})`);
    }
    lines.push('');
  }

  return lines.join('\n');
}
