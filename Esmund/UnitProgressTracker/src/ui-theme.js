export const DEFAULT_UI_THEME = {
  fontFamily: '"Segoe UI", system-ui, sans-serif',
  fontSizePx: 14,
  colors: {
    text: '#e2e8f0',
    textMuted: '#94a3b8',
    panelBg: '#111827',
    headerBg: '#111827',
    accent: '#38bdf8',
    listText: '#e2e8f0',
  },
};

export const DEFAULT_LAYOUT = {
  leftWidth: 260,
  rightWidth: 320,
};

export const FONT_FAMILY_OPTIONS = [
  { id: 'system', label: 'System UI', value: '"Segoe UI", system-ui, sans-serif' },
  { id: 'segoe', label: 'Segoe UI', value: '"Segoe UI", Tahoma, sans-serif' },
  { id: 'calibri', label: 'Calibri', value: 'Calibri, "Segoe UI", sans-serif' },
  { id: 'arial', label: 'Arial', value: 'Arial, Helvetica, sans-serif' },
  { id: 'tahoma', label: 'Tahoma', value: 'Tahoma, "Segoe UI", sans-serif' },
  { id: 'verdana', label: 'Verdana', value: 'Verdana, Geneva, sans-serif' },
  { id: 'trebuchet', label: 'Trebuchet MS', value: '"Trebuchet MS", Tahoma, sans-serif' },
  { id: 'consolas', label: 'Consolas', value: 'Consolas, "Courier New", monospace' },
  { id: 'georgia', label: 'Georgia', value: 'Georgia, "Times New Roman", serif' },
  { id: 'cambria', label: 'Cambria', value: 'Cambria, Georgia, serif' },
];

export function normalizeUiTheme(raw) {
  const base = raw && typeof raw === 'object' ? raw : {};
  const colors = base.colors && typeof base.colors === 'object' ? base.colors : {};
  const defaults = DEFAULT_UI_THEME.colors;
  const fontSizePx = Number(base.fontSizePx);
  return {
    fontFamily:
      typeof base.fontFamily === 'string' && base.fontFamily.trim()
        ? base.fontFamily.trim()
        : DEFAULT_UI_THEME.fontFamily,
    fontSizePx: Number.isFinite(fontSizePx) ? Math.min(22, Math.max(11, fontSizePx)) : DEFAULT_UI_THEME.fontSizePx,
    colors: {
      text: colors.text || defaults.text,
      textMuted: colors.textMuted || defaults.textMuted,
      panelBg: colors.panelBg || defaults.panelBg,
      headerBg: colors.headerBg || defaults.headerBg,
      accent: colors.accent || defaults.accent,
      listText: colors.listText || defaults.listText,
    },
  };
}

export function normalizeLayout(raw) {
  const base = raw && typeof raw === 'object' ? raw : {};
  const left = Number(base.leftWidth);
  const right = Number(base.rightWidth);
  return {
    leftWidth: Number.isFinite(left) ? Math.min(520, Math.max(180, left)) : DEFAULT_LAYOUT.leftWidth,
    rightWidth: Number.isFinite(right) ? Math.min(560, Math.max(240, right)) : DEFAULT_LAYOUT.rightWidth,
  };
}

export function applyUiTheme(theme) {
  const t = normalizeUiTheme(theme);
  const root = document.documentElement;
  root.style.setProperty('--font-family', t.fontFamily);
  root.style.setProperty('--font-size-base', `${t.fontSizePx}px`);
  root.style.setProperty('--text', t.colors.text);
  root.style.setProperty('--text-muted', t.colors.textMuted);
  root.style.setProperty('--bg-panel', t.colors.panelBg);
  root.style.setProperty('--header-bg', t.colors.headerBg);
  root.style.setProperty('--accent', t.colors.accent);
  root.style.setProperty('--list-text', t.colors.listText);
}

export function applyLayout(layout) {
  const l = normalizeLayout(layout);
  const root = document.documentElement;
  root.style.setProperty('--panel-left-width', `${l.leftWidth}px`);
  root.style.setProperty('--panel-right-width', `${l.rightWidth}px`);
}
