import { useEffect } from 'react'
import type { ReactNode } from 'react'
import { useBackButton } from '../hooks/useBackButton'

interface Props {
  open: boolean
  title?: string
  onClose: () => void
  children: ReactNode
}

/**
 * Нижний лист. Монтируется постоянно, а показывается CSS-переходом:
 * так закрытие анимируется без состояния и таймеров в компоненте.
 */
export default function BottomSheet({ open, title, onClose, children }: Props) {
  useBackButton(open, onClose)

  useEffect(() => {
    if (!open) {
      return
    }

    function handleKey(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    window.addEventListener('keydown', handleKey)

    return () => window.removeEventListener('keydown', handleKey)
  }, [open, onClose])

  return (
    <div
      className={open ? 'sheet-overlay is-open' : 'sheet-overlay'}
      aria-hidden={!open}
      inert={!open}
      onClick={onClose}
    >
      <div
        className="sheet"
        role={open ? 'dialog' : undefined}
        aria-modal={open ? true : undefined}
        aria-label={title}
        onClick={(event) => event.stopPropagation()}
      >
        <div className="sheet-handle" aria-hidden="true" />
        {title && <h2 className="sheet-title">{title}</h2>}
        <div className="sheet-body">{children}</div>
      </div>
    </div>
  )
}
