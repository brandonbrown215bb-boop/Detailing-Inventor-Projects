import { useCallback, useEffect, useMemo, useState } from 'react'
import { v4 as uuid } from 'uuid'
import type { AppSettings, BoxLink, GridBox, LayoutState, MoveConflictPayload } from '../../shared/types'
import { DEFAULT_APP_SETTINGS, DEFAULT_HOME_VIEW } from '../../shared/types'
import { boxPathExists } from './boxPathUtils'
import { ConflictModal } from './components/ConflictModal'
import { ConfirmModal } from './components/ConfirmModal'
import { FileDropButton } from './components/FileDropButton'
import { FolderDropButton } from './components/FolderDropButton'
import { GridViewport } from './components/GridViewport'
import { NamePromptModal } from './components/NamePromptModal'
import { SettingsPanel } from './components/SettingsPanel'
import { RelinkFolderModal } from './components/RelinkFolderModal'
import { ToolbarShell } from './components/ToolbarShell'
import { copyTextToClipboard } from './clipboard'
import { hexToRgba } from './colorUtils'
import { usePersistLayout } from './hooks/usePersistLayout'
import { DEFAULT_LINK_LINE_STYLE, LINK_LINE_STYLE_OPTIONS } from '../../shared/linkLineStyle'
import { isTypingContext } from './keyboard'
import { layoutSimpleTree, treeExtent } from './folderTreeLayout'
import { normalizeLayout } from './layoutUtils'

const BOX_COLORS = ['#3d5a80', '#5c4d7d', '#4a6741', '#8b4513', '#2f6f6f', '#6b3a3a', '#555555']
const DEFAULT_LINK_COLOR = '#b8b8b8'
const DEFAULT_LINK_THICKNESS = 2

function basename(p: string): string {
  const parts = p.replace(/[/\\]+$/, '').split(/[/\\]/)
  return parts[parts.length - 1] || p
}

