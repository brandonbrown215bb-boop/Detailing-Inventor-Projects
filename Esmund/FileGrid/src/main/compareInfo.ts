import { app, nativeImage } from 'electron'
import { stat } from 'fs/promises'
import { basename } from 'path'
import type { FileCompareSide } from '../shared/types'
import { isDirectory } from './fileOps'

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`
}

async function thumbnailForPath(filePath: string, dir: boolean): Promise<string | null> {
  try {
    if (!dir && /\.(png|jpe?g|gif|webp|bmp|ico)$/i.test(filePath)) {
      const img = nativeImage.createFromPath(filePath)
      if (!img.isEmpty()) {
        return img.resize({ width: 128, height: 128 }).toDataURL()
      }
    }
    const icon = await app.getFileIcon(filePath, { size: 'large' })
    if (!icon.isEmpty()) return icon.toDataURL()
  } catch {
    /* ignore */
  }
  return null
}

export async function getFileCompareSide(filePath: string): Promise<FileCompareSide> {
  const dir = await isDirectory(filePath)
  let sizeLabel = dir ? 'Folder' : '—'
  let modifiedAt: string | null = null

  try {
    const s = await stat(filePath)
    if (!dir) sizeLabel = formatBytes(s.size)
    modifiedAt = s.mtime.toLocaleString()
  } catch {
    /* ignore */
  }

  const thumbnailDataUrl = await thumbnailForPath(filePath, dir)

  return {
    path: filePath,
    name: basename(filePath),
    sizeLabel,
    modifiedAt,
    thumbnailDataUrl,
    isDirectory: dir
  }
}

export async function getComparePair(
  sourcePath: string,
  existingPath: string
): Promise<{ incoming: FileCompareSide; existing: FileCompareSide }> {
  const [incoming, existing] = await Promise.all([
    getFileCompareSide(sourcePath),
    getFileCompareSide(existingPath)
  ])
  return { incoming, existing }
}
