import { useEffect, useState } from 'react'
import type { AppSettings, GridBox } from '../../../shared/types'
import { boxPathExists } from '../boxPathUtils'
import type { ViewTransform } from '../gridMath'
import { gridRectToScreen } from '../gridMath'

interface Props {
  box: GridBox
  cellSize: number
  view: ViewTransform
  settings: AppSettings
  selected: boolean
  linking: boolean
  linkFrom: boolean
  onSelect: () => void
  onLinkPick: () => void
  onDragStart: (e: React.PointerEvent) => void
  onResizeStart: (e: React.PointerEvent) => void
  onDropPaths: (paths: string[]) => void
  onOpenBox: () => void
  onContextMenu: (e: React.MouseEvent) => void
  shouldSuppressContextMenu?: () => boolean
}

export function FolderBox({
  box,
  cellSize,
  view,
  settings,
  selected,
  linking,
  linkFrom,
  onSelect,
  onLinkPick,
  onDragStart,
  onResizeStart,
  onDropPaths,
  onOpenBox,
  onContextMenu,
  shouldSuppressContextMenu
}: Props) {
  const [dragOver, setDragOver] = useState(false)
  const [pathMissing, setPathMissing] = useState(false)

  useEffect(() => {
    let cancelled = false
    void boxPathExists(box).then((ok) => {
      if (!cancelled) setPathMissing(!ok)
    })
    return () => {
      cancelled = true
    }
  }, [box.folderPath, box.kind])
  const { left, top, width, height } = gridRectToScreen(
    box.gx,
    box.gy,
    box.gw,
    box.gh,
    view,
    cellSize
  )

  const titleOnly = !settings.showFilePath
  const scale = settings.textScale
  const shortSide = Math.min(width, height)
  const titlePx = titleOnly
    ? Math.min(28 * scale, Math.max(11 * scale, Math.round(shortSide * 0.24 * scale)))
    : Math.min(18 * scale, Math.max(9 * scale, Math.round(shortSide * 0.2 * scale)))
  const pathPx = Math.min(11 * scale, Math.max(8 * scale, Math.round(shortSide * 0.14 * scale)))
  const padY = shortSide < 56 ? 2 : titleOnly ? 8 : 4
  const padX = shortSide < 56 ? 4 : titleOnly ? 10 : 6

  const openIfShift = (e: React.PointerEvent) => {
    if (e.shiftKey) {
      e.stopPropagation()
      e.preventDefault()
      onOpenBox()
      return true
    }
    return false
  }

  return (
    <div
      className={`box ${box.kind === 'file' ? 'box-file' : 'box-folder'} ${selected ? 'selected' : ''} ${dragOver ? 'drag-over' : ''} ${titleOnly ? 'box-title-only' : ''} ${linkFrom ? 'link-from' : ''} ${linking ? 'linking-mode' : ''} ${pathMissing ? 'box-missing-path' : ''}`}
      style={{
        left,
        top,
        width,
        height,
        backgroundColor: box.color,
        color: settings.fontColor,
        fontFamily: settings.fontFamily,
        ['--box-title-size' as string]: `${titlePx}px`,
        ['--box-path-size' as string]: `${pathPx}px`,
        ['--box-pad-y' as string]: `${padY}px`,
        ['--box-pad-x' as string]: `${padX}px`
      }}
      onPointerDown={(e) => {
        if (e.button !== 0) return
        if (openIfShift(e)) return
        if (linking) {
          e.stopPropagation()
          onLinkPick()
          return
        }
        if (titleOnly) return
        e.stopPropagation()
        onSelect()
      }}
      onDragOver={(e) => {
        if (box.kind === 'file') return
        e.preventDefault()
        e.stopPropagation()
        setDragOver(true)
      }}
      onDragLeave={() => setDragOver(false)}
      onDrop={(e) => {
        if (box.kind === 'file') return
        e.preventDefault()
        e.stopPropagation()
        setDragOver(false)
        const paths = [...e.dataTransfer.files]
          .map((f) => window.fileGrid.getPathForFile(f))
          .filter(Boolean)
        if (paths.length) onDropPaths(paths)
      }}
      onContextMenu={(e) => {
        e.preventDefault()
        e.stopPropagation()
        if (shouldSuppressContextMenu?.()) return
        onContextMenu(e)
      }}
    >
      <div
        className="box-header"
        title={titleOnly ? box.folderPath : undefined}
        onPointerDown={(e) => {
          if (e.button !== 0) return
          if (openIfShift(e)) return
          if (linking) {
            e.stopPropagation()
            onLinkPick()
            return
          }
          e.stopPropagation()
          onSelect()
          onDragStart(e)
        }}
      >
        {box.title || 'Untitled'}
      </div>
      {settings.showFilePath && (
        <div className="box-path" title={box.folderPath}>
          {box.folderPath}
        </div>
      )}
      {box.kind === 'file' && (
        <div className="box-file-badge" aria-hidden title="File shortcut">
          <svg viewBox="0 0 24 24" width="18" height="18" focusable="false">
            <path
              fill="currentColor"
              d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8l-6-6zm-1 2 5 5h-5V4zM8 12h8v2H8v-2zm0 4h8v2H8v-2z"
            />
          </svg>
        </div>
      )}
      <div
        className="resize-handle"
        onPointerDown={(e) => {
          if (e.button !== 0) return
          e.stopPropagation()
          onResizeStart(e)
        }}
      />
    </div>
  )
}
