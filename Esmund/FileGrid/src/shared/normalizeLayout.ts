import {
  DEFAULT_LINK_LINE_STYLE,
  migrateLineStyleFromSchemaV1,
  normalizeLinkLineStyle
} from './linkLineStyle'
import type { BoxLink, LayoutState } from './types'
import { LAYOUT_SCHEMA_VERSION } from './types'

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

  return {
    ...layout,
    schemaVersion: LAYOUT_SCHEMA_VERSION,
    links
  }
}
