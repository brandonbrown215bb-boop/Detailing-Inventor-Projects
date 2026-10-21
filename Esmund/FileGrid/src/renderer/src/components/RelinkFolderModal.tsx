import type { GridBox } from '../../../shared/types'

interface Props {
  box: GridBox
  message: string
  onRelink: (newFolderPath: string) => void
  onCancel: () => void
}

export function RelinkFolderModal({ box, message, onRelink, onCancel }: Props) {
  const isFile = box.kind === 'file'

  const choosePath = async () => {
    if (isFile) {
      const filePath = await window.fileGrid.pickFile()
      if (!filePath) return
      const ok = await window.fileGrid.pathIsFile(filePath)
      if (!ok) return
      onRelink(filePath)
      return
    }
    const folder = await window.fileGrid.pickFolder()
    if (!folder) return
    const ok = await window.fileGrid.pathIsDirectory(folder)
    if (!ok) return
    onRelink(folder)
  }

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true" onClick={onCancel}>
      <div className="modal confirm-modal" onClick={(e) => e.stopPropagation()}>
        <h2>{isFile ? 'File not found' : 'Folder not found'}</h2>
        <p className="modal-lead">{message}</p>
        <p className="modal-path" title={box.folderPath}>{box.folderPath}</p>
        <div className="modal-actions">
          <button type="button" className="primary" onClick={() => void choosePath()}>
            {isFile ? 'Choose file…' : 'Choose folder…'}
          </button>
          <button type="button" onClick={onCancel}>Cancel</button>
        </div>
      </div>
    </div>
  )
}
