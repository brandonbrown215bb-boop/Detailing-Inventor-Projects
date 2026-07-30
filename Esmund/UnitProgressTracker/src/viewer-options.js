export const MOUSE_ACTIONS = [
  { id: 'rotate', label: 'Rotate' },
  { id: 'pan', label: 'Pan' },
  { id: 'zoom', label: 'Zoom (dolly)' },
  { id: 'none', label: 'None' },
];

export const MOUSE_BUTTONS = [
  { id: 0, label: 'Left' },
  { id: 1, label: 'Middle' },
  { id: 2, label: 'Right' },
];

export const FPS_KEY_OPTIONS = [
  { id: 'Space', label: 'Space' },
  { id: 'Control', label: 'Ctrl' },
  { id: 'Shift', label: 'Shift (either)' },
  { id: 'ShiftLeft', label: 'Left Shift' },
  { id: 'ShiftRight', label: 'Right Shift' },
  { id: 'Alt', label: 'Alt' },
  { id: 'KeyQ', label: 'Q' },
  { id: 'KeyE', label: 'E' },
  { id: 'KeyR', label: 'R' },
  { id: 'KeyF', label: 'F' },
  { id: 'KeyC', label: 'C' },
  { id: 'KeyV', label: 'V' },
  { id: 'KeyZ', label: 'Z' },
  { id: 'KeyX', label: 'X' },
  { id: 'PageUp', label: 'Page Up' },
  { id: 'PageDown', label: 'Page Down' },
];

export const DEFAULT_FPS_KEYS = {
  ascend: 'Space',
  descend: 'Control',
  sprint: 'ShiftLeft',
};

export const DEFAULT_FPS_SPRINT_MULTIPLIER = 2.5;

export const DEFAULT_STICKER_OPTIONS = {
  fontFamily: '"Segoe UI", system-ui, sans-serif',
  textColor: '#f8fafc',
  backgroundColor: '#0f172a',
  borderColor: '#94a3b8',
};

function normalizeHexColor(value, fallback) {
  const v = String(value || '').trim();
  return /^#[0-9A-Fa-f]{6}$/.test(v) ? v : fallback;
}

export const DEFAULT_VIEWER_OPTIONS = {
  showGrid: true,
  fpsControlsEnabled: true,
  fpsSprintMultiplier: DEFAULT_FPS_SPRINT_MULTIPLIER,
  mouseButtons: {
    rotate: 0,
    pan: 2,
    zoom: 1,
  },
  fpsKeys: { ...DEFAULT_FPS_KEYS },
  stickers: { ...DEFAULT_STICKER_OPTIONS },
};

function normalizeMouseButton(value, fallback) {
  const n = Number(value);
  return n === 0 || n === 1 || n === 2 ? n : fallback;
}

function normalizeFpsKey(value, fallback) {
  if (value === null || value === undefined || value === '' || value === 'none') return fallback;
  const allowed = new Set(FPS_KEY_OPTIONS.map((o) => o.id));
  if (typeof value === 'string' && allowed.has(value)) return value;
  return fallback;
}

export function normalizeViewerOptions(raw) {
  const base = raw && typeof raw === 'object' ? raw : {};
  const mb = base.mouseButtons && typeof base.mouseButtons === 'object' ? base.mouseButtons : {};
  const fk = base.fpsKeys && typeof base.fpsKeys === 'object' ? base.fpsKeys : {};
  const st = base.stickers && typeof base.stickers === 'object' ? base.stickers : {};
  const sprintMult = Number(base.fpsSprintMultiplier);

  return {
    showGrid: base.showGrid !== false,
    fpsControlsEnabled: base.fpsControlsEnabled !== false,
    fpsSprintMultiplier: Number.isFinite(sprintMult)
      ? Math.min(5, Math.max(1.25, sprintMult))
      : DEFAULT_FPS_SPRINT_MULTIPLIER,
    mouseButtons: {
      rotate: normalizeMouseButton(mb.rotate, DEFAULT_VIEWER_OPTIONS.mouseButtons.rotate),
      pan: normalizeMouseButton(mb.pan, DEFAULT_VIEWER_OPTIONS.mouseButtons.pan),
      zoom: normalizeMouseButton(mb.zoom, DEFAULT_VIEWER_OPTIONS.mouseButtons.zoom),
    },
    fpsKeys: {
      ascend: normalizeFpsKey(fk.ascend, DEFAULT_FPS_KEYS.ascend),
      descend: normalizeFpsKey(fk.descend, DEFAULT_FPS_KEYS.descend),
      sprint: normalizeFpsKey(fk.sprint, DEFAULT_FPS_KEYS.sprint),
    },
    stickers: {
      fontFamily:
        typeof st.fontFamily === 'string' && st.fontFamily.trim()
          ? st.fontFamily.trim()
          : DEFAULT_STICKER_OPTIONS.fontFamily,
      textColor: normalizeHexColor(st.textColor, DEFAULT_STICKER_OPTIONS.textColor),
      backgroundColor: normalizeHexColor(st.backgroundColor, DEFAULT_STICKER_OPTIONS.backgroundColor),
      borderColor: normalizeHexColor(st.borderColor, DEFAULT_STICKER_OPTIONS.borderColor),
    },
  };
}

export function getViewerOptions(options) {
  return normalizeViewerOptions(options?.viewer);
}

export function fpsKeyLabel(keyId) {
  const match = FPS_KEY_OPTIONS.find((o) => o.id === keyId);
  return match ? match.label : keyId;
}

/** Map action name -> button index for OrbitControls mouseButtons assignment. */
export function buildOrbitMouseButtonMap(viewerOptions) {
  const mb = normalizeViewerOptions(viewerOptions).mouseButtons;
  const actionForButton = { 0: 'none', 1: 'none', 2: 'none' };
  for (const [action, button] of Object.entries(mb)) {
    if (button == null) continue;
    actionForButton[button] = action;
  }
  return actionForButton;
}

export function fpsBindingCodes(binding) {
  if (!binding) return [];
  if (binding === 'Control') return ['ControlLeft', 'ControlRight'];
  if (binding === 'Shift') return ['ShiftLeft', 'ShiftRight'];
  if (binding === 'Alt') return ['AltLeft', 'AltRight'];
  return [binding];
}

export function isFpsBindingDown(binding, pressedKeyCodes) {
  if (!binding || !pressedKeyCodes) return false;
  return fpsBindingCodes(binding).some((code) => pressedKeyCodes.has(code));
}
