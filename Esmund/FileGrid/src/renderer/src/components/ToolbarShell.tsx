import type { ReactNode } from 'react'
import type { ToolbarAlign, ToolbarDock } from '../../../shared/types'

const TOOLBAR_ICON_URL = new URL('../../../../build/icon.png', import.meta.url).href

interface Props {
  dock: ToolbarDock
  align: ToolbarAlign
  children: ReactNode
}

export function ToolbarShell({ dock, align, children }: Props) {
  const vertical = dock === 'left' || dock === 'right'

  const brandIsland = vertical && align === 'center'

  const brand = vertical ? (
    <div
      className={`toolbar-brand${brandIsland ? ' toolbar-brand--island' : ''}`}
      aria-hidden
    >
      <img src={TOOLBAR_ICON_URL} alt="" draggable={false} />
    </div>
  ) : null

  const controls = <div className="toolbar-controls">{children}</div>

  let inner: ReactNode
  if (!vertical) {
    inner = controls
  } else if (align === 'center') {
    inner = (
      <>
        {brand}
        {controls}
      </>
    )
  } else if (align === 'start') {
    inner = (
      <>
        {controls}
        {brand}
      </>
    )
  } else {
    inner = (
      <>
        {brand}
        {controls}
      </>
    )
  }

  const className = [
    'toolbar',
    vertical ? 'toolbar--vertical' : 'toolbar--horizontal',
    `toolbar--align-${align}`,
    `toolbar--dock-${dock}`
  ].join(' ')

  return <header className={className}>{inner}</header>
}
