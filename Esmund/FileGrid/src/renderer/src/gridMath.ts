export interface ViewTransform {
  panX: number
  panY: number
  zoom: number
}

export function gridStep(cellSize: number, zoom: number): number {
  return cellSize * zoom
}

export function screenToGrid(
  sx: number,
  sy: number,
  view: ViewTransform,
  cellSize: number
): { gx: number; gy: number } {
  const step = gridStep(cellSize, view.zoom)
  return {
    gx: Math.round((sx - view.panX) / step),
    gy: Math.round((sy - view.panY) / step)
  }
}

export function gridRectToScreen(
  gx: number,
  gy: number,
  gw: number,
  gh: number,
  view: ViewTransform,
  cellSize: number
): { left: number; top: number; width: number; height: number } {
  const step = gridStep(cellSize, view.zoom)
  return {
    left: Math.round(view.panX + gx * step),
    top: Math.round(view.panY + gy * step),
    width: Math.round(gw * step),
    height: Math.round(gh * step)
  }
}
