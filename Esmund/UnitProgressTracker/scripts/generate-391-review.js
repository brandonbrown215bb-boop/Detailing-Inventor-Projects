'use strict';

const XLSX = require('C:/Users/esmun/Documents/Cursor/Ce3/node_modules/xlsx');
const fs = require('fs');
const path = require('path');

const SRC = 'C:/Users/esmun/Desktop/test/BOM_FLAT_5E-030269-05_20260319_1057.xlsx';
const OUT = path.join(__dirname, '..', 'docs', 'BOM_391_PARTS_REVIEW.md');

const rows = XLSX.utils.sheet_to_json(XLSX.readFile(SRC).Sheets.Sheet1, { defval: '' });
const p391 = rows.filter((r) => /^391-/i.test(String(r['Part Number'] || '').trim()));

const byPart = new Map();
for (const r of p391) {
  const pn = String(r['Part Number']).trim();
  if (!byPart.has(pn)) byPart.set(pn, []);
  byPart.get(pn).push(r);
}

function esc(s) {
  return String(s ?? '').replace(/\|/g, '\\|').replace(/\n/g, ' ');
}

let md = '# 391- parts — review list\n\n';
md += 'Source: `BOM_FLAT_5E-030269-05_20260319_1057.xlsx` · Unit `5E-030269-05`\n\n';
md += '**Kept columns shown:** Part Number, Quantity, Unit, Skid, Segment, Description, Ext. Description\n\n';
md += `- **112** BOM rows · **${byPart.size}** unique part numbers\n\n`;
md += 'Unit is always `5E-030269-05` on every row (omitted from tables below). *(dup)* marks repeated identical lines in the flat BOM.\n\n';

md += '## Planned import tiers (your notes — not coded yet)\n\n';
md += '| Tier | Prefixes |\n| --- | --- |\n';
md += '| **Focus** | `391-` |\n';
md += '| Related | `291-` |\n';
md += '| Hidden by default | `007-`, `025-`, `026-`, `091-`, `386-` |\n';
md += '| Drop | `035-`, `491-`, `486-`, `251` (no dash) |\n\n';

const sorted = [...byPart.keys()].sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));

for (const pn of sorted) {
  const occ = byPart.get(pn);
  const desc = occ[0].Description;
  const extSet = [...new Set(occ.map((r) => r['Ext. Description']).filter(Boolean))];
  md += `### ${pn}\n\n`;
  md += `- **Description:** ${esc(desc)}\n`;
  if (extSet.length) md += `- **Ext. Description variant(s):** ${extSet.map(esc).join(' · ')}\n`;
  md += `- **BOM line count:** ${occ.length}\n\n`;
  md += '| Qty | Skid | Segment | Description | Ext. Description |\n';
  md += '| ---: | --- | --- | --- | --- |\n';
  const seen = new Set();
  for (const r of occ) {
    const key = [r.Quantity, r.Skid, r.Segment, r.Description, r['Ext. Description']].join('|');
    const dup = seen.has(key) ? ' *(dup)*' : '';
    seen.add(key);
    md += `| ${r.Quantity} | ${esc(r.Skid)} | ${esc(r.Segment)} | ${esc(r.Description)} | ${esc(r['Ext. Description'])}${dup} |\n`;
  }
  md += '\n';
}

fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, md, 'utf8');
console.log('Wrote', OUT);
