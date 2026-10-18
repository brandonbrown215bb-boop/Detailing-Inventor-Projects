import { useState } from 'react'
import type { AppSettings, GridBox } from '../../../shared/types'
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
  onContextMenu: (e: React.MouseEvent) => void
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
  onContextMenu
}: Props) {
  const [dragOver, setDragOver] = useState(false)
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
      void window.fileGrid.openFolder(box.folderPath)
      return true
    }
    return false
  }

  return (
    <div
      className={`box ${selected ? 'selected' : ''} ${dragOver ? 'drag-over' : ''} ${titleOnly ? 'box-title-only' : ''} ${linkFrom ? 'link-from' : ''} ${linking ? 'linking-mode' : ''}`}
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
        e.preventDefault()
        e.stopPropagation()
        setDragOver(true)
      }}
      onDragLeave={() => setDragOver(false)}
      onDrop={(e) => {
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
        onContextMenu(e)
      }}
    >
      <div
        className="box-header"
        title={titleOnly ? box.folderPath : undefined}
        onPointerDown={(e) => {
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
      <div
        className="resize-handle"
        onPointerDown={(e) => {
          e.stopPropagation()
          onResizeStart(e)
        }}
      />
    </div>
  )
}
