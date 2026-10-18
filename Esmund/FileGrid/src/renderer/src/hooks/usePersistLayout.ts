import { useCallback, useEffect, useRef } from 'react'
import type { LayoutState } from '../../../shared/types'

const DEBOUNCE_MS = 800

export function usePersistLayout(layout: LayoutState | null, onSaved?: () => void) {
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const layoutRef = useRef(layout)
  const deletedIds = useRef(new Set<string>())
  const saving = useRef(false)

  layoutRef.current = layout

  const cancelPending = useCallback(() => {
    if (timer.current) {
      clearTimeout(timer.current)
      timer.current = null
    }
  }, [])

  const waitForSave = useCallback(async () => {
    while (saving.current) {
      await new Promise((r) => setTimeout(r, 25))
    }
  }, [])

  const markDeleted = useCallback(
    (id: string) => {
      deletedIds.current.add(id)
      cancelPending()
    },
    [cancelPending]
  )

  const flush = useCallback(async () => {
    const L = layoutRef.current
    if (!L || deletedIds.current.has(L.id)) return
    if (saving.current) return
    saving.current = true
    try {
      const id = L.id
      if (deletedIds.current.has(id)) return
      await window.fileGrid.saveLayout(L)
      if (deletedIds.current.has(id)) {
        await window.fileGrid.deleteLayout(id)
        return
      }
      await window.fileGrid.saveSession(id)
      onSaved?.()
    } finally {
      saving.current = false
    }
  }, [onSaved])

  useEffect(() => {
    if (!layout) return
    if (deletedIds.current.has(layout.id)) return
    cancelPending()
    timer.current = setTimeout(() => {
      void flush()
    }, DEBOUNCE_MS)
    return cancelPending
  }, [layout, flush, cancelPending])

  return { flushNow: flush, cancelPending, markDeleted, waitForSave }
}
