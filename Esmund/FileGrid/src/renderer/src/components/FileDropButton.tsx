import { useState } from 'react'

interface Props {
  onFilesDropped: (filePaths: string[]) => void
}

export function FileDropButton({ onFilesDropped }: Props) {
  const [over, setOver] = useState(false)

  return (
    <button
      type="button"
      className={`folder-drop-btn file-drop-btn ${over ? 'drag-over' : ''}`}
      title="Drop file(s) to add file shortcut boxes on the grid."
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
        const dropped = [...e.dataTransfer.files]
        const filePaths: string[] = []
        for (const file of dropped) {
          const p = window.fileGrid.getPathForFile(file)
          if (!p) continue
          if (await window.fileGrid.pathIsFile(p)) filePaths.push(p)
        }
        if (filePaths.length) onFilesDropped(filePaths)
      }}
    >
      Drop File
    </button>
  )
}
