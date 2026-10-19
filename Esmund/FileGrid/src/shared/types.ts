export type BoxKind = 'folder' | 'file'

export interface GridBox {
  id: string
  kind: BoxKind
  title: string
  color: string
  /** Folder path or file path depending on {@link BoxKind}. */
  folderPath: string
  /** Top-left corner in grid units */
  gx: number
  gy: number
  /** Size in grid units */
  gw: number
  gh: number
}

export type LinkLineStyle =
  | 'solid'
  | 'hidden'
  | 'dashed'
  | 'phantom'
  | 'dotDash'
  | 'doubleDotDash'

/** Bump when layout JSON shape or enums change; loaders migrate older versions. */
export const LAYOUT_SCHEMA_VERSION = 2

export interface BoxLink {
  id: string
  fromBoxId: string
  toBoxId: string
  color: string
  thickness: number
  lineStyle: LinkLineStyle
}

export interface LayoutState {
  id: string
  name: string
  updatedAt: string
  /** Omitted in v1 layouts; normalized to {@link LAYOUT_SCHEMA_VERSION} on load. */
  schemaVersion?: number
  cellSize: number
  view: { panX: number; panY: number; zoom: number }
  boxes: GridBox[]
  links: BoxLink[]
}

export const DEFAULT_HOME_VIEW: LayoutState['view'] = { panX: 80, panY: 80, zoom: 1 }

export type ToolbarDock = 'top' | 'bottom' | 'left' | 'right'

/** Along-edge alignment: start/center/end maps to left/middle/right or top/middle/bottom by dock. */
export type ToolbarAlign = 'start' | 'center' | 'end'

/** How the canvas zoom level is adjusted. */
export type ZoomInputMode = 'wheel' | 'keys' | 'both'

export type FolderDropMode = 'boxes' | 'tree'

export interface FolderTreeEntry {
  path: string
  /** 0 = dropped root, 1 = first level of children, etc. */
  depth: number
}

export const DEFAULT_FOLDER_TREE_EXCLUDES = [
  'OldVersions',
  '$Recycle.Bin',
  '.git',
  'node_modules'
]

export interface AppSettings {
  textScale: number
  showFilePath: boolean
  showGrid: boolean
  backgroundColor: string
  gridColor: string
  gridOpacity: number
  fontFamily: string
  fontColor: string
  toolbarDock: ToolbarDock
  toolbarAlign: ToolbarAlign
  zoomInput: ZoomInputMode
  /** Click the drop button to toggle; persisted here. */
  folderDropMode: FolderDropMode
  /** Max folder depth when {@link folderDropMode} is `tree` (0 = root only). */
  folderTreeMaxDepth: number
  folderTreeExcludeNames: string[]
  /** Stop scanning and cap boxes created in one tree drop. */
  folderTreeMaxBoxes: number
}

export const DEFAULT_APP_SETTINGS: AppSettings = {
  textScale: 1,
  showFilePath: true,
  showGrid: true,
  backgroundColor: '#0a0a0a',
  gridColor: '#ffffff',
  gridOpacity: 0.26,
  fontFamily: "'Segoe UI', system-ui, sans-serif",
  fontColor: '#f2f2f2',
  toolbarDock: 'top',
  toolbarAlign: 'start',
  zoomInput: 'wheel',
  folderDropMode: 'boxes',
  folderTreeMaxDepth: 7,
  folderTreeExcludeNames: [...DEFAULT_FOLDER_TREE_EXCLUDES],
  folderTreeMaxBoxes: 250
}

export interface FileCompareSide {
  path: string
  name: string
  sizeLabel: string
  modifiedAt: string | null
  thumbnailDataUrl: string | null
  isDirectory: boolean
}

export interface MoveConflictPayload {
  id: string
  name: string
  sourcePath: string
  existingPath: string
  incoming: FileCompareSide
  existing: FileCompareSide
}

export type ConflictChoice = 'replace' | 'keepBoth' | 'skip' | 'cancel'

export interface MoveResult {
  path: string
  ok: boolean
  error?: string
  skipped?: boolean
  destPath?: string
  renamed?: boolean
  /** User cancelled the whole drop; remaining items were not processed */
  aborted?: boolean
}
