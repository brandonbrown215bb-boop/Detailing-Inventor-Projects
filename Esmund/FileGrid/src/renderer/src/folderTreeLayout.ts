import type { FolderTreeEntry } from '../../shared/types'

export interface TreeBoxPlacement {
  gx: number
  gy: number
  gw: number
  gh: number
}

export interface LaidOutTreeEntry {
  entry: FolderTreeEntry
  placement: TreeBoxPlacement
}

export interface TreeParentChildEdge {
  parentPath: string
  childPath: string
}

export interface SimpleTreeLayoutResult {
  laidOut: LaidOutTreeEntry[]
  edges: TreeParentChildEdge[]
}

const GW = 2
const GH = 2
/** Horizontal gap between sibling subtrees (grid cells). */
const SIBLING_GAP = 1
/** Vertical gap between parent row and children row (grid cells). */
const LEVEL_GAP = 2

interface TreeNode {
  entry: FolderTreeEntry
  children: TreeNode[]
}

function pathKey(p: string): string {
  return p.replace(/\\/g, '/').toLowerCase()
}

export function parentFolderPath(folderPath: string): string | null {
  const trimmed = folderPath.replace(/[/\\]+$/, '')
  const lastSep = Math.max(trimmed.lastIndexOf('/'), trimmed.lastIndexOf('\\'))
  if (lastSep < 0) return null
  if (lastSep === 2 && /^[A-Za-z]:/.test(trimmed)) return null
  return trimmed.slice(0, lastSep)
}

function buildTree(entries: FolderTreeEntry[]): { root: TreeNode; edges: TreeParentChildEdge[] } | null {
  const rootEntry = entries.find((e) => e.depth === 0)
  if (!rootEntry) return null

  const byKey = new Map<string, FolderTreeEntry>()
  for (const e of entries) byKey.set(pathKey(e.path), e)

  const childMap = new Map<string, FolderTreeEntry[]>()
  const edges: TreeParentChildEdge[] = []

  for (const e of entries) {
    if (e.depth === 0) continue
    const parentPath = parentFolderPath(e.path)
    if (!parentPath) continue
    const parent = byKey.get(pathKey(parentPath))
    if (!parent) continue
    edges.push({ parentPath: parent.path, childPath: e.path })
    const list = childMap.get(parent.path) ?? []
    list.push(e)
    childMap.set(parent.path, list)
  }

  const makeNode = (entry: FolderTreeEntry): TreeNode => {
    const kids = (childMap.get(entry.path) ?? []).sort((a, b) =>
      a.path.localeCompare(b.path, undefined, { sensitivity: 'base' })
    )
    return { entry, children: kids.map(makeNode) }
  }

  return { root: makeNode(rootEntry), edges }
}

interface PlaceCtx {
  originGx: number
  originGy: number
  laidOut: LaidOutTreeEntry[]
}

/** Children sit in a row below the parent; parent is centered over its subtree. */
function placeSubtree(node: TreeNode, depth: number, leftGx: number, ctx: PlaceCtx): number {
  const y = ctx.originGy + depth * (GH + LEVEL_GAP)
  const unit = GW + SIBLING_GAP

  if (node.children.length === 0) {
    const gx = leftGx
    ctx.laidOut.push({
      entry: node.entry,
      placement: { gx, gy: y, gw: GW, gh: GH }
    })
    return leftGx + unit
  }

  let cursor = leftGx
  const centers: number[] = []
  for (const child of node.children) {
    const before = cursor
    cursor = placeSubtree(child, depth + 1, cursor, ctx)
    const childRight = cursor - SIBLING_GAP
    centers.push((before + childRight - GW) / 2 + GW / 2)
  }

  const center =
    centers.length === 1
      ? centers[0]
      : (centers[0] + centers[centers.length - 1]) / 2
  const gx = Math.round(center - GW / 2)

  ctx.laidOut.push({
    entry: node.entry,
    placement: { gx, gy: y, gw: GW, gh: GH }
  })

  return cursor
}

export function layoutSimpleTree(
  entries: FolderTreeEntry[],
  originGx: number,
  originGy: number
): SimpleTreeLayoutResult {
  const built = buildTree(entries)
  if (!built) return { laidOut: [], edges: [] }

  const ctx: PlaceCtx = { originGx, originGy, laidOut: [] }
  placeSubtree(built.root, 0, originGx, ctx)
  return { laidOut: ctx.laidOut, edges: built.edges }
}

export function treeExtent(laidOut: LaidOutTreeEntry[]): { width: number; height: number } {
  if (!laidOut.length) return { width: 0, height: 0 }
  let minGx = Infinity
  let maxGx = 0
  let minGy = Infinity
  let maxGy = 0
  for (const { placement: p } of laidOut) {
    minGx = Math.min(minGx, p.gx)
    maxGx = Math.max(maxGx, p.gx + p.gw)
    minGy = Math.min(minGy, p.gy)
    maxGy = Math.max(maxGy, p.gy + p.gh)
  }
  return { width: maxGx - minGx, height: maxGy - minGy }
}
