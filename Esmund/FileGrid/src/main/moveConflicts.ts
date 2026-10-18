import { BrowserWindow, ipcMain } from 'electron'
import { randomUUID } from 'crypto'
import type { ConflictChoice, MoveConflictPayload } from '../shared/types'
import type { MoveConflictInfo } from './fileOps'
import { getComparePair } from './compareInfo'

let mainWindow: BrowserWindow | null = null

export function setMainWindow(win: BrowserWindow): void {
  mainWindow = win
}

export function createConflictResolver(): (info: MoveConflictInfo) => Promise<ConflictChoice> {
  let batchChoice: ConflictChoice | null = null

  return async (info: MoveConflictInfo): Promise<ConflictChoice> => {
    if (batchChoice) return batchChoice

    const compare = await getComparePair(info.sourcePath, info.existingPath)
    const id = randomUUID()

    const payload: MoveConflictPayload = {
      id,
      name: info.name,
      sourcePath: info.sourcePath,
      existingPath: info.existingPath,
      incoming: compare.incoming,
      existing: compare.existing
    }

    return new Promise<ConflictChoice>((resolve) => {
      const timeout = setTimeout(() => resolve('skip'), 10 * 60 * 1000)

      const responseHandler = (
        _e: Electron.IpcMainEvent,
        responseId: string,
        choice: ConflictChoice,
        applyToAll: boolean
      ) => {
        if (responseId !== id) return
        clearTimeout(timeout)
        ipcMain.removeListener('move-conflict-response', responseHandler)
        if (applyToAll) batchChoice = choice
        resolve(choice)
      }

      ipcMain.on('move-conflict-response', responseHandler)
      mainWindow?.webContents.send('move-conflict', payload)
    })
  }
}
