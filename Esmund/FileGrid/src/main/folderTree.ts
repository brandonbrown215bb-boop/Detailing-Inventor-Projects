import { readdir, stat } from 'fs/promises'
import { basename, normalize } from 'path'
import type { FolderTreeEntry } from '../shared/types'

const DRIVE_ROOT = /^[A-Za-z]:[\\/]?$/

export function isBlockedDropRoot(folderPath: string): boolean {
  const n = normalize(folderPath).replace(/[/\\]+$/, '')
  if (DRIVE_ROOT.test(n)) return true
  const base = basename(n).toLowerCase()
  const blocked = new Set([
    'windows',
    'program files',
    'program files (x86)',
    'programdata',
    '$recycle.bin'
  ])
  return blocked.has(base)
}

function excludeName(name: string, excludeNames: string[]): boolean {
  const lower = name.toLowerCase()
  return excludeNames.some((ex) => ex && ex.toLowerCase() === lower)
}

async function walk(
  dirPath: string,
  depth: number,
  maxDepth: number,
  excludeNames: string[],
  out: FolderTreeEntry[],
  maxBoxes: number
): Promise<void> {
  if (out.length >= maxBoxes) return
  if (depth > maxDepth) return

  out.push({ path: dirPath, depth })

  if (depth >= maxDepth) return

  let names: string[]
  try {
    names = await readdir(dirPath)
  } catch {
    return
  }
  names.sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }))

  for (const name of names) {
    if (out.length >= maxBoxes) return
    if (excludeName(name, excludeNames)) continue
    const child = normalize(`${dirPath}/${name}`)
    try {
      const s = await stat(child)
      if (!s.isDirectory()) continue
    } catch {
      continue
    }
    await walk(child, depth + 1, maxDepth, excludeNames, out, maxBoxes)
  }
}

export async function scanFolderTree(
  rootPath: string,
  maxDepth: number,
  excludeNames: string[],
  maxBoxes: number
): Promise<FolderTreeEntry[]> {
  const root = normalize(rootPath)
  if (!(await stat(root).then((s) => s.isDirectory()).catch(() => false))) {
    return []
  }
  const out: FolderTreeEntry[] = []
  await walk(root, 0, maxDepth, excludeNames, out, maxBoxes)
  return out
}
