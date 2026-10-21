import { useEffect, useRef } from 'react'
import type { ViewTransform } from '../gridMath'
import { gridStep } from '../gridMath'

interface Props {
  view: ViewTransform
  cellSize: number
  gridStroke: string
}

export function GridCanvas({ view, cellSize, gridStroke }: Props) {
  const canvasRef = useRef<HTMLCanvasElement>(null)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return

    const draw = () => {
      const parent = canvas.parentElement
      if (!parent) return

      const dpr = window.devicePixelRatio || 1
      const w = parent.clientWidth
      const h = parent.clientHeight
      if (w === 0 || h === 0) return

      canvas.width = Math.round(w * dpr)
      canvas.height = Math.round(h * dpr)
      canvas.style.width = `${w}px`
      canvas.style.height = `${h}px`

      const ctx = canvas.getContext('2d')
      if (!ctx) return

      ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
      ctx.clearRect(0, 0, w, h)

      const step = gridStep(cellSize, view.zoom)
      if (step < 4) return

      const { panX, panY } = view
      const firstGx = Math.floor(-panX / step) - 1
      const lastGx = Math.ceil((w - panX) / step) + 1
      const firstGy = Math.floor(-panY / step) - 1
      const lastGy = Math.ceil((h - panY) / step) + 1

      ctx.strokeStyle = gridStroke
      ctx.lineWidth = 1
      ctx.beginPath()

      for (let gx = firstGx; gx <= lastGx; gx++) {
        const x = Math.round(panX + gx * step) + 0.5
        ctx.moveTo(x, 0)
        ctx.lineTo(x, h)
      }
      for (let gy = firstGy; gy <= lastGy; gy++) {
        const y = Math.round(panY + gy * step) + 0.5
        ctx.moveTo(0, y)
        ctx.lineTo(w, y)
      }
      ctx.stroke()
    }

    draw()
    const ro = new ResizeObserver(draw)
    ro.observe(canvas.parentElement!)
    return () => ro.disconnect()
  }, [view.panX, view.panY, view.zoom, cellSize, gridStroke])

  return <canvas ref={canvasRef} className="grid-canvas" aria-hidden />
}
