import { useCallback, useEffect, useRef, useState } from 'react'
import type { AppSettings, BoxLink, GridBox, LayoutState } from '../../../shared/types'
import { boxPathExists } from '../boxPathUtils'
import { isTypingContext } from '../keyboard'
import { screenToGrid } from '../gridMath'
import { BoxLinksLayer } from './BoxLinksLayer'
import { CanvasQuickEdit } from './CanvasQuickEdit'
import { FolderBox } from './FolderBox'
import { GridCanvas } from './GridCanvas'

interface Props {
  layout: LayoutState
  settings: AppSettings
  gridStroke: string
  selectedId: string | null
  selectedLinkId: string | null
  linking: boolean
  linkFromId: string | null
  onSelect: (id: string | null) => void
  onSelectLink: (id: string | null) => void
  onLinkPick: (boxId: string) => void
  onUpdateLayout: (patch: Partial<LayoutState> | ((prev: LayoutState) => LayoutState)) => void
  onMovePaths: (box: GridBox, paths: string[]) => void
  onPatchBox: (boxId: string, patch: Partial<GridBox>) => void
  onRemoveBox: (boxId: string) => void
  onPatchLink: (linkId: string, patch: Partial<BoxLink>) => void
  onRemoveLink: (linkId: string) => void
  onCopyPath: (path: string) => void
  onRelinkFolder: (boxId: string) => void
  onOpenBox: (box: GridBox) => void
}

