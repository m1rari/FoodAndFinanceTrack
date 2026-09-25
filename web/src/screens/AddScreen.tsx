import { useRef, useState } from 'react'
import type { ChangeEvent } from 'react'
import { api, ApiError } from '../api/client'
import { compressImage } from '../utils/image'

interface Props {
  onManual: () => void
  onUploaded: (receiptId: string) => void
}

export default function AddScreen({ onManual, onUploaded }: Props) {
  const cameraInput = useRef<HTMLInputElement>(null)
  const galleryInput = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleFile(event: ChangeEvent<HTMLInputElement>) {
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
      onUploaded(uploaded.id)
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось загрузить чек')
    } finally {
      setUploading(false)
    }
  }

  return (
    <section className="screen">
      <header className="screen-header">
        <h1>Добавить</h1>
      </header>

      <button className="action-card" onClick={onManual}>
        <span className="action-icon">₽</span>
        <span className="action-text">
          <strong>Вручную</strong>
          <span className="muted small">Доход или расход без фото</span>
        </span>
      </button>

      <input
        ref={cameraInput}
        type="file"
        accept="image/*"
        capture="environment"
        hidden
        onChange={handleFile}
      />
      <input ref={galleryInput} type="file" accept="image/*" hidden onChange={handleFile} />

      <button className="action-card" disabled={uploading} onClick={() => cameraInput.current?.click()}>
        <span className="action-icon">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z" />
            <circle cx="12" cy="13" r="4" />
          </svg>
        </span>
        <span className="action-text">
          <strong>Сфотографировать чек</strong>
          <span className="muted small">AI распознает товары и цены</span>
        </span>
      </button>

      <button className="action-card" disabled={uploading} onClick={() => galleryInput.current?.click()}>
        <span className="action-icon">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <rect x="3" y="3" width="18" height="18" rx="2" />
            <circle cx="8.5" cy="8.5" r="1.5" />
            <path d="M21 15l-5-5L5 21" />
          </svg>
        </span>
        <span className="action-text">
          <strong>Чек из галереи</strong>
          <span className="muted small">Выбрать готовое фото</span>
        </span>
      </button>

      {uploading && <p className="muted">Загрузка и распознавание…</p>}
      {error && <p className="error">{error}</p>}

      <p className="muted small">
        Чек также можно отправить прямо в чат с ботом — он обработает фото и напишет результат.
      </p>
    </section>
  )
}
