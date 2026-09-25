import { useEffect, useRef, useState } from 'react'
import type { ChangeEvent } from 'react'
import { api, ApiError } from '../api/client'
import type { FoodLogDto } from '../api/types'
import BottomSheet from '../components/BottomSheet'
import { compressImage } from '../utils/image'
import { addDays, dayRange, formatDayTitle } from '../utils/date'
import { formatTime } from '../utils/format'

interface Props {
  refreshKey: number
  onOpen: (id: string) => void
  onUploaded: (id: string) => void
}

const STATUS_LABELS: Record<string, string> = {
  Pending: 'анализ…',
  Processed: '',
  NeedsReview: 'проверьте',
  Failed: 'ошибка',
}

function range(min: number | null, max: number | null): string {
  if (min == null && max == null) {
    return '—'
  }

  if (min != null && max != null) {
    return `${Math.round(min)}–${Math.round(max)}`
  }

  return `${Math.round((min ?? max) as number)}`
}

function sum(values: Array<number | null>): number {
  return values.reduce<number>((total, value) => total + (value ?? 0), 0)
}

export default function FoodScreen({ refreshKey, onOpen, onUploaded }: Props) {
  const cameraInput = useRef<HTMLInputElement>(null)
  const galleryInput = useRef<HTMLInputElement>(null)
  const [day, setDay] = useState(() => new Date())
  const [logs, setLogs] = useState<FoodLogDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [uploading, setUploading] = useState(false)
  const [pending, setPending] = useState<{ blob: Blob; fileName: string; previewUrl: string } | null>(null)
  const [context, setContext] = useState('')

  useEffect(() => {
    let cancelled = false
    const { from, to } = dayRange(day)

    api
      .foodLogs(from, to)
      .then((data) => {
        if (!cancelled) {
          setLogs(data)
          setError(null)
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(err instanceof ApiError ? err.message : 'Не удалось загрузить дневник')
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [day, refreshKey])

  async function handleFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''

    if (!file) {
      return
    }

    setError(null)
    const compressed = await compressImage(file)
    setContext('')
    setPending({
      blob: compressed.blob,
      fileName: compressed.fileName,
      previewUrl: URL.createObjectURL(compressed.blob),
    })
  }

  function cancelPending() {
    if (pending) {
      URL.revokeObjectURL(pending.previewUrl)
    }

    setPending(null)
    setContext('')
  }

  async function confirmUpload() {
    if (!pending) {
      return
    }

    setUploading(true)
    setError(null)

    try {
      const uploaded = await api.uploadFoodLog(pending.blob, pending.fileName, context.trim() || undefined)
      URL.revokeObjectURL(pending.previewUrl)
      setPending(null)
      onUploaded(uploaded.id)
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось загрузить фото')
    } finally {
      setUploading(false)
    }
  }

  const calories = `${range(sum(logs.map((log) => log.caloriesMin)), sum(logs.map((log) => log.caloriesMax)))}`
  const hasCalories = logs.some((log) => log.caloriesMin != null || log.caloriesMax != null)

  return (
    <section className="screen">
      <header className="screen-header">
        <h1>Питание</h1>
        <button className="primary" disabled={uploading} onClick={() => cameraInput.current?.click()}>
          {uploading ? 'Загрузка…' : '+ Блюдо'}
        </button>
      </header>

      <input ref={cameraInput} type="file" accept="image/*" capture="environment" hidden onChange={handleFile} />
      <input ref={galleryInput} type="file" accept="image/*" hidden onChange={handleFile} />

      <div className="day-switch">
        <button className="ghost" onClick={() => setDay((value) => addDays(value, -1))}>
          ‹
        </button>
        <span className="day-switch-title">{formatDayTitle(day)}</span>
        <button className="ghost" onClick={() => setDay((value) => addDays(value, 1))}>
          ›
        </button>
      </div>

      <div className="card">
        <span className="muted small">Калории за день (оценка)</span>
        <strong>{hasCalories ? `${calories} ккал` : '—'}</strong>
        <span className="muted small">
          Б {range(sum(logs.map((log) => log.proteinMinG)), sum(logs.map((log) => log.proteinMaxG)))} · Ж{' '}
          {range(sum(logs.map((log) => log.fatMinG)), sum(logs.map((log) => log.fatMaxG)))} · У{' '}
          {range(sum(logs.map((log) => log.carbsMinG)), sum(logs.map((log) => log.carbsMaxG)))} г
        </span>
      </div>

      {loading && <p className="muted">Загрузка…</p>}
      {error && <p className="error">{error}</p>}

      {!loading && logs.length === 0 && <p className="muted">За этот день блюд нет.</p>}

      <ul className="list">
        {logs.map((log) => (
          <li key={log.id}>
            <button className="list-item" onClick={() => onOpen(log.id)}>
              <span className="list-main">
                <span className="list-title">{log.dishName ?? 'Блюдо'}</span>
                <span className="muted small">
                  {formatTime(log.eatenAt)}
                  {STATUS_LABELS[log.status] ? ` · ${STATUS_LABELS[log.status]}` : ''}
                </span>
              </span>
              <span className="list-right">
                <span className="amount">
                  {log.caloriesMin != null || log.caloriesMax != null
                    ? `${range(log.caloriesMin, log.caloriesMax)} ккал`
                    : '—'}
                </span>
                <span className="muted small">Открыть ›</span>
              </span>
            </button>
          </li>
        ))}
      </ul>

      <button className="ghost" disabled={uploading} onClick={() => galleryInput.current?.click()}>
        Выбрать фото из галереи
      </button>

      <BottomSheet open={pending !== null} title="Добавить блюдо" onClose={cancelPending}>
        {pending && <img className="receipt-image" src={pending.previewUrl} alt="Блюдо" />}

        <label className="field">
          <span>Что на фото? (необязательно)</span>
          <textarea
            className="textarea"
            rows={3}
            placeholder="Например: домашний борщ со сметаной, порция ~300 г"
            value={context}
            onChange={(event) => setContext(event.target.value)}
          />
        </label>

        <p className="muted small">Контекст помогает AI точнее распознать блюдо и порцию.</p>

        <div className="actions">
          <button type="button" className="ghost" onClick={cancelPending}>
            Отмена
          </button>
          <button type="button" className="primary" disabled={uploading} onClick={confirmUpload}>
            {uploading ? 'Распознавание…' : 'Распознать'}
          </button>
        </div>
      </BottomSheet>
    </section>
  )
}
