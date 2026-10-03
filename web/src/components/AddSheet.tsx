import { useRef, useState } from 'react'
import type { ChangeEvent, CSSProperties } from 'react'
import { api, ApiError } from '../api/client'
import { haptic } from '../telegram/telegram'
import { compressImage } from '../utils/image'
import BottomSheet from './BottomSheet'
import Icon from './Icon'

interface Props {
  open: boolean
  onClose: () => void
  onManual: () => void
  onReceipt: (receiptId: string) => void
  onStatement: (statementId: string) => void
}

export default function AddSheet({ open, onClose, onManual, onReceipt, onStatement }: Props) {
  const cameraInput = useRef<HTMLInputElement>(null)
  const galleryInput = useRef<HTMLInputElement>(null)
  const pdfInput = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleReceipt(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''

    if (!file) {
      return
    }

    setError(null)
    setUploading(true)

    try {
      const compressed = await compressImage(file)
      const uploaded = await api.uploadReceipt(compressed.blob, compressed.fileName)
      haptic('success')
      onReceipt(uploaded.id)
      onClose()
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось загрузить чек')
    } finally {
      setUploading(false)
    }
  }

  async function handleStatement(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''

    if (!file) {
      return
    }

    setError(null)
    setUploading(true)

    try {
      const statement = await api.uploadStatement(file, file.name)
      haptic('success')
      onStatement(statement.id)
      onClose()
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось обработать выписку')
    } finally {
      setUploading(false)
    }
  }

  return (
    <BottomSheet open={open} title="Добавить" onClose={onClose}>
      <button className="action-card" style={{ '--cat': 'var(--violet)' } as CSSProperties} onClick={onManual}>
        <span className="action-icon" aria-hidden="true">
          <Icon name="pencil" size={20} />
        </span>
        <span className="action-text">
          <strong>Вручную</strong>
          <span className="muted small">Доход или расход без фото</span>
        </span>
      </button>

      <input ref={cameraInput} type="file" accept="image/*" capture="environment" hidden onChange={handleReceipt} />
      <input ref={galleryInput} type="file" accept="image/*" hidden onChange={handleReceipt} />
      <input ref={pdfInput} type="file" accept="application/pdf,.pdf" hidden onChange={handleStatement} />

      <button
        className="action-card"
        style={{ '--cat': 'var(--brand)' } as CSSProperties}
        disabled={uploading}
        onClick={() => cameraInput.current?.click()}
      >
        <span className="action-icon" aria-hidden="true">
          <Icon name="camera" size={20} />
        </span>
        <span className="action-text">
          <strong>Сфотографировать чек</strong>
          <span className="muted small">AI распознает товары и цены</span>
        </span>
      </button>

      <button
        className="action-card"
        style={{ '--cat': 'var(--cyan)' } as CSSProperties}
        disabled={uploading}
        onClick={() => galleryInput.current?.click()}
      >
        <span className="action-icon" aria-hidden="true">
          <Icon name="image" size={20} />
        </span>
        <span className="action-text">
          <strong>Чек из галереи</strong>
          <span className="muted small">Выбрать готовое фото</span>
        </span>
      </button>

      <button
        className="action-card"
        style={{ '--cat': 'var(--gold)' } as CSSProperties}
        disabled={uploading}
        onClick={() => pdfInput.current?.click()}
      >
        <span className="action-icon" aria-hidden="true">
          <Icon name="file" size={20} />
        </span>
        <span className="action-text">
          <strong>Выписка банка</strong>
          <span className="muted small">PDF с операциями по счёту — AI разберёт</span>
        </span>
      </button>

      {uploading && (
        <p className="muted loading-row" aria-live="polite">
          <span className="spinner" aria-hidden="true" />
          Загрузка и распознавание…
        </p>
      )}
      {error && (
        <p className="error loading-row" aria-live="polite">
          <Icon name="alert" size={16} strokeWidth={2.1} />
          {error}
        </p>
      )}
    </BottomSheet>
  )
}
