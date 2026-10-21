import { useEffect, useRef, useState } from 'react'

interface Props {
  title: string
  label: string
  initialValue: string
  confirmLabel: string
  onConfirm: (value: string) => void
  onCancel: () => void
}

export function NamePromptModal({
  title,
  label,
  initialValue,
  confirmLabel,
  onConfirm,
  onCancel
}: Props) {
  const [value, setValue] = useState(initialValue)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    setValue(initialValue)
    inputRef.current?.focus()
    inputRef.current?.select()
  }, [initialValue])

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true" onClick={onCancel}>
      <div className="modal name-prompt-modal" onClick={(e) => e.stopPropagation()}>
        <h2>{title}</h2>
        <label>
          {label}
          <input
            ref={inputRef}
            type="text"
            value={value}
            onChange={(e) => setValue(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && value.trim()) onConfirm(value.trim())
              if (e.key === 'Escape') onCancel()
            }}
          />
        </label>
        <div className="modal-actions">
          <button type="button" className="primary" disabled={!value.trim()} onClick={() => onConfirm(value.trim())}>
            {confirmLabel}
          </button>
          <button type="button" onClick={onCancel}>Cancel</button>
        </div>
      </div>
    </div>
  )
}
