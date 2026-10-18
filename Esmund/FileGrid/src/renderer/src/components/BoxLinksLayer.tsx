import type { GridBox, LayoutState } from '../../../shared/types'
import { rectBorderPoint, type ScreenRect } from '../boxLinkGeometry'
import { gridRectToScreen } from '../gridMath'
import { linkStrokeDasharray, linkStrokeLinecap, normalizeLinkLineStyle } from '../../../shared/linkLineStyle'

interface Props {
  layout: LayoutState
  selectedLinkId: string | null
  onSelectLink: (id: string | null) => void
  onLinkContextMenu: (linkId: string, e: React.MouseEvent) => void
}

function boxRect(box: GridBox, layout: LayoutState): ScreenRect {
  return gridRectToScreen(box.gx, box.gy, box.gw, box.gh, layout.view, layout.cellSize)
}

export function BoxLinksLayer({ layout, selectedLinkId, onSelectLink, onLinkContextMenu }: Props) {
  const { links, boxes } = layout
  if (!links.length) return null

  const boxById = new Map(boxes.map((b) => [b.id, b]))

  return (
    <svg className="links-layer" aria-hidden>
      {links.map((link) => {
        const a = boxById.get(link.fromBoxId)
        const b = boxById.get(link.toBoxId)
        if (!a || !b) return null

        const ra = boxRect(a, layout)
        const rb = boxRect(b, layout)
        const acx = ra.left + ra.width / 2
        const acy = ra.top + ra.height / 2
        const bcx = rb.left + rb.width / 2
        const bcy = rb.top + rb.height / 2
        const p1 = rectBorderPoint(ra, bcx, bcy)
        const p2 = rectBorderPoint(rb, acx, acy)
        const hit = Math.max(link.thickness, 10)
        const lineStyle = normalizeLinkLineStyle(link.lineStyle)
        const dash = linkStrokeDasharray(lineStyle, link.thickness)
        const linecap = linkStrokeLinecap(lineStyle)

        return (
          <g key={link.id}>
            <line
              className="link-hit"
              x1={p1.x}
              y1={p1.y}
              x2={p2.x}
              y2={p2.y}
              stroke="transparent"
              strokeWidth={hit}
              style={{ cursor: 'pointer' }}
              onPointerDown={(e) => {
                e.stopPropagation()
                onSelectLink(link.id)
              }}
              onContextMenu={(e) => {
                e.preventDefault()
                e.stopPropagation()
                onLinkContextMenu(link.id, e)
              }}
            />
            <line
              x1={p1.x}
              y1={p1.y}
              x2={p2.x}
              y2={p2.y}
              stroke={link.color}
              strokeWidth={link.thickness}
              strokeLinecap={linecap}
              strokeDasharray={dash}
              className={selectedLinkId === link.id ? 'link-line selected' : 'link-line'}
              style={{ pointerEvents: 'none' }}
            />
          </g>
        )
      })}
    </svg>
  )
}