export default function App() {
  const [layout, setLayout] = useState<LayoutState | null>(null)
  const [layouts, setLayouts] = useState<LayoutState[]>([])
  const [settings, setSettings] = useState<AppSettings>(DEFAULT_APP_SETTINGS)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [toast, setToast] = useState<string | null>(null)
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [newLayoutOpen, setNewLayoutOpen] = useState(false)
  const [newLayoutDefaultName, setNewLayoutDefaultName] = useState('Job 1')
  const [conflict, setConflict] = useState<MoveConflictPayload | null>(null)
  const [deleteLayoutOpen, setDeleteLayoutOpen] = useState(false)
  const [linking, setLinking] = useState(false)
  const [linkFromId, setLinkFromId] = useState<string | null>(null)
  const [selectedLinkId, setSelectedLinkId] = useState<string | null>(null)
  const [relinkModal, setRelinkModal] = useState<{
    boxId: string
    retryMovePaths?: string[]
  } | null>(null)
  const [selectedFolderMissing, setSelectedFolderMissing] = useState(false)

  const showToast = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(null), 4000)
  }

  const { cancelPending, markDeleted, waitForSave } = usePersistLayout(layout)

  const gridStroke = useMemo(
    () => hexToRgba(settings.gridColor, settings.gridOpacity),
    [settings.gridColor, settings.gridOpacity]
  )

  const refreshLayouts = useCallback(async () => {
    const list = await window.fileGrid.listLayouts()
    setLayouts(list)
    return list
  }, [])

  useEffect(() => {
    return window.fileGrid.onMoveConflict((payload) => setConflict(payload))
  }, [])

  useEffect(() => {
    void (async () => {
      const loadedSettings = await window.fileGrid.loadSettings()
      setSettings(loadedSettings)

      let list = await refreshLayouts()
      const session = await window.fileGrid.loadSession()
      let active: LayoutState | null = null
      if (session.activeLayoutId) {
        active = await window.fileGrid.loadLayout(session.activeLayoutId)
      }
      if (!active && list.length > 0) {
        active = list[0]
      }
      if (!active) {
        active = await window.fileGrid.createLayout('Default')
        list = await refreshLayouts()
      }
      setLayout(normalizeLayout(active))
      await window.fileGrid.saveSession(active.id)
    })()
  }, [refreshLayouts])

  const updateLayout = useCallback(
    (patch: Partial<LayoutState> | ((prev: LayoutState) => LayoutState)) => {
      setLayout((prev) => {
        if (!prev) return prev
        const next = typeof patch === 'function' ? patch(prev) : { ...prev, ...patch }
        return normalizeLayout(next)
      })
    },
    []
  )

  const persistSettings = async (next: AppSettings) => {
    setSettings(next)
    await window.fileGrid.saveSettings(next)
  }

  const switchLayout = async (id: string) => {
    const loaded = await window.fileGrid.loadLayout(id)
    if (loaded) {
      setLayout(normalizeLayout(loaded))
      setSelectedId(null)
      setSelectedLinkId(null)
      setLinkFromId(null)
      await window.fileGrid.saveSession(id)
    }
  }

  const openNewLayoutPrompt = () => {
    setNewLayoutDefaultName(`Job ${layouts.length + 1}`)
    setNewLayoutOpen(true)
  }

  const confirmNewLayout = async (name: string) => {
    const created = await window.fileGrid.createLayout(name)
    await refreshLayouts()
    setLayout(normalizeLayout(created))
    setSelectedId(null)
    setSelectedLinkId(null)
    await window.fileGrid.saveSession(created.id)
    setNewLayoutOpen(false)
    showToast(`Created layout "${name}"`)
  }

  const duplicateLayoutByName = async (name: string) => {
    if (!layout) return
    const copy: LayoutState = {
      ...layout,
      id: uuid(),
      name,
      updatedAt: new Date().toISOString()
    }
    await window.fileGrid.saveLayout(copy)
    await refreshLayouts()
    setLayout(copy)
    await window.fileGrid.saveSession(copy.id)
    showToast(`Duplicated as "${name}"`)
  }

  const renameLayout = async (name: string) => {
    if (!layout) return
    const updated = normalizeLayout({ ...layout, name })
    setLayout(updated)
    setLayouts((prev) => prev.map((l) => (l.id === layout.id ? { ...l, name } : l)))
    await window.fileGrid.saveLayout(updated)
    showToast('Layout renamed')
  }

  const performDeleteLayout = async () => {
    if (!layout) return
    const deletedId = layout.id
    markDeleted(deletedId)
    cancelPending()
    await waitForSave()
    await window.fileGrid.deleteLayout(deletedId)
    await window.fileGrid.deleteLayout(deletedId)

    let list = (await refreshLayouts()).filter((l) => l.id !== deletedId)
    let next: LayoutState
    if (list.length === 0) {
      next = normalizeLayout(await window.fileGrid.createLayout('Default'))
      await refreshLayouts()
    } else {
      const loaded = await window.fileGrid.loadLayout(list[0].id)
      next = normalizeLayout(loaded ?? list[0])
    }
    setLayout(next)
    await window.fileGrid.saveSession(next.id)
    setSelectedId(null)
    setSelectedLinkId(null)
    setLinkFromId(null)
    setDeleteLayoutOpen(false)
    setSettingsOpen(false)
    showToast('Layout deleted')
  }

  const addBoxesFromPaths = (folders: string[]) => {
    if (!layout || folders.length === 0) return
    const start = layout.boxes.length
    const newBoxes: GridBox[] = folders.map((folder, i) => ({
      id: uuid(),
      kind: 'folder',
      title: basename(folder),
      color: BOX_COLORS[(start + i) % BOX_COLORS.length],
      folderPath: folder,
      gx: 2 + (i % 4) * 2,
      gy: 2 + Math.floor(i / 4) * 2,
      gw: 2,
      gh: 2
    }))
    updateLayout((prev) => ({ ...prev, boxes: [...prev.boxes, ...newBoxes] }))
    setSelectedId(newBoxes[newBoxes.length - 1].id)
    showToast(`Added ${newBoxes.length} folder box(es)`)
  }

  const addTreesFromPaths = async (roots: string[]) => {
    if (!layout || roots.length === 0) return
    let originGx = 2
    let originGy = 2
    const start = layout.boxes.length
    const newBoxes: GridBox[] = []
    const newLinks: BoxLink[] = []
    let capped = false

    for (const root of roots) {
      if (await window.fileGrid.folderDropBlocked(root)) {
        showToast('Cannot build a tree from that location')
        continue
      }
      const entries = await window.fileGrid.scanFolderTree(
        root,
        settings.folderTreeMaxDepth,
        settings.folderTreeExcludeNames,
        settings.folderTreeMaxBoxes
      )
      if (!entries.length) {
        showToast('No folders found under that path')
        continue
      }
      if (entries.length >= settings.folderTreeMaxBoxes) capped = true

      const { laidOut, edges } = layoutSimpleTree(entries, originGx, originGy)
      const pathToId = new Map<string, string>()
      for (const { entry, placement } of laidOut) {
        const id = uuid()
        pathToId.set(entry.path, id)
        newBoxes.push({
          id,
          kind: 'folder',
          title: basename(entry.path),
          color: BOX_COLORS[(start + newBoxes.length) % BOX_COLORS.length],
          folderPath: entry.path,
          ...placement
        })
      }
      for (const { parentPath, childPath } of edges) {
        const fromBoxId = pathToId.get(parentPath)
        const toBoxId = pathToId.get(childPath)
        if (!fromBoxId || !toBoxId) continue
        newLinks.push({
          id: uuid(),
          fromBoxId,
          toBoxId,
          color: DEFAULT_LINK_COLOR,
          thickness: DEFAULT_LINK_THICKNESS,
          lineStyle: DEFAULT_LINK_LINE_STYLE
        })
      }
      const extent = treeExtent(laidOut)
      originGy += extent.height + 3
    }

    if (!newBoxes.length) return
    updateLayout((prev) => ({
      ...prev,
      boxes: [...prev.boxes, ...newBoxes],
      links: [...prev.links, ...newLinks]
    }))
    setSelectedId(newBoxes[newBoxes.length - 1].id)
    const linkNote = newLinks.length ? `, ${newLinks.length} strings` : ''
    if (capped) {
      showToast(`Added ${newBoxes.length} boxes${linkNote} (scan cap ${settings.folderTreeMaxBoxes})`)
    } else {
      showToast(`Added ${newBoxes.length} boxes from tree${linkNote}`)
    }
  }

  const handleFoldersDropped = (folders: string[]) => {
    if (settings.folderDropMode === 'tree') {
      void addTreesFromPaths(folders)
      return
    }
    addBoxesFromPaths(folders)
  }

  const toggleFolderDropMode = () => {
    const next = settings.folderDropMode === 'boxes' ? 'tree' : 'boxes'
    void persistSettings({ ...settings, folderDropMode: next })
    showToast(next === 'tree' ? 'Drop Tree mode' : 'Drop Folder mode')
  }

  const addBox = async () => {
    const folder = await window.fileGrid.pickFolder()
    if (!folder) return
    addBoxesFromPaths([folder])
  }

  const addFilesFromPaths = (files: string[]) => {
    if (!layout || files.length === 0) return
    const start = layout.boxes.length
    const newBoxes: GridBox[] = files.map((filePath, i) => ({
      id: uuid(),
      kind: 'file',
      title: basename(filePath),
      color: BOX_COLORS[(start + i) % BOX_COLORS.length],
      folderPath: filePath,
      gx: 2 + (i % 4) * 2,
      gy: 2 + Math.floor(i / 4) * 2,
      gw: 2,
      gh: 2
    }))
    updateLayout((prev) => ({ ...prev, boxes: [...prev.boxes, ...newBoxes] }))
    setSelectedId(newBoxes[newBoxes.length - 1].id)
    showToast(`Added ${newBoxes.length} file box(es)`)
  }

  const addFileBox = async () => {
    const filePath = await window.fileGrid.pickFile()
    if (!filePath) return
    addFilesFromPaths([filePath])
  }

  const handleLinkPick = (boxId: string) => {
    if (!linkFromId) {
      setLinkFromId(boxId)
      setSelectedId(boxId)
      return
    }
    if (linkFromId === boxId) {
      setLinkFromId(null)
      return
    }
    const exists = layout?.links.some(
      (l) =>
        (l.fromBoxId === linkFromId && l.toBoxId === boxId) ||
        (l.fromBoxId === boxId && l.toBoxId === linkFromId)
    )
    if (!exists) {
      const link: BoxLink = {
        id: uuid(),
        fromBoxId: linkFromId,
        toBoxId: boxId,
        color: DEFAULT_LINK_COLOR,
        thickness: DEFAULT_LINK_THICKNESS,
        lineStyle: DEFAULT_LINK_LINE_STYLE
      }
      updateLayout((prev) => ({ ...prev, links: [...prev.links, link] }))
      setSelectedLinkId(link.id)
    }
    setLinkFromId(null)
  }

  const selected = layout?.boxes.find((b) => b.id === selectedId) ?? null
  const selectedLink = layout?.links.find((l) => l.id === selectedLinkId) ?? null
  const relinkTarget = relinkModal
    ? layout?.boxes.find((b) => b.id === relinkModal.boxId) ?? null
    : null

  useEffect(() => {
    if (!selected) {
      setSelectedFolderMissing(false)
      return
    }
    void boxPathExists(selected).then((ok) => {
      setSelectedFolderMissing(!ok)
    })
  }, [selected?.id, selected?.folderPath, selected?.kind])

  const patchLink = (linkId: string, patch: Partial<BoxLink>) => {
    updateLayout((prev) => ({
      ...prev,
      links: prev.links.map((l) => (l.id === linkId ? { ...l, ...patch } : l))
    }))
  }

  const patchSelectedLink = (patch: Partial<BoxLink>) => {
    if (!selectedLinkId) return
    patchLink(selectedLinkId, patch)
  }

  const removeLink = useCallback(
    (linkId: string) => {
      updateLayout((prev) => ({
        ...prev,
        links: prev.links.filter((l) => l.id !== linkId)
      }))
      setSelectedLinkId((cur) => (cur === linkId ? null : cur))
      showToast('String removed')
    },
    [updateLayout]
  )

  const removeSelectedLink = useCallback(() => {
    if (!selectedLinkId) return
    removeLink(selectedLinkId)
  }, [selectedLinkId, removeLink])

  const adjustBoxDrawOrder = useCallback(
    (mode: 'nudge' | 'front' | 'back', direction?: 1 | -1) => {
      if (!selectedId) return
      let toastMsg: string | null = null
      updateLayout((prev) => {
        const idx = prev.boxes.findIndex((b) => b.id === selectedId)
        if (idx < 0) return prev
        if (mode === 'front' && idx === prev.boxes.length - 1) return prev
        if (mode === 'back' && idx === 0) return prev
        const boxes = [...prev.boxes]
        const [item] = boxes.splice(idx, 1)
        if (mode === 'front') {
          boxes.push(item)
          toastMsg = 'To front'
        } else if (mode === 'back') {
          boxes.unshift(item)
          toastMsg = 'To back'
        } else {
          const target = idx + (direction ?? 1)
          if (target < 0 || target >= prev.boxes.length) return prev
          boxes.splice(target, 0, item)
          toastMsg = (direction ?? 1) > 0 ? 'Forward' : 'Back'
        }
        return { ...prev, boxes }
      })
      if (toastMsg) showToast(toastMsg)
    },
    [selectedId, updateLayout]
  )

  const goHomeView = () => {
    updateLayout((prev) => ({ ...prev, view: { ...DEFAULT_HOME_VIEW } }))
    showToast('Home view')
  }

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && linking) {
        setLinking(false)
        setLinkFromId(null)
      }
      if ((e.key === 'Delete' || e.key === 'Backspace') && selectedLinkId) {
        const t = e.target as HTMLElement
        if (t.closest('input, textarea, select, .modal')) return
        e.preventDefault()
        removeSelectedLink()
      }
      if (selectedId && (e.key === 'ArrowUp' || e.key === 'ArrowDown')) {
        if (isTypingContext(e.target)) return
        e.preventDefault()
        if (e.ctrlKey) {
          adjustBoxDrawOrder(e.key === 'ArrowUp' ? 'front' : 'back')
        } else {
          adjustBoxDrawOrder('nudge', e.key === 'ArrowUp' ? 1 : -1)
        }
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [linking, selectedLinkId, removeSelectedLink, selectedId, adjustBoxDrawOrder])

  const patchBox = (boxId: string, patch: Partial<GridBox>) => {
    updateLayout((prev) => ({
      ...prev,
      boxes: prev.boxes.map((b) => (b.id === boxId ? { ...b, ...patch } : b))
    }))
  }

  const patchSelected = (patch: Partial<GridBox>) => {
    if (!selectedId) return
    patchBox(selectedId, patch)
  }

  const removeBox = useCallback(
    (boxId: string) => {
      updateLayout((prev) => ({
        ...prev,
        boxes: prev.boxes.filter((b) => b.id !== boxId),
        links: prev.links.filter((l) => l.fromBoxId !== boxId && l.toBoxId !== boxId)
      }))
      setSelectedId((cur) => (cur === boxId ? null : cur))
      showToast('Box removed')
    },
    [updateLayout]
  )

  const copyFolderPath = async (path: string) => {
    const ok = await copyTextToClipboard(path)
    showToast(ok ? 'Path copied' : 'Could not copy path')
  }

  const applyFolderRelink = (boxId: string, newPath: string) => {
    const box = layout?.boxes.find((b) => b.id === boxId)
    patchBox(boxId, {
      folderPath: newPath,
      title: box?.title ? box.title : basename(newPath)
    })
    showToast(box?.kind === 'file' ? 'File linked' : 'Folder linked')
  }

  const promptRelinkFolder = async (boxId: string) => {
    const box = layout?.boxes.find((b) => b.id === boxId)
    if (!box) return
    if (box.kind === 'file') {
      const filePath = await window.fileGrid.pickFile()
      if (!filePath) return
      if (!(await window.fileGrid.pathIsFile(filePath))) {
        showToast('Not a valid file')
        return
      }
      applyFolderRelink(boxId, filePath)
      return
    }
    const folder = await window.fileGrid.pickFolder()
    if (!folder) return
    if (!(await window.fileGrid.pathIsDirectory(folder))) {
      showToast('Not a valid folder')
      return
    }
    applyFolderRelink(boxId, folder)
  }

  const openBoxTarget = async (box: GridBox) => {
    if (!(await boxPathExists(box))) {
      setRelinkModal({ boxId: box.id })
      return
    }
    await window.fileGrid.openFolder(box.folderPath)
  }

  const runMovePaths = async (box: GridBox, paths: string[]) => {
    if (box.kind === 'file') return
    if (!(await window.fileGrid.pathIsDirectory(box.folderPath))) {
      setRelinkModal({ boxId: box.id, retryMovePaths: paths })
      return
    }
    const results = await window.fileGrid.movePaths(box.folderPath, paths)
    if (results.some((r) => r.aborted)) {
      const done = results.filter((r) => r.ok && !r.skipped).length
      showToast(done ? `Cancelled — ${done} item(s) already moved` : 'Move cancelled')
      return
    }
    const failed = results.filter((r) => !r.ok && !r.aborted)
    const skipped = results.filter((r) => r.ok && r.skipped)
    const moved = results.filter((r) => r.ok && !r.skipped)
    const renamed = moved.filter((r) => r.renamed)

    if (failed.length > 0) {
      showToast(`Failed ${failed.length}: ${failed.map((f) => f.error).join('; ')}`)
      return
    }
    const parts: string[] = []
    if (moved.length) parts.push(`Moved ${moved.length}`)
    if (renamed.length) parts.push(`${renamed.length} renamed (kept both)`)
    if (skipped.length) parts.push(`${skipped.length} skipped`)
    showToast(`${parts.join(', ')} → ${box.title}`)
  }

  const onMovePaths = (box: GridBox, paths: string[]) => void runMovePaths(box, paths)

  const confirmRelink = (newPath: string) => {
    if (!relinkModal) return
    const { boxId, retryMovePaths } = relinkModal
    applyFolderRelink(boxId, newPath)
    setRelinkModal(null)
    if (retryMovePaths?.length) {
      const box = layout?.boxes.find((b) => b.id === boxId)
      if (box) {
        void runMovePaths({ ...box, folderPath: newPath }, retryMovePaths)
      }
    }
  }

  if (!layout) {
    return <div className="app-shell" style={{ padding: 24 }}>Loading…</div>
  }

  const dock = settings.toolbarDock
  const toolbarFirst = dock === 'top' || dock === 'left'

  const shellStyle = {
    backgroundColor: settings.backgroundColor,
    color: settings.fontColor,
    fontFamily: settings.fontFamily
  }

  const toolbar = (
      <ToolbarShell dock={dock} align={settings.toolbarAlign}>
        <label>
          Layout
          <select
            value={layout.id}
            onChange={(e) => void switchLayout(e.target.value)}
          >
            {layouts.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </select>
        </label>
        <button type="button" onClick={openNewLayoutPrompt}>New</button>
        <button type="button" onClick={() => setDeleteLayoutOpen(true)}>Delete</button>
        <button type="button" className="primary" onClick={() => void addBox()}>+ Folder</button>
        <button type="button" onClick={() => void addFileBox()}>+ File</button>
        <FileDropButton onFilesDropped={addFilesFromPaths} />
        <FolderDropButton
          mode={settings.folderDropMode}
          onModeToggle={toggleFolderDropMode}
          onFoldersDropped={handleFoldersDropped}
        />
        <button type="button" onClick={goHomeView} title="Reset pan and zoom">
          Home
        </button>
        <button
          type="button"
          className={linking ? 'primary' : ''}
          onClick={() => {
            setLinking((v) => !v)
            setLinkFromId(null)
            setSelectedLinkId(null)
          }}
        >
          {linking ? 'Connecting…' : 'Connect'}
        </button>
        <button type="button" onClick={() => setSettingsOpen(true)}>Settings</button>
        {selected && (
          <>
            <label>
              Title
              <input
                type="text"
                value={selected.title}
                maxLength={40}
                onChange={(e) => patchSelected({ title: e.target.value })}
              />
            </label>
            <label>
              Color
              <input
                type="color"
                value={selected.color}
                onChange={(e) => patchSelected({ color: e.target.value })}
              />
            </label>
            <button type="button" onClick={() => void openBoxTarget(selected)}>
              {selected.kind === 'file' ? 'Open file' : 'Open folder'}
            </button>
            <button type="button" onClick={() => adjustBoxDrawOrder('front')} title="Ctrl+↑">
              To front
            </button>
            <button type="button" onClick={() => adjustBoxDrawOrder('back')} title="Ctrl+↓">
              To back
            </button>
            <button type="button" onClick={() => void copyFolderPath(selected.folderPath)}>
              Copy path
            </button>
            <button type="button" onClick={() => void promptRelinkFolder(selected.id)}>
              {selectedFolderMissing
                ? 'Relink…'
                : selected.kind === 'file'
                  ? 'Change file…'
                  : 'Change folder…'}
            </button>
            <button
              type="button"
              onClick={() => {
                updateLayout((prev) => ({
                  ...prev,
                  boxes: prev.boxes.filter((b) => b.id !== selectedId),
                  links: prev.links.filter((l) => l.fromBoxId !== selectedId && l.toBoxId !== selectedId)
                }))
                setSelectedId(null)
              }}
            >
              Remove box
            </button>
          </>
        )}
        {selectedLink && (
          <>
            <label>
              String color
              <input
                type="color"
                value={selectedLink.color}
                onChange={(e) => patchSelectedLink({ color: e.target.value })}
              />
            </label>
            <label>
              String width
              <input
                type="range"
                min={1}
                max={8}
                step={1}
                value={selectedLink.thickness}
                onChange={(e) => patchSelectedLink({ thickness: Number(e.target.value) })}
              />
            </label>
            <label>
              Line type
              <select
                value={selectedLink.lineStyle}
                onChange={(e) =>
                  patchSelectedLink({ lineStyle: e.target.value as BoxLink['lineStyle'] })
                }
              >
                {LINK_LINE_STYLE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </label>
            <button type="button" onClick={removeSelectedLink}>Remove string</button>
          </>
        )}
      </ToolbarShell>
  )

  const viewport = (
      <GridViewport
        layout={layout}
        settings={settings}
        gridStroke={gridStroke}
        selectedId={selectedId}
        selectedLinkId={selectedLinkId}
        linking={linking}
        linkFromId={linkFromId}
        onSelect={(id) => {
          setSelectedId(id)
          if (id) setSelectedLinkId(null)
        }}
        onSelectLink={(id) => {
          setSelectedLinkId(id)
          if (id) setSelectedId(null)
        }}
        onLinkPick={handleLinkPick}
        onUpdateLayout={updateLayout}
        onMovePaths={onMovePaths}
        onPatchBox={patchBox}
        onRemoveBox={removeBox}
        onPatchLink={patchLink}
        onRemoveLink={removeLink}
        onCopyPath={(path) => void copyFolderPath(path)}
        onRelinkFolder={(boxId) => void promptRelinkFolder(boxId)}
        onOpenBox={(box) => void openBoxTarget(box)}
      />
  )

  return (
    <div className={`app-shell app-shell--dock-${dock}`} style={shellStyle}>
      {toolbarFirst ? toolbar : viewport}
      {toolbarFirst ? viewport : toolbar}
      {toast && <div className="toast">{toast}</div>}
      {settingsOpen && (
        <SettingsPanel
          key={layout.id}
          settings={settings}
          layout={layout}
          onSettingsChange={(s) => void persistSettings(s)}
          onLayoutRename={renameLayout}
          onLayoutDuplicate={(name) => void duplicateLayoutByName(name)}
          onClose={() => setSettingsOpen(false)}
        />
      )}
      {conflict && (
        <ConflictModal
          conflict={conflict}
          onResolve={(choice, applyToAll) => {
            window.fileGrid.resolveMoveConflict(conflict.id, choice, applyToAll)
            setConflict(null)
          }}
        />
      )}
      {deleteLayoutOpen && layout && (
        <ConfirmModal
          title="Delete layout"
          message={`Delete "${layout.name}"? This cannot be undone.`}
          confirmLabel="Delete"
          danger
          onConfirm={() => void performDeleteLayout()}
          onCancel={() => setDeleteLayoutOpen(false)}
        />
      )}
      {newLayoutOpen && (
        <NamePromptModal
          title="New layout"
          label="Name for this sorting job"
          initialValue={newLayoutDefaultName}
          confirmLabel="Create"
          onConfirm={(name) => void confirmNewLayout(name)}
          onCancel={() => setNewLayoutOpen(false)}
        />
      )}
      {relinkTarget && relinkModal && (
        <RelinkFolderModal
          box={relinkTarget}
          message={
            relinkTarget.kind === 'file'
              ? `This shortcut’s file is missing or moved. Choose the file on disk.`
              : relinkModal.retryMovePaths?.length
                ? `This box’s folder is missing. Choose where it moved to, then the drop will be retried.`
                : `This box’s folder is missing or moved. Choose its folder on disk.`
          }
          onRelink={confirmRelink}
          onCancel={() => setRelinkModal(null)}
        />
      )}
    </div>
  )
}
