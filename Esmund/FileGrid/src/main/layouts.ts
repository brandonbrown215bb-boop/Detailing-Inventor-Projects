import { readFile, readdir, unlink, writeFile } from 'fs/promises'
import { join } from 'path'
import { normalizeLayout } from '../shared/normalizeLayout'
import { LAYOUT_SCHEMA_VERSION, type LayoutState } from '../shared/types'
import { ensureDir } from './fileOps'

const LAYOUT_EXT = '.json'

function layoutsDir(userData: string): string {
  return join(userData, 'layouts')
}

function layoutPath(userData: string, id: string): string {
  return join(layoutsDir(userData), `${id}${LAYOUT_EXT}`)
}

export async function listLayouts(userData: string): Promise<LayoutState[]> {
  const dir = layoutsDir(userData)
  await ensureDir(dir)
  const files = await readdir(dir)
  const layouts: LayoutState[] = []
  for (const f of files) {
    if (!f.endsWith(LAYOUT_EXT)) continue
    try {
      const raw = await readFile(join(dir, f), 'utf-8')
      layouts.push(normalizeLayout(JSON.parse(raw) as LayoutState))
    } catch {
      /* skip corrupt */
    }
  }
  layouts.sort((a, b) => b.updatedAt.localeCompare(a.updatedAt))
  return layouts
}

export async function loadLayout(userData: string, id: string): Promise<LayoutState | null> {
  try {
    const raw = await readFile(layoutPath(userData, id), 'utf-8')
    return normalizeLayout(JSON.parse(raw) as LayoutState)
  } catch {
    return null
  }
}

export async function saveLayout(userData: string, layout: LayoutState): Promise<void> {
  await ensureDir(layoutsDir(userData))
  const toSave = { ...layout, updatedAt: new Date().toISOString() }
  await writeFile(layoutPath(userData, layout.id), JSON.stringify(toSave, null, 2), 'utf-8')
}

export async function deleteLayout(userData: string, id: string): Promise<void> {
  try {
    await unlink(layoutPath(userData, id))
  } catch {
    /* ignore */
  }
}

const SESSION_FILE = 'session.json'

export interface SessionMeta {
  activeLayoutId: string | null
}

export async function loadSession(userData: string): Promise<SessionMeta> {
  try {
    const raw = await readFile(join(userData, SESSION_FILE), 'utf-8')
    return JSON.parse(raw) as SessionMeta
  } catch {
    return { activeLayoutId: null }
  }
}

export async function saveSession(userData: string, meta: SessionMeta): Promise<void> {
  await writeFile(join(userData, SESSION_FILE), JSON.stringify(meta, null, 2), 'utf-8')
}

export function defaultLayout(name = 'Default'): LayoutState {
  return {
    id: crypto.randomUUID(),
    name,
    updatedAt: new Date().toISOString(),
    schemaVersion: LAYOUT_SCHEMA_VERSION,
    cellSize: 48,
    view: { panX: 80, panY: 80, zoom: 1 },
    boxes: [],
    links: []
  }
}
