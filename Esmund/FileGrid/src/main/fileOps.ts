import { copyFile, cp, mkdir, rename, rm, stat } from 'fs/promises'
import { basename, join } from 'path'
import type { ConflictChoice } from '../shared/types'

export type { ConflictChoice }

export interface MoveConflictInfo {
  sourcePath: string
  destDir: string
  name: string
  existingPath: string
}

export async function isDirectory(p: string): Promise<boolean> {
  try {
    const s = await stat(p)
    return s.isDirectory()
  } catch {
    return false
  }
}

async function destExists(dest: string): Promise<boolean> {
  try {
    await stat(dest)
    return true
  } catch {
    return false
  }
}

async function moveByCopy(sourcePath: string, dest: string, dir: boolean): Promise<void> {
  if (dir) {
    await cp(sourcePath, dest, { recursive: true, errorOnExist: true })
    await rm(sourcePath, { recursive: true, force: true })
  } else {
    await copyFile(sourcePath, dest)
    await rm(sourcePath, { force: true })
  }
}

/** Move source to an exact destination path (parent folder must exist). */
export async function moveToDestPath(sourcePath: string, destPath: string): Promise<void> {
  try {
    await rename(sourcePath, destPath)
    return
  } catch (err: unknown) {
    const code = (err as NodeJS.ErrnoException)?.code
    if (code !== 'EXDEV' && code !== 'EPERM') throw err
  }

  const dir = await isDirectory(sourcePath)
  await moveByCopy(sourcePath, destPath, dir)
}

export async function uniqueDestPath(
  destDir: string,
  name: string,
  sourceIsDir: boolean
): Promise<string> {
  const initial = join(destDir, name)
  if (!(await destExists(initial))) return initial

  let stem: string
  let ext: string
  if (sourceIsDir) {
    stem = name
    ext = ''
  } else {
    const dot = name.lastIndexOf('.')
    if (dot > 0) {
      stem = name.slice(0, dot)
      ext = name.slice(dot)
    } else {
      stem = name
      ext = ''
    }
  }

  for (let n = 2; n < 10_000; n++) {
    const candidateName = ext ? `${stem} (${n})${ext}` : `${stem} (${n})`
    const candidate = join(destDir, candidateName)
    if (!(await destExists(candidate))) return candidate
  }
  throw new Error(`Could not find a unique name for "${name}"`)
}

export class MoveAbortedError extends Error {
  constructor() {
    super('Move cancelled')
    this.name = 'MoveAbortedError'
  }
}

export interface MoveIntoFolderResult {
  destPath: string
  skipped: boolean
  renamed: boolean
}

/**
 * Move file or folder into destDir. On name collision, calls resolveConflict (if provided)
 * or throws.
 */
export async function moveIntoFolder(
  sourcePath: string,
  destDir: string,
  resolveConflict?: (info: MoveConflictInfo) => Promise<ConflictChoice>
): Promise<MoveIntoFolderResult> {
  const name = basename(sourcePath)
  if (!name) throw new Error('Invalid source path')

  const existingPath = join(destDir, name)
  let destPath = existingPath
  let renamed = false

  if (await destExists(existingPath)) {
    if (!resolveConflict) {
      throw new Error(`"${name}" already exists in the destination folder`)
    }
    const choice = await resolveConflict({
      sourcePath,
      destDir,
      name,
      existingPath
    })
    if (choice === 'cancel') {
      throw new MoveAbortedError()
    }
    if (choice === 'skip') {
      return { destPath: existingPath, skipped: true, renamed: false }
    }
    if (choice === 'replace') {
      await rm(existingPath, { recursive: true, force: true })
      destPath = existingPath
    } else {
      const sourceIsDir = await isDirectory(sourcePath)
      destPath = await uniqueDestPath(destDir, name, sourceIsDir)
      renamed = destPath !== existingPath
    }
  }

  await moveToDestPath(sourcePath, destPath)
  return { destPath, skipped: false, renamed }
}

export async function ensureDir(path: string): Promise<void> {
  await mkdir(path, { recursive: true })
}
