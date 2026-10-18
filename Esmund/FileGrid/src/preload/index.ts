import { contextBridge, ipcRenderer, webUtils } from 'electron'
import type { AppSettings, ConflictChoice, LayoutState, MoveConflictPayload, MoveResult } from '../shared/types'

export interface FileGridApi {
  pickFolder: () => Promise<string | null>
  openFolder: (folderPath: string) => Promise<void>
  pathIsDirectory: (filePath: string) => Promise<boolean>
  movePaths: (targetFolder: string, paths: string[]) => Promise<MoveResult[]>
  onMoveConflict: (handler: (payload: MoveConflictPayload) => void) => () => void
  resolveMoveConflict: (id: string, choice: ConflictChoice, applyToAll: boolean) => void
  loadSettings: () => Promise<AppSettings>
  saveSettings: (settings: AppSettings) => Promise<AppSettings>
  listLayouts: () => Promise<LayoutState[]>
  loadLayout: (id: string) => Promise<LayoutState | null>
  saveLayout: (layout: LayoutState) => Promise<LayoutState>
  deleteLayout: (id: string) => Promise<void>
  createLayout: (name: string) => Promise<LayoutState>
  loadSession: () => Promise<{ activeLayoutId: string | null }>
  saveSession: (activeLayoutId: string | null) => Promise<void>
  getPathForFile: (file: File) => string
}

const api: FileGridApi = {
  pickFolder: () => ipcRenderer.invoke('pick-folder'),
  openFolder: (folderPath) => ipcRenderer.invoke('open-folder', folderPath),
  pathIsDirectory: (filePath) => ipcRenderer.invoke('path-is-directory', filePath),
  movePaths: (targetFolder, paths) => ipcRenderer.invoke('move-paths', targetFolder, paths),
  onMoveConflict: (handler) => {
    const listener = (_e: Electron.IpcRendererEvent, payload: MoveConflictPayload) => handler(payload)
    ipcRenderer.on('move-conflict', listener)
    return () => ipcRenderer.removeListener('move-conflict', listener)
  },
  resolveMoveConflict: (id, choice, applyToAll) => {
    ipcRenderer.send('move-conflict-response', id, choice, applyToAll)
  },
  loadSettings: () => ipcRenderer.invoke('settings-load'),
  saveSettings: (settings) => ipcRenderer.invoke('settings-save', settings),
  listLayouts: () => ipcRenderer.invoke('layouts-list'),
  loadLayout: (id) => ipcRenderer.invoke('layout-load', id),
  saveLayout: (layout) => ipcRenderer.invoke('layout-save', layout),
  deleteLayout: (id) => ipcRenderer.invoke('layout-delete', id),
  createLayout: (name) => ipcRenderer.invoke('layout-create', name),
  loadSession: () => ipcRenderer.invoke('session-load'),
  saveSession: (activeLayoutId) => ipcRenderer.invoke('session-save', activeLayoutId),
  getPathForFile: (file) => webUtils.getPathForFile(file)
}

contextBridge.exposeInMainWorld('fileGrid', api)
