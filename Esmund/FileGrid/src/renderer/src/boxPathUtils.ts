import type { GridBox } from '../../shared/types'

export async function boxPathExists(box: GridBox): Promise<boolean> {
  if (box.kind === 'file') {
    return window.fileGrid.pathIsFile(box.folderPath)
  }
  return window.fileGrid.pathIsDirectory(box.folderPath)
}
