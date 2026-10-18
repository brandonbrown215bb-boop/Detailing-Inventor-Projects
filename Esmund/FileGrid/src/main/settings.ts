import { readFile, writeFile } from 'fs/promises'
import { join } from 'path'
import {
  DEFAULT_APP_SETTINGS,
  type AppSettings,
  type ToolbarAlign,
  type ToolbarDock,
  type ZoomInputMode
} from '../shared/types'

const TOOLBAR_DOCKS: ToolbarDock[] = ['top', 'bottom', 'left', 'right']
const TOOLBAR_ALIGNS: ToolbarAlign[] = ['start', 'center', 'end']
const ZOOM_INPUTS: ZoomInputMode[] = ['wheel', 'keys', 'both']

const SETTINGS_FILE = 'settings.json'

function clamp(n: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, n))
}

function mergeSettings(raw: Partial<AppSettings>): AppSettings {
  const toolbarDock = TOOLBAR_DOCKS.includes(raw.toolbarDock as ToolbarDock)
    ? raw.toolbarDock!
    : DEFAULT_APP_SETTINGS.toolbarDock
  const zoomInput = ZOOM_INPUTS.includes(raw.zoomInput as ZoomInputMode)
    ? raw.zoomInput!
    : DEFAULT_APP_SETTINGS.zoomInput
  const toolbarAlign = TOOLBAR_ALIGNS.includes(raw.toolbarAlign as ToolbarAlign)
    ? raw.toolbarAlign!
    : DEFAULT_APP_SETTINGS.toolbarAlign

  return {
    ...DEFAULT_APP_SETTINGS,
    ...raw,
    textScale: clamp(raw.textScale ?? DEFAULT_APP_SETTINGS.textScale, 0.65, 2),
    showGrid: raw.showGrid ?? DEFAULT_APP_SETTINGS.showGrid,
    gridOpacity: clamp(raw.gridOpacity ?? DEFAULT_APP_SETTINGS.gridOpacity, 0.05, 1),
    toolbarDock,
    toolbarAlign,
    zoomInput
  }
}

export async function loadSettings(userData: string): Promise<AppSettings> {
  try {
    const raw = await readFile(join(userData, SETTINGS_FILE), 'utf-8')
    return mergeSettings(JSON.parse(raw) as Partial<AppSettings>)
  } catch {
    return { ...DEFAULT_APP_SETTINGS }
  }
}

export async function saveSettings(userData: string, settings: AppSettings): Promise<AppSettings> {
  const merged = mergeSettings(settings)
  await writeFile(join(userData, SETTINGS_FILE), JSON.stringify(merged, null, 2), 'utf-8')
  return merged
}
