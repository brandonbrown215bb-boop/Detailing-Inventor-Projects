import type { LinkLineStyle } from './types'

export const DEFAULT_LINK_LINE_STYLE: LinkLineStyle = 'solid'

export const LINK_LINE_STYLE_OPTIONS: { value: LinkLineStyle; label: string }[] = [
  { value: 'solid', label: 'Solid' },
  { value: 'hidden', label: 'Hidden' },
  { value: 'dashed', label: 'Dashed' },
  { value: 'phantom', label: 'Phantom' },
  { value: 'dotDash', label: 'Dot dash' },
  { value: 'doubleDotDash', label: 'Double dot dash' }
]

const VALID: LinkLineStyle[] = LINK_LINE_STYLE_OPTIONS.map((o) => o.value)

/** Map line styles stored in layout schema v1 to current ids (only when schemaVersion is 1). */
export function migrateLineStyleFromSchemaV1(raw: unknown): LinkLineStyle {
  if (raw === 'dashedLine') return 'dashed'
  if (raw === 'dashed') return 'hidden'
  return normalizeLinkLineStyle(raw)
}

export function normalizeLinkLineStyle(raw: unknown): LinkLineStyle {
  if (raw === 'dashedLine') return 'dashed'
  return VALID.includes(raw as LinkLineStyle) ? (raw as LinkLineStyle) : DEFAULT_LINK_LINE_STYLE
}

/** Phantom / drafting dashed use butt caps so short segments read as tiny lines, not round dots. */
export function linkStrokeLinecap(style: LinkLineStyle): 'round' | 'butt' {
  return style === 'phantom' || style === 'dashed' ? 'butt' : 'round'
}

export function linkStrokeDasharray(style: LinkLineStyle, thickness: number): string | undefined {
  const u = Math.max(1, thickness)
  const gap = u * 2.5
  switch (style) {
    case 'solid':
      return undefined
    case 'hidden':
      return `${u * 5} ${u * 3.5}`
    case 'dashed': {
      const long = u * 11
      const short = u * 3
      return `${long} ${gap} ${short} ${gap}`
    }
    case 'phantom': {
      const long = u * 11
      const short = u * 3
      return `${long} ${gap} ${short} ${gap} ${short} ${gap}`
    }
    case 'dotDash':
      return `${u * 0.95} ${gap} ${u * 6} ${gap}`
    case 'doubleDotDash':
      return `${u * 0.95} ${gap} ${u * 0.95} ${gap} ${u * 6} ${gap}`
    default:
      return undefined
  }
}
