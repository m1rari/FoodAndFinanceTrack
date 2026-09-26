import { useEffect, useRef, useState } from 'react'
import type { ChangeEvent } from 'react'
import { api, ApiError } from '../api/client'
import type { FoodLogDto, SavedDishDto } from '../api/types'
import BottomSheet from '../components/BottomSheet'
import { haptic } from '../telegram/telegram'
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
  const [saved, setSaved] = useState<SavedDishDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [uploading, setUploading] = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)
  const [composeOpen, setComposeOpen] = useState(false)
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

  useEffect(() => {
    let cancelled = false

    api
      .savedDishes(undefined, 50)
      .then((data) => {
        if (!cancelled) {
          setSaved(data)
        }
      })
      .catch(() => {
        // список блюд необязателен
      })

    return () => {
      cancelled = true
    }
  }, [refreshKey])

  async function handleFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''

    if (!file) {
      return
    }

    setError(null)
    const compressed = await compressImage(file)
    setPending({
      blob: compressed.blob,
      fileName: compressed.fileName,
      previewUrl: URL.createObjectURL(compressed.blob),
    })
  }

  function openCompose() {
    setMenuOpen(false)
    setContext('')
    setPending(null)
    setComposeOpen(true)
  }

  function closeCompose() {
    if (pending) {
      URL.revokeObjectURL(pending.previewUrl)
    }

    setPending(null)
    setContext('')
    setComposeOpen(false)
  }

  async function submitCompose() {
    if (!pending && !context.trim()) {
      setError('Добавьте фото или опишите блюдо.')
      return
    }

    setUploading(true)
    setError(null)

    try {
      const created = pending
        ? await api.uploadFoodLog(pending.blob, pending.fileName, context.trim() || undefined)
        : await api.createFoodLogText(context.trim())

      if (pending) {
        URL.revokeObjectURL(pending.previewUrl)
      }

      setPending(null)
      setContext('')
      setComposeOpen(false)
      haptic('success')
      onUploaded(created.id)
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось добавить блюдо')
    } finally {
      setUploading(false)
    }
  }

  async function addFromSaved(dish: SavedDishDto) {
    setError(null)

    try {
      const log = await api.addSavedDishToDiary(dish.id)
      setMenuOpen(false)
      haptic('success')
      onUploaded(log.id)
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось добавить блюдо')
    }
  }

  async function toggleFavorite(dish: SavedDishDto) {
    try {
      const updated = await api.setSavedDishFavorite(dish.id, !dish.isFavorite)
      setSaved((items) => items.map((item) => (item.id === updated.id ? updated : item)))
    } catch {
      // игнорируем
    }
  }

  const favorites = saved.filter((dish) => dish.isFavorite)
  const recents = saved.filter((dish) => !dish.isFavorite).slice(0, 10)
  const calories = range(sum(logs.map((log) => log.caloriesMin)), sum(logs.map((log) => log.caloriesMax)))
  const hasCalories = logs.some((log) => log.caloriesMin != null || log.caloriesMax != null)

  return (
    <section className="screen">
      <header className="screen-header">
        <h1>Питание</h1>
        <button className="primary" onClick={() => setMenuOpen(true)}>
          + Блюдо
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
            <button className="list-item is-neutral" onClick={() => onOpen(log.id)}>
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

      <BottomSheet open={menuOpen} title="Добавить блюдо" onClose={() => setMenuOpen(false)}>
        <button className="action-card" onClick={openCompose}>
          <span className="action-icon">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 5v14M5 12h14" />
            </svg>
          </span>
          <span className="action-text">
            <strong>Новое блюдо</strong>
            <span className="muted small">Фото и/или описание — AI оценит</span>
          </span>
        </button>

        {favorites.length > 0 && (
          <>
            <h3 className="sheet-section">Избранные</h3>
            <ul className="list">
              {favorites.map((dish) => (
                <li key={dish.id} className="saved-row">
                  <button className="saved-main" onClick={() => addFromSaved(dish)}>
                    <span className="list-title">{dish.name}</span>
                    <span className="muted small">{range(dish.caloriesMin, dish.caloriesMax)} ккал</span>
                  </button>
                  <button className="star on" onClick={() => toggleFavorite(dish)} aria-label="Убрать из избранного">
                    ★
                  </button>
                </li>
              ))}
            </ul>
          </>
        )}

        {recents.length > 0 && (
          <>
            <h3 className="sheet-section">Недавние</h3>
            <ul className="list">
              {recents.map((dish) => (
                <li key={dish.id} className="saved-row">
                  <button className="saved-main" onClick={() => addFromSaved(dish)}>
                    <span className="list-title">{dish.name}</span>
                    <span className="muted small">{range(dish.caloriesMin, dish.caloriesMax)} ккал</span>
                  </button>
                  <button className="star" onClick={() => toggleFavorite(dish)} aria-label="В избранное">
                    ☆
                  </button>
                </li>
              ))}
            </ul>
          </>
        )}
      </BottomSheet>

      <BottomSheet open={composeOpen} title="Новое блюдо" onClose={closeCompose}>
        {pending ? (
          <img className="receipt-image" src={pending.previewUrl} alt="Блюдо" />
        ) : (
          <p className="muted small">Добавьте фото, опишите блюдо или заполните оба поля.</p>
        )}

        <div className="segmented">
          <button type="button" className="ghost" onClick={() => cameraInput.current?.click()}>
            {pending ? 'Переснять' : 'Сфотографировать'}
          </button>
          <button type="button" className="ghost" onClick={() => galleryInput.current?.click()}>
            Из галереи
          </button>
        </div>

        {pending && (
          <button
            type="button"
            className="ghost"
            onClick={() => {
              URL.revokeObjectURL(pending.previewUrl)
              setPending(null)
            }}
          >
            Убрать фото
          </button>
        )}

        <label className="field">
          <span>Описание (необязательно)</span>
          <textarea
            className="textarea"
            rows={3}
            placeholder="Например: домашний борщ со сметаной, порция ~300 г"
            value={context}
            onChange={(event) => setContext(event.target.value)}
          />
        </label>

        <div className="actions">
          <button type="button" className="ghost" onClick={closeCompose}>
            Отмена
          </button>
          <button
            type="button"
            className="primary"
            disabled={uploading || (!pending && !context.trim())}
            onClick={submitCompose}
          >
            {uploading ? 'Обработка…' : 'Оценить'}
          </button>
        </div>
      </BottomSheet>
    </section>
  )
}
