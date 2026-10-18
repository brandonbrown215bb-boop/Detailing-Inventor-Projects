export interface ScreenRect {
  left: number
  top: number
  width: number
  height: number
}

/** Point on rect border from center toward (towardX, towardY). */
export function rectBorderPoint(rect: ScreenRect, towardX: number, towardY: number): { x: number; y: number } {
  const cx = rect.left + rect.width / 2
  const cy = rect.top + rect.height / 2
  const dx = towardX - cx
  const dy = towardY - cy
  if (dx === 0 && dy === 0) return { x: cx, y: cy }
  const hw = rect.width / 2
  const hh = rect.height / 2
  const scale = Math.max(Math.abs(dx) / hw, Math.abs(dy) / hh)
  return { x: cx + dx / scale, y: cy + dy / scale }
}
