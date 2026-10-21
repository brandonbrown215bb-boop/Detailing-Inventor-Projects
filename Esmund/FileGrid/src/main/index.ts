import { app, BrowserWindow, dialog, ipcMain, shell } from 'electron'
import { join } from 'path'
import { loadAppIcon } from './appIcon'
import { isBlockedDropRoot, scanFolderTree } from './folderTree'
import { moveIntoFolder, isDirectory, isFile, MoveAbortedError } from './fileOps'
import { createConflictResolver, setMainWindow } from './moveConflicts'
import {
  defaultLayout,
  deleteLayout,
  listLayouts,
  loadLayout,
  loadSession,
  saveLayout,
  saveSession
} from './layouts'
import { loadSettings, saveSettings } from './settings'
import type { AppSettings, LayoutState, MoveResult } from '../shared/types'

app.commandLine.appendSwitch('high-dpi-support', '1')

let mainWindow: BrowserWindow | null = null

function createWindow(): void {
  const icon = loadAppIcon()
  const win = new BrowserWindow({
    width: 1280,
    height: 800,
    backgroundColor: '#0a0a0a',
    icon,
    webPreferences: {
      preload: join(__dirname, '../preload/index.js'),
      contextIsolation: true,
      nodeIntegration: false
    }
  })

  mainWindow = win
  setMainWindow(win)

  win.webContents.on('did-finish-load', () => {
    win.webContents.setZoomFactor(1)
  })

  if (process.env.ELECTRON_RENDERER_URL) {
    win.loadURL(process.env.ELECTRON_RENDERER_URL)
  } else {
    win.loadFile(join(__dirname, '../renderer/index.html'))
  }
}

app.whenReady().then(() => {
  createWindow()
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow()
  })
})

app.on('window-all-closed', () => {
  mainWindow = null
  if (process.platform !== 'darwin') app.quit()
})

ipcMain.handle('pick-folder', async () => {
  const { canceled, filePaths } = await dialog.showOpenDialog({
    properties: ['openDirectory']
  })
  if (canceled || !filePaths[0]) return null
  return filePaths[0]
})

ipcMain.handle('pick-file', async () => {
  const { canceled, filePaths } = await dialog.showOpenDialog({
    properties: ['openFile']
  })
  if (canceled || !filePaths[0]) return null
  return filePaths[0]
})

ipcMain.handle('open-folder', async (_e, folderPath: string) => {
  await shell.openPath(folderPath)
})

ipcMain.handle('path-is-directory', async (_e, filePath: string) => isDirectory(filePath))

ipcMain.handle('path-is-file', async (_e, filePath: string) => isFile(filePath))

ipcMain.handle('folder-drop-blocked', async (_e, folderPath: string) =>
  isBlockedDropRoot(folderPath)
)

ipcMain.handle(
  'scan-folder-tree',
  async (
    _e,
    rootPath: string,
    maxDepth: number,
    excludeNames: string[],
    maxBoxes: number
  ) => scanFolderTree(rootPath, maxDepth, excludeNames, maxBoxes)
)

ipcMain.handle('move-paths', async (_e, targetFolder: string, paths: string[]): Promise<MoveResult[]> => {
  const resolveConflict = createConflictResolver()
  const results: MoveResult[] = []
  for (const p of paths) {
    try {
      const { destPath, skipped, renamed } = await moveIntoFolder(p, targetFolder, resolveConflict)
      if (skipped) {
        results.push({ path: p, ok: true, skipped: true })
      } else {
        results.push({ path: p, ok: true, destPath, renamed })
      }
    } catch (err: unknown) {
      if (err instanceof MoveAbortedError) {
        results.push({ path: p, ok: false, aborted: true, error: err.message })
        break
      }
      results.push({
        path: p,
        ok: false,
        error: err instanceof Error ? err.message : String(err)
      })
    }
  }
  return results
})

ipcMain.handle('settings-load', async () => loadSettings(app.getPath('userData')))

ipcMain.handle('settings-save', async (_e, settings: AppSettings) =>
  saveSettings(app.getPath('userData'), settings)
)

ipcMain.handle('layouts-list', async () => listLayouts(app.getPath('userData')))

ipcMain.handle('layout-load', async (_e, id: string) => loadLayout(app.getPath('userData'), id))

ipcMain.handle('layout-save', async (_e, layout: LayoutState) => {
  await saveLayout(app.getPath('userData'), layout)
  return layout
})

ipcMain.handle('layout-delete', async (_e, id: string) => {
  await deleteLayout(app.getPath('userData'), id)
})

ipcMain.handle('layout-create', async (_e, name: string) => {
  const layout = defaultLayout(name)
  await saveLayout(app.getPath('userData'), layout)
  return layout
})

ipcMain.handle('session-load', async () => loadSession(app.getPath('userData')))

ipcMain.handle('session-save', async (_e, activeLayoutId: string | null) => {
  await saveSession(app.getPath('userData'), { activeLayoutId })
})
