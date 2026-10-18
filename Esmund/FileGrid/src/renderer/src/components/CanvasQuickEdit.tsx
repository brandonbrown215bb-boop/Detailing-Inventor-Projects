import { useEffect, useRef } from 'react'
import type { BoxLink, GridBox, LinkLineStyle } from '../../../shared/types'
import { LINK_LINE_STYLE_OPTIONS } from '../../../shared/linkLineStyle'

type MenuState =
  | { kind: 'box'; id: string; x: number; y: number }
  | { kind: 'link'; id: string; x: number; y: number }

interface Props {
  menu: MenuState
  box: GridBox | null
  link: BoxLink | null
  onPatchBox: (id: string, patch: Partial<GridBox>) => void
  onPatchLink: (id: string, patch: Partial<BoxLink>) => void
  onClose: () => void
}

export function CanvasQuickEdit({ menu, box, link, onPatchBox, onPatchLink, onClose }: Props) {
  const panelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    const onPointer = (e: PointerEvent) => {
      if (panelRef.current?.contains(e.target as Node)) return
      onClose()
    }
    window.addEventListener('keydown', onKey)
    window.addEventListener('pointerdown', onPointer, true)
    return () => {
      window.removeEventListener('keydown', onKey)
      window.removeEventListener('pointerdown', onPointer, true)
    }
  }, [onClose])

  useEffect(() => {
    const el = panelRef.current
    if (!el) return
    const pad = 8
    const rect = el.getBoundingClientRect()
    let left = menu.x
    let top = menu.y
    if (left + rect.width > window.innerWidth - pad) {
      left = Math.max(pad, window.innerWidth - rect.width - pad)
    }
    if (top + rect.height > window.innerHeight - pad) {
      top = Math.max(pad, window.innerHeight - rect.height - pad)
    }
    el.style.left = `${left}px`
    el.style.top = `${top}px`
  }, [menu.x, menu.y, menu.kind])

  if (menu.kind === 'box' && box) {
    return (
      <div
        ref={panelRef}
        className="canvas-quick-edit"
        style={{ left: menu.x, top: menu.y }}
        onContextMenu={(e) => e.preventDefault()}
        onPointerDown={(e) => e.stopPropagation()}
      >
        <label>
          Title
          <input
            type="text"
            value={box.title}
            maxLength={40}
            onChange={(e) => onPatchBox(box.id, { title: e.target.value })}
          />
        </label>
        <label>
          Color
          <input
            type="color"
            value={box.color}
            onChange={(e) => onPatchBox(box.id, { color: e.target.value })}
          />
        </label>
        <p className="canvas-quick-edit-hint">Ctrl+right-click deletes box</p>
      </div>
    )
  }

  if (menu.kind === 'link' && link) {
    return (
      <div
        ref={panelRef}
        className="canvas-quick-edit"
        style={{ left: menu.x, top: menu.y }}
        onContextMenu={(e) => e.preventDefault()}
        onPointerDown={(e) => e.stopPropagation()}
      >
        <label>
          String color
          <input
            type="color"
            value={link.color}
            onChange={(e) => onPatchLink(link.id, { color: e.target.value })}
          />
        </label>
        <label>
          Width
          <input
            type="range"
            min={1}
            max={8}
            step={1}
            value={link.thickness}
            onChange={(e) => onPatchLink(link.id, { thickness: Number(e.target.value) })}
          />
        </label>
        <label>
          Line type
          <select
            value={link.lineStyle}
            onChange={(e) => onPatchLink(link.id, { lineStyle: e.target.value as LinkLineStyle })}
          >
            {LINK_LINE_STYLE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </label>
        <p className="canvas-quick-edit-hint">Shift+right-click removes string</p>
      </div>
    )
  }

  return null
}
