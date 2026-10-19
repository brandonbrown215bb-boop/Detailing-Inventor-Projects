import {
  DEFAULT_LINK_LINE_STYLE,
  migrateLineStyleFromSchemaV1,
  normalizeLinkLineStyle
} from './linkLineStyle'
import type { BoxKind, BoxLink, GridBox, LayoutState } from './types'
import { LAYOUT_SCHEMA_VERSION } from './types'

function normalizeBox(box: GridBox): GridBox {
  const kind: BoxKind = box.kind === 'file' ? 'file' : 'folder'
  return { ...box, kind }
}

function normalizeLink(link: BoxLink): BoxLink {
  return {
    ...link,
    lineStyle: normalizeLinkLineStyle(link.lineStyle)
  }
}

function migrateLinksForSchemaV2(links: BoxLink[]): BoxLink[] {
  return links.map((l) =>
    normalizeLink({ ...l, lineStyle: migrateLineStyleFromSchemaV1(l.lineStyle) })
  )
}

export function normalizeLayout(layout: LayoutState): LayoutState {
  const schemaVersion = layout.schemaVersion ?? 1
  let links = layout.links ?? []

  if (schemaVersion < 2) {
    links = migrateLinksForSchemaV2(links)
  } else {
    links = links.map((l) => normalizeLink({ lineStyle: DEFAULT_LINK_LINE_STYLE, ...l }))
  }

  const boxes = (layout.boxes ?? []).map((b) => normalizeBox({ kind: 'folder', ...b }))

  return {
    ...layout,
    schemaVersion: LAYOUT_SCHEMA_VERSION,
    links,
    boxes
  }
}
