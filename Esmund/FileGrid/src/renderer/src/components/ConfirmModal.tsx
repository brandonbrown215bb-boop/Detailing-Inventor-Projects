interface Props {
  title: string
  message: string
  confirmLabel: string
  danger?: boolean
  onConfirm: () => void
  onCancel: () => void
}

export function ConfirmModal({
  title,
  message,
  confirmLabel,
  danger,
  onConfirm,
  onCancel
}: Props) {
  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true" onClick={onCancel}>
      <div className="modal confirm-modal" onClick={(e) => e.stopPropagation()}>
        <h2>{title}</h2>
        <p className="modal-lead">{message}</p>
        <div className="modal-actions">
          <button type="button" className={danger ? 'danger' : 'primary'} onClick={onConfirm}>
            {confirmLabel}
          </button>
          <button type="button" onClick={onCancel}>Cancel</button>
        </div>
      </div>
    </div>
  )
}
