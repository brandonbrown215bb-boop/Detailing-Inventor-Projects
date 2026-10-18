import { useState } from 'react'
import type { ConflictChoice, MoveConflictPayload } from '../../../shared/types'

interface Props {
  conflict: MoveConflictPayload
  onResolve: (choice: ConflictChoice, applyToAll: boolean) => void
}

function CompareCard({ label, side }: { label: string; side: MoveConflictPayload['incoming'] }) {
  return (
    <div className="compare-card">
      <div className="compare-card-label">{label}</div>
      <div className="compare-thumb">
        {side.thumbnailDataUrl ? (
          <img src={side.thumbnailDataUrl} alt="" />
        ) : (
          <span className="compare-thumb-fallback">?</span>
        )}
      </div>
      <div className="compare-name" title={side.path}>{side.name}</div>
      <div className="compare-meta">
        <span>{side.sizeLabel}</span>
        {side.modifiedAt && <span>{side.modifiedAt}</span>}
      </div>
    </div>
  )
}

export function ConflictModal({ conflict, onResolve }: Props) {
  const [applyToAll, setApplyToAll] = useState(false)
  const [showCompare, setShowCompare] = useState(true)

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal conflict-modal">
        <h2>Name already in use</h2>
        <p className="modal-lead">
          <strong>{conflict.name}</strong> is already in this folder. Compare the items, then choose what to do.
        </p>

        <button
          type="button"
          className="linkish"
          onClick={() => setShowCompare((v) => !v)}
        >
          {showCompare ? 'Hide compare info' : 'Show compare info'}
        </button>

        {showCompare && (
          <div className="compare-row">
            <CompareCard label="File you're moving" side={conflict.incoming} />
            <CompareCard label="File already there" side={conflict.existing} />
          </div>
        )}

        <label className="modal-checkbox">
          <input
            type="checkbox"
            checked={applyToAll}
            onChange={(e) => setApplyToAll(e.target.checked)}
          />
          Do this for all conflicts in this drop
        </label>

        <div className="modal-actions">
          <button type="button" className="danger" onClick={() => onResolve('replace', applyToAll)}>
            Replace
          </button>
          <button type="button" className="primary" onClick={() => onResolve('keepBoth', applyToAll)}>
            Keep both
          </button>
          <button type="button" onClick={() => onResolve('skip', applyToAll)}>Skip this file</button>
          <button type="button" className="ghost" onClick={() => onResolve('cancel', false)}>
            Cancel operation
          </button>
        </div>
      </div>
    </div>
  )
}
