'use strict';

const XLSX = require('C:/Users/esmun/Documents/Cursor/Ce3/node_modules/xlsx');
const fs = require('fs');
const path = require('path');

const SRC = 'C:/Users/esmun/Desktop/test/BOM_FLAT_5E-030269-05_20260319_1057.xlsx';
const OUT = path.join(__dirname, '..', 'docs', 'BOM_XLSX_IMPORT_MOCKUP.md');

const KEEP_COLS = ['Part Number', 'Quantity', 'Unit', 'Skid', 'Segment', 'Description', 'Ext. Description'];
const DROP_COLS = ['MAPICS Seqc', 'MAPICS Action', 'MAPICS Response', 'Labor Hours'];

const DROP_PREFIXES = new Set(['025', '026', '028', '035', '007', '091']);
const KEEP_PART_PREFIXES = /^(391|291|491|486|386|251|5E)/i;

function shouldKeep(r) {
  const pn = String(r['Part Number'] || '').trim();
  if (!pn) return false;
  if (/^5E/i.test(pn)) return true;
  if (DROP_PREFIXES.has(pn.slice(0, 3))) return false;
  if (!KEEP_PART_PREFIXES.test(pn)) return false;
  if (/^491-/i.test(pn)) return false;
  if (r.Segment === '<--' && !/^391/i.test(pn)) return false;
  return true;
}

function pick(r) {
  const o = {};
  for (const c of KEEP_COLS) o[c] = r[c] ?? '';
  return o;
}

function mdTable(objRows, cols) {
  const esc = (v) => String(v ?? '').replace(/\|/g, '\\|').replace(/\n/g, ' ');
  let s = `| ${cols.join(' | ')} |\n`;
  s += `| ${cols.map(() => '---').join(' | ')} |\n`;
  for (const r of objRows) s += `| ${cols.map((c) => esc(r[c])).join(' | ')} |\n`;
  return s;
}

const rows = XLSX.utils.sheet_to_json(XLSX.readFile(SRC).Sheets.Sheet1, { defval: '' });
const kept = rows.filter(shouldKeep);
const unitRow = kept.find((r) => /^5E/i.test(String(r['Part Number'] || '')));

const bySkid = new Map();
for (const r of kept) {
  const skid = String(r.Skid || '(none)');
  if (!bySkid.has(skid)) bySkid.set(skid, []);
  bySkid.get(skid).push(r);
}

const prefixCounts = {};
for (const r of kept) {
  const pn = String(r['Part Number']);
  const pre = /^5E/i.test(pn) ? '5E (unit)' : pn.slice(0, 3);
  prefixCounts[pre] = (prefixCounts[pre] || 0) + 1;
}

const hardwareDrop = rows.filter((r) => DROP_PREFIXES.has(String(r['Part Number'] || '').slice(0, 3))).length;
const factorDrop = rows.filter((r) => /^491-/i.test(String(r['Part Number'] || ''))).length;
const segPlaceholder = rows.filter((r) => r.Segment === '<--').length;

let md = `# BOM XLSX import — mockup (Unit Surface Viewer)\n\n`;
md += `Generated from test file: \`${path.basename(SRC)}\`\n\n`;
md += `Design mockup for the upcoming import feature — **not implemented yet**.\n\n`;

md += `## Source workbook\n\n`;
md += `- **Sheet:** Sheet1 (only sheet)\n`;
md += `- **Header row:** fixed 11-column layout (same for all BOM_FLAT files)\n`;
md += `- **Data rows:** ${rows.length}\n\n`;

md += `### Column map\n\n`;
md += mdTable(
  [
    ...KEEP_COLS.map((c) => ({ Column: c, Import: '**keep**' })),
    ...DROP_COLS.map((c) => ({ Column: c, Import: 'drop' })),
  ],
  ['Column', 'Import']
);

md += `\n## Proposed row filter\n\n`;
md += `| Rule | Effect |\n| --- | --- |\n`;
md += `| Drop empty part numbers | blank rows |\n`;
md += `| Drop \`025-\`, \`026-\`, \`028-\`, \`035-\`, \`007-\`, \`091-\` | hardware/conduit/stock (~${hardwareDrop} rows) |\n`;
md += `| Keep \`5E…\` unit root | unit header (1 row) |\n`;
md += `| Keep \`391-\`, \`291-\`, \`486-\`, \`386-\`, \`251-\` | shop assemblies, coils, panels |\n`;
md += `| Drop all \`491-\` rows | MAPICS factors (~${factorDrop} rows) |\n`;
md += `| Drop \`Segment = <--\` unless \`391-…\` | segment inheritance placeholders (${segPlaceholder} \`<--\` rows in source) |\n\n`;
md += `**Result:** ${kept.length} kept / ${rows.length - kept.length} dropped\n\n`;

md += `### Kept rows by part prefix\n\n`;
md += mdTable(
  Object.entries(prefixCounts)
    .sort((a, b) => b[1] - a[1])
    .map(([Prefix, Count]) => ({ Prefix, Count })),
  ['Prefix', 'Count']
);

md += `\n## Parsed unit header\n\n\`\`\`json\n${JSON.stringify(pick(unitRow), null, 2)}\n\`\`\`\n\n`;

md += `## Parsed structure (by Skid)\n\n`;
for (const [skid, skidRows] of [...bySkid.entries()].sort((a, b) => a[0].localeCompare(b[0]))) {
  if (skid === '<--') continue;
  const segments = [...new Set(skidRows.map((r) => r.Segment).filter((s) => s && s !== '<--'))];
  md += `### Skid: ${skid}\n\n`;
  md += `- **Rows kept:** ${skidRows.length}\n`;
  md += `- **Segments:** ${segments.join('; ')}\n\n`;
  md += mdTable(skidRows.slice(0, 5).map(pick), KEEP_COLS);
  md += `\n`;
}

const mock = {
  sourceFile: path.basename(SRC),
  importedAt: '(ISO timestamp on import)',
  unit: pick(unitRow),
  filter: {
    keptColumns: KEEP_COLS,
    droppedColumns: DROP_COLS,
    keptRowCount: kept.length,
    droppedRowCount: rows.length - kept.length,
  },
  skids: [...bySkid.entries()]
    .filter(([k]) => k !== '<--')
    .map(([skid, skidRows]) => ({
      skidId: skid,
      segments: [...new Set(skidRows.map((r) => r.Segment))],
      partCount: skidRows.length,
      partsSample: skidRows.slice(0, 3).map(pick),
    })),
};

md += `## Target in-app object (mock JSON)\n\n\`\`\`json\n${JSON.stringify(mock, null, 2)}\n\`\`\`\n\n`;

md += `## Planned UI flow\n\n`;
md += `1. **File → Import BOM…** — pick \`.xlsx\`\n`;
md += `2. Parse Sheet1 with fixed column names\n`;
md += `3. Apply filters above; show summary counts\n`;
md += `4. Store/display by Skid → Segment → parts\n`;
md += `5. Later: cross-link to loaded 391Z surfaces\n\n`;

md += `## Open questions\n\n`;
md += `- Match \`291-\` coil rows to surfaces or keep unit-level only?\n`;
md += `- Collapse duplicate part+skid+segment lines or keep separate qty rows?\n`;
md += `- Map \`391-602xx\` panel parts to 391Z surface IAM numbers — rules TBD\n`;

fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, md, 'utf8');
console.log('Wrote', OUT);
