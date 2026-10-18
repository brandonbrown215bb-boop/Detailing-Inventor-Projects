import { useState } from 'react'

interface Props {
  onFoldersDropped: (folderPaths: string[]) => void
}

export function FolderDropButton({ onFoldersDropped }: Props) {
  const [over, setOver] = useState(false)

  return (
    <button
      type="button"
      className={`folder-drop-btn ${over ? 'drag-over' : ''}`}
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
      Drop Folder
    </button>
  )
}
