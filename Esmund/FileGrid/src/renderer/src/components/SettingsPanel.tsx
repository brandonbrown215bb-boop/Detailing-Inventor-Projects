import { useEffect, useState } from 'react'
import type { AppSettings, LayoutState } from '../../../shared/types'
import { DEFAULT_APP_SETTINGS } from '../../../shared/types'
import { FONT_OPTION_GROUPS } from '../fontOptions'

interface Props {
  settings: AppSettings
  layout: LayoutState
  onSettingsChange: (s: AppSettings) => void
  onLayoutRename: (name: string) => void | Promise<void>
  onLayoutDuplicate: (name: string) => void
  onClose: () => void
}

export function SettingsPanel({
  settings,
  layout,
  onSettingsChange,
  onLayoutRename,
  onLayoutDuplicate,
  onClose
}: Props) {
  const [local, setLocal] = useState(settings)
  const [layoutName, setLayoutName] = useState(layout.name)
  const [dupName, setDupName] = useState(`${layout.name} copy`)

  useEffect(() => {
    setLocal(settings)
  }, [settings])

  useEffect(() => {
    setLayoutName(layout.name)
    setDupName(`${layout.name} copy`)
  }, [layout.id])

  const patch = (p: Partial<AppSettings>) => {
    const next = { ...local, ...p }
    setLocal(next)
    onSettingsChange(next)
  }

  const resetAppearance = () => onSettingsChange({ ...DEFAULT_APP_SETTINGS })

  const dockHorizontal =
    local.toolbarDock === 'top' || local.toolbarDock === 'bottom'

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true" onClick={onClose}>
      <div className="modal settings-modal" onClick={(e) => e.stopPropagation()}>
        <div className="settings-header">
          <h2>Settings</h2>
          <button type="button" className="icon-btn" onClick={onClose} aria-label="Close">×</button>
        </div>

        <section className="settings-section">
          <h3>Toolbar</h3>
          <label>
            Dock toolbar to
            <select
              value={local.toolbarDock}
              onChange={(e) => patch({ toolbarDock: e.target.value as AppSettings['toolbarDock'] })}
            >
              <option value="top">Top</option>
              <option value="bottom">Bottom</option>
              <option value="left">Left</option>
              <option value="right">Right</option>
            </select>
          </label>
          <label>
            Align on edge
            <select
              value={local.toolbarAlign}
              onChange={(e) => patch({ toolbarAlign: e.target.value as AppSettings['toolbarAlign'] })}
            >
              {dockHorizontal ? (
                <>
                  <option value="start">Left</option>
                  <option value="center">Middle</option>
                  <option value="end">Right</option>
                </>
              ) : (
                <>
                  <option value="start">Top</option>
                  <option value="center">Middle</option>
                  <option value="end">Bottom</option>
                </>
              )}
            </select>
          </label>
        </section>

        <section className="settings-section">
          <h3>Folder drop</h3>
          <p className="settings-note">
            Click the drop button on the toolbar to switch between Drop Folder and Drop Tree. Tree
            drops lay out parent → child downward and add strings between each folder and its
            subfolders.
          </p>
          <label>
            Tree depth (levels under root)
            <input
              type="number"
              min={0}
              max={15}
              value={local.folderTreeMaxDepth}
              onChange={(e) =>
                patch({ folderTreeMaxDepth: Math.min(15, Math.max(0, Number(e.target.value) || 0)) })
              }
            />
          </label>
          <p className="settings-note">
            The dropped folder is depth 0. A value of 7 also includes subfolders up to seven levels
            below that root. Use 0 for root only.
          </p>
          <label>
            Max boxes per tree drop
            <input
              type="number"
              min={20}
              max={800}
              value={local.folderTreeMaxBoxes}
              onChange={(e) =>
                patch({
                  folderTreeMaxBoxes: Math.min(800, Math.max(20, Number(e.target.value) || 20))
                })
              }
            />
          </label>
          <label>
            Exclude folder names (one per line)
            <textarea
              rows={5}
              value={local.folderTreeExcludeNames.join('\n')}
              onChange={(e) =>
                patch({
                  folderTreeExcludeNames: e.target.value
                    .split(/\r?\n/)
                    .map((s) => s.trim())
                    .filter(Boolean)
                })
              }
            />
          </label>
        </section>

        <section className="settings-section">
          <h3>Canvas</h3>
          <label>
            Zoom controls
            <select
              value={local.zoomInput}
              onChange={(e) => patch({ zoomInput: e.target.value as AppSettings['zoomInput'] })}
            >
              <option value="wheel">Mouse wheel</option>
              <option value="keys">+ and − keys</option>
              <option value="both">Mouse wheel and + / − keys</option>
            </select>
          </label>
          <p className="settings-note">
            + and − zoom toward the center of the canvas. Works when focus is not in a text field.
          </p>
        </section>

        <section className="settings-section">
          <h3>Appearance</h3>
          <label>
            Text scale
            <input
              type="range"
              min={0.65}
              max={2}
              step={0.05}
              value={local.textScale}
              onChange={(e) => patch({ textScale: Number(e.target.value) })}
            />
            <span>{Math.round(local.textScale * 100)}%</span>
          </label>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={local.showFilePath}
              onChange={(e) => patch({ showFilePath: e.target.checked })}
            />
            Show file path on folder boxes
          </label>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={local.showGrid}
              onChange={(e) => patch({ showGrid: e.target.checked })}
            />
            Show alignment grid
          </label>
          <label>
            Background
            <input
              type="color"
              value={local.backgroundColor}
              onChange={(e) => patch({ backgroundColor: e.target.value })}
            />
          </label>
          <label>
            Grid color
            <input
              type="color"
              value={local.gridColor}
              onChange={(e) => patch({ gridColor: e.target.value })}
            />
          </label>
          <label>
            Grid opacity
            <input
              type="range"
              min={0.05}
              max={1}
              step={0.01}
              value={local.gridOpacity}
              onChange={(e) => patch({ gridOpacity: Number(e.target.value) })}
            />
          </label>
          <label>
            Font
            <select
              value={local.fontFamily}
              onChange={(e) => patch({ fontFamily: e.target.value })}
            >
              {FONT_OPTION_GROUPS.map((group) => (
                <optgroup key={group.label} label={group.label}>
                  {group.options.map((f) => (
                    <option key={f.value} value={f.value}>{f.label}</option>
                  ))}
                </optgroup>
              ))}
            </select>
          </label>
          <p className="settings-note">
            Lexend, Atkinson Hyperlegible, OpenDyslexic, Andika, and Comic Neue load automatically.
            Calibre variants use fonts installed on your PC (commercial typeface).
          </p>
          <label>
            Font color
            <input
              type="color"
              value={local.fontColor}
              onChange={(e) => patch({ fontColor: e.target.value })}
            />
          </label>
          <button type="button" onClick={resetAppearance}>Reset appearance defaults</button>
        </section>

        <section className="settings-section">
          <h3>Layout</h3>
          <label>
            Rename current layout
            <input
              type="text"
              value={layoutName}
              onChange={(e) => setLayoutName(e.target.value)}
            />
          </label>
          <button
            type="button"
            disabled={!layoutName.trim() || layoutName.trim() === layout.name}
            onClick={() => void onLayoutRename(layoutName.trim())}
          >
            Save name
          </button>
          <label>
            Duplicate layout as
            <input type="text" value={dupName} onChange={(e) => setDupName(e.target.value)} />
          </label>
          <button
            type="button"
            disabled={!dupName.trim()}
            onClick={() => onLayoutDuplicate(dupName.trim())}
          >
            Duplicate
          </button>
        </section>
      </div>
    </div>
  )
}