export function GridViewport({
  layout,
  settings,
  gridStroke,
  selectedId,
  selectedLinkId,
  linking,
  linkFromId,
  onSelect,
  onSelectLink,
  onLinkPick,
  onUpdateLayout,
  onMovePaths,
  onPatchBox,
  onRemoveBox,
  onPatchLink,
  onRemoveLink,
  onCopyPath,
  onRelinkFolder,
  onOpenBox
}: Props) {
  const viewportRef = useRef<HTMLDivElement>(null)
  const [quickEdit, setQuickEdit] = useState<
    | null
    | { kind: 'box'; id: string; x: number; y: number }
    | { kind: 'link'; id: string; x: number; y: number }
  >(null)
  const [panning, setPanning] = useState(false)
  const [spaceHeld, setSpaceHeld] = useState(false)
  const panStart = useRef({ x: 0, y: 0, panX: 0, panY: 0 })
  const dragBox = useRef<{
    id: string
    startGx: number
    startGy: number
    pointerGx: number
    pointerGy: number
  } | null>(null)
  const suppressBoxContextMenu = useRef(false)
  const resizeBox = useRef<{
    id: string
    startGw: number
    startGh: number
    pointerGx: number
    pointerGy: number
  } | null>(null)

  const { cellSize, view, boxes, links } = layout

  const closeQuickEdit = useCallback(() => setQuickEdit(null), [])

  const handleBoxContextMenu = (boxId: string, e: React.MouseEvent) => {
    if (e.ctrlKey) {
      onRemoveBox(boxId)
      setQuickEdit(null)
      return
    }
    onSelect(boxId)
    onSelectLink(null)
    setQuickEdit({ kind: 'box', id: boxId, x: e.clientX, y: e.clientY })
  }

  const handleLinkContextMenu = (linkId: string, e: React.MouseEvent) => {
    if (e.shiftKey) {
      onRemoveLink(linkId)
      setQuickEdit(null)
      return
    }
    onSelectLink(linkId)
    onSelect(null)
    setQuickEdit({ kind: 'link', id: linkId, x: e.clientX, y: e.clientY })
  }

  const quickEditBox =
    quickEdit?.kind === 'box' ? boxes.find((b) => b.id === quickEdit.id) ?? null : null
  const quickEditLink =
    quickEdit?.kind === 'link' ? links.find((l) => l.id === quickEdit.id) ?? null : null
  const [quickEditPathMissing, setQuickEditPathMissing] = useState(false)

  useEffect(() => {
    if (!quickEditBox) {
      setQuickEditPathMissing(false)
      return
    }
    void boxPathExists(quickEditBox).then((ok) => setQuickEditPathMissing(!ok))
  }, [quickEditBox?.id, quickEditBox?.folderPath, quickEditBox?.kind])

  const setView = useCallback(
    (patch: Partial<LayoutState['view']>) => {
      onUpdateLayout((prev) => {
        const next = { ...prev.view, ...patch }
        if (patch.panX !== undefined) next.panX = Math.round(next.panX)
        if (patch.panY !== undefined) next.panY = Math.round(next.panY)
        return { ...prev, view: next }
      })
    },
    [onUpdateLayout]
  )

  const zoomAtScreenPoint = useCallback(
    (sx: number, sy: number, factor: number) => {
      const newZoom = Math.min(3, Math.max(0.25, view.zoom * factor))
      const wx = (sx - view.panX) / view.zoom
      const wy = (sy - view.panY) / view.zoom
      setView({
        zoom: newZoom,
        panX: Math.round(sx - wx * newZoom),
        panY: Math.round(sy - wy * newZoom)
      })
    },
    [view, setView]
  )

  const zoomAtViewportCenter = useCallback(
    (factor: number) => {
      const rect = viewportRef.current?.getBoundingClientRect()
      if (!rect) return
      zoomAtScreenPoint(rect.width / 2, rect.height / 2, factor)
    },
    [zoomAtScreenPoint]
  )

  const updateBox = useCallback(
    (id: string, patch: Partial<GridBox>) => {
      onUpdateLayout((prev) => ({
        ...prev,
        boxes: prev.boxes.map((b) => (b.id === id ? { ...b, ...patch } : b))
      }))
    },
    [onUpdateLayout]
  )

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.code === 'Space' && !isTypingContext(e.target)) {
        e.preventDefault()
        setSpaceHeld(true)
      }
    }
    const onKeyUp = (e: KeyboardEvent) => {
      if (e.code === 'Space') setSpaceHeld(false)
    }
    window.addEventListener('keydown', onKeyDown)
    window.addEventListener('keyup', onKeyUp)
    return () => {
      window.removeEventListener('keydown', onKeyDown)
      window.removeEventListener('keyup', onKeyUp)
    }
  }, [])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Delete' || e.key === 'Backspace') {
        if (!selectedId || isTypingContext(e.target)) return
        e.preventDefault()
        onUpdateLayout((prev) => ({
          ...prev,
          boxes: prev.boxes.filter((b) => b.id !== selectedId),
          links: prev.links.filter((l) => l.fromBoxId !== selectedId && l.toBoxId !== selectedId)
        }))
        onSelect(null)
        onSelectLink(null)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [selectedId, onUpdateLayout, onSelect, onSelectLink])

  const wheelZoomEnabled =
    settings.zoomInput === 'wheel' || settings.zoomInput === 'both'
  const keyZoomEnabled = settings.zoomInput === 'keys' || settings.zoomInput === 'both'

  useEffect(() => {
    if (!keyZoomEnabled) return
    const onKey = (e: KeyboardEvent) => {
      if (isTypingContext(e.target)) return
      const zoomIn = e.key === '+' || e.key === '=' || e.code === 'NumpadAdd'
      const zoomOut = e.key === '-' || e.key === '_' || e.code === 'NumpadSubtract'
      if (zoomIn) {
        e.preventDefault()
        zoomAtViewportCenter(1.1)
      } else if (zoomOut) {
        e.preventDefault()
        zoomAtViewportCenter(0.9)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [keyZoomEnabled, zoomAtViewportCenter])

  const onWheel = (e: React.WheelEvent) => {
    if (!wheelZoomEnabled) return
    e.preventDefault()
    const rect = viewportRef.current?.getBoundingClientRect()
    if (!rect) return
    const sx = e.clientX - rect.left
    const sy = e.clientY - rect.top
    const factor = e.deltaY > 0 ? 0.9 : 1.1
    zoomAtScreenPoint(sx, sy, factor)
  }

  const startPan = (e: React.PointerEvent) => {
    const onEmpty = !(e.target as HTMLElement).closest('.box')
    const middleOrRight = e.button === 1 || e.button === 2
    const wantPan =
      middleOrRight || (e.button === 0 && (spaceHeld || e.altKey))
    if (wantPan && (onEmpty || middleOrRight)) {
      e.preventDefault()
      suppressBoxContextMenu.current = false
      setPanning(true)
      panStart.current = { x: e.clientX, y: e.clientY, panX: view.panX, panY: view.panY }
      viewportRef.current?.setPointerCapture(e.pointerId)
      return
    }
    if (e.button === 0 && onEmpty) {
      onSelect(null)
      onSelectLink(null)
    }
  }

  const onPointerMove = (e: React.PointerEvent) => {
    const rect = viewportRef.current?.getBoundingClientRect()
    if (!rect) return

    if (panning) {
      const dx = e.clientX - panStart.current.x
      const dy = e.clientY - panStart.current.y
      if (Math.abs(dx) + Math.abs(dy) > 4) {
        suppressBoxContextMenu.current = true
      }
      setView({
        panX: panStart.current.panX + dx,
        panY: panStart.current.panY + dy
      })
      return
    }

    const { gx, gy } = screenToGrid(e.clientX - rect.left, e.clientY - rect.top, view, cellSize)

    if (dragBox.current) {
      const d = dragBox.current
      const ngx = d.startGx + (gx - d.pointerGx)
      const ngy = d.startGy + (gy - d.pointerGy)
      updateBox(d.id, { gx: ngx, gy: ngy })
    }

    if (resizeBox.current) {
      const r = resizeBox.current
      const box = boxes.find((b) => b.id === r.id)
      if (!box) return
      const ngw = Math.max(1, r.startGw + (gx - r.pointerGx))
      const ngh = Math.max(1, r.startGh + (gy - r.pointerGy))
      updateBox(r.id, { gw: ngw, gh: ngh })
    }
  }

  const endPointer = (e: React.PointerEvent) => {
    setPanning(false)
    dragBox.current = null
    resizeBox.current = null
    try {
      (e.target as HTMLElement).releasePointerCapture(e.pointerId)
    } catch {
      /* ignore */
    }
  }

  const startBoxDrag = (box: GridBox, e: React.PointerEvent) => {
    const rect = viewportRef.current?.getBoundingClientRect()
    if (!rect) return
    const { gx, gy } = screenToGrid(e.clientX - rect.left, e.clientY - rect.top, view, cellSize)
    dragBox.current = {
      id: box.id,
      startGx: box.gx,
      startGy: box.gy,
      pointerGx: gx,
      pointerGy: gy
    }
    ;(e.target as HTMLElement).setPointerCapture(e.pointerId)
  }

  const startResize = (box: GridBox, e: React.PointerEvent) => {
    const rect = viewportRef.current?.getBoundingClientRect()
    if (!rect) return
    const { gx, gy } = screenToGrid(e.clientX - rect.left, e.clientY - rect.top, view, cellSize)
    resizeBox.current = {
      id: box.id,
      startGw: box.gw,
      startGh: box.gh,
      pointerGx: gx,
      pointerGy: gy
    }
    ;(e.target as HTMLElement).setPointerCapture(e.pointerId)
  }

  return (
    <div
      ref={viewportRef}
      className={`viewport ${panning ? 'panning' : ''}`}
      onWheel={onWheel}
      onPointerDown={startPan}
      onPointerMove={onPointerMove}
      onPointerUp={endPointer}
      onPointerCancel={endPointer}
      onContextMenu={(e) => e.preventDefault()}
    >
      {settings.showGrid && (
        <GridCanvas view={view} cellSize={cellSize} gridStroke={gridStroke} />
      )}
      <BoxLinksLayer
        layout={layout}
        selectedLinkId={selectedLinkId}
        onSelectLink={onSelectLink}
        onLinkContextMenu={handleLinkContextMenu}
      />
      <div className="boxes-layer">
        {boxes.map((box) => (
          <FolderBox
            key={box.id}
            box={box}
            cellSize={cellSize}
            view={view}
            settings={settings}
            selected={box.id === selectedId}
            linking={linking}
            linkFrom={box.id === linkFromId}
            onSelect={() => onSelect(box.id)}
            onLinkPick={() => onLinkPick(box.id)}
            onDragStart={(e) => startBoxDrag(box, e)}
            onResizeStart={(e) => startResize(box, e)}
            onDropPaths={(paths) => onMovePaths(box, paths)}
            onOpenBox={() => onOpenBox(box)}
            onContextMenu={(e) => handleBoxContextMenu(box.id, e)}
            shouldSuppressContextMenu={() => suppressBoxContextMenu.current}
          />
        ))}
      </div>
      {quickEdit && (
        <CanvasQuickEdit
          menu={quickEdit}
          box={quickEditBox}
          link={quickEditLink}
          pathMissing={quickEditPathMissing}
          onPatchBox={onPatchBox}
          onPatchLink={onPatchLink}
          onCopyPath={onCopyPath}
          onRelinkFolder={onRelinkFolder}
          onClose={closeQuickEdit}
        />
      )}
    </div>
  )
}
