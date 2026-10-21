import { useState } from 'react'
import type { FolderDropMode } from '../../../shared/types'

interface Props {
  mode: FolderDropMode
  onModeToggle: () => void
  onFoldersDropped: (folderPaths: string[]) => void
}

export function FolderDropButton({ mode, onModeToggle, onFoldersDropped }: Props) {
  const [over, setOver] = useState(false)
  const treeMode = mode === 'tree'
  const label = treeMode ? 'Drop Tree' : 'Drop Folder'

  return (
    <button
      type="button"
      className={`folder-drop-btn ${treeMode ? 'folder-drop-btn--tree' : ''} ${over ? 'drag-over' : ''}`}
      title={
        treeMode
          ? 'Drop one folder to build a parent/child tree with strings. Click to switch to Drop Folder mode.'
          : 'Drop folder(s) as separate boxes. Click to switch to Drop Tree mode.'
      }
      onClick={onModeToggle}
      onDragOver={(e) => {
        e.preventDefault()
        e.stopPropagation()
        e.dataTransfer.dropEffect = 'copy'
        setOver(true)
      }}
      onDragLeave={() => setOver(false)}
      onDrop={async (e) => {
        e.preventDefault()
        e.stopPropagation()
        setOver(false)
        const files = [...e.dataTransfer.files]
        const folders: string[] = []
        for (const file of files) {
          const p = window.fileGrid.getPathForFile(file)
          if (!p) continue
          if (await window.fileGrid.pathIsDirectory(p)) folders.push(p)
        }
        if (folders.length) onFoldersDropped(folders)
      }}
    >
      {label}
    </button>
  )
}
