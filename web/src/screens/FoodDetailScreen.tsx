import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { api, ApiError, fetchFoodImage } from '../api/client'
import type { FoodLogDto, UpdateFoodLogRequest } from '../api/types'
import BottomSheet from '../components/BottomSheet'
import Skeleton from '../components/Skeleton'
import { haptic, openTelegramLink } from '../telegram/telegram'
import { formatDate, formatTime } from '../utils/format'

interface Props {
  foodId: string
  onBack: () => void
  onChanged: () => void
}

const STATUS_LABELS: Record<string, string> = {
  Pending: 'Анализ…',
  Processed: 'Оценка',
  NeedsReview: 'Проверьте оценку',
  Failed: 'Не удалось оценить',
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

function toLocalInput(iso: string): string {
  const date = new Date(iso)
  const pad = (value: number) => String(value).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

function parseNumber(value: string): number | undefined {
  const parsed = Number(value.replace(',', '.'))

  return value.trim() !== '' && Number.isFinite(parsed) ? parsed : undefined
}

export default function FoodDetailScreen({ foodId, onBack, onChanged }: Props) {
  const [log, setLog] = useState<FoodLogDto | null>(null)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [isFavorite, setIsFavorite] = useState(false)
  const [sharing, setSharing] = useState(false)

  const [menuOpen, setMenuOpen] = useState(false)
  const [editOpen, setEditOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [deleting, setDeleting] = useState(false)

  const [dishName, setDishName] = useState('')
  const [portionGrams, setPortionGrams] = useState('')
  const [context, setContext] = useState('')
  const [calMin, setCalMin] = useState('')
  const [calMax, setCalMax] = useState('')
  const [proteinMin, setProteinMin] = useState('')
  const [proteinMax, setProteinMax] = useState('')
  const [fatMin, setFatMin] = useState('')
  const [fatMax, setFatMax] = useState('')
  const [carbsMin, setCarbsMin] = useState('')
  const [carbsMax, setCarbsMax] = useState('')
  const [eatenAt, setEatenAt] = useState('')

  useEffect(() => {
    let cancelled = false

    api
      .foodLog(foodId)
      .then((data) => {
        if (!cancelled) {
          setLog(data)
          setError(null)
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(err instanceof ApiError ? err.message : 'Не удалось загрузить блюдо')
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
  }, [foodId])

  useEffect(() => {
    if (!log || log.status !== 'Pending') {
      return
    }

    let cancelled = false
    const timer = window.setInterval(() => {
      api
        .foodLog(log.id)
        .then((data) => {
          if (!cancelled) {
            setLog(data)
          }
        })
        .catch(() => {
          // продолжаем опрашивать
        })
    }, 3000)

    return () => {
      cancelled = true
      window.clearInterval(timer)
    }
  }, [log])

  useEffect(() => {
    if (!log) {
      return
    }

    let cancelled = false
    let objectUrl: string | null = null

    fetchFoodImage(log.imageUrl)
      .then((blob) => {
        if (!cancelled) {
          objectUrl = URL.createObjectURL(blob)
          setPreviewUrl(objectUrl)
        }
      })
      .catch(() => setPreviewUrl(null))

    return () => {
      cancelled = true
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl)
      }
    }
  }, [log])

  useEffect(() => {
    if (!log?.dishName) {
      return
    }

    let cancelled = false
    const name = log.dishName.toLowerCase()

    api
      .savedDishes(undefined, 100)
      .then((items) => {
        if (!cancelled) {
          const match = items.find((item) => item.name.toLowerCase() === name)
          setIsFavorite(match?.isFavorite ?? false)
        }
      })
      .catch(() => {
        // необязательно
      })

    return () => {
      cancelled = true
    }
  }, [log?.dishName])

  async function toggleFavorite() {
    if (!log) {
      return
    }

    try {
      await api.favoriteFoodLog(log.id, !isFavorite)
      setIsFavorite((value) => !value)
      haptic('light')
      onChanged()
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось обновить избранное')
    }
  }

  async function handleShare() {
    if (!log) {
      return
    }

    setSharing(true)
    setError(null)

    try {
      const share = await api.shareFoodLog(log.id)

      if (!share.url) {
        setError('Шаринг недоступен: не настроено имя бота.')
        return
      }

      const text = `Блюдо: ${log.dishName ?? 'блюдо'}`
      openTelegramLink(`https://t.me/share/url?url=${encodeURIComponent(share.url)}&text=${encodeURIComponent(text)}`)
      haptic('success')
      setMenuOpen(false)
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось поделиться блюдом')
    } finally {
      setSharing(false)
    }
  }

  function openEditor() {
    if (!log) {
      return
    }

    setDishName(log.dishName ?? '')
    setPortionGrams(log.portionGrams != null ? String(log.portionGrams) : '')
    setContext(log.userContext ?? '')
    setCalMin(log.caloriesMin != null ? String(log.caloriesMin) : '')
    setCalMax(log.caloriesMax != null ? String(log.caloriesMax) : '')
    setProteinMin(log.proteinMinG != null ? String(log.proteinMinG) : '')
    setProteinMax(log.proteinMaxG != null ? String(log.proteinMaxG) : '')
    setFatMin(log.fatMinG != null ? String(log.fatMinG) : '')
    setFatMax(log.fatMaxG != null ? String(log.fatMaxG) : '')
    setCarbsMin(log.carbsMinG != null ? String(log.carbsMinG) : '')
    setCarbsMax(log.carbsMaxG != null ? String(log.carbsMaxG) : '')
    setEatenAt(toLocalInput(log.eatenAt))
    setEditOpen(true)
  }

  async function handleSave(event: FormEvent) {
    event.preventDefault()

    if (!log) {
      return
    }

    setSaving(true)
    setError(null)

    const body: UpdateFoodLogRequest = {
      dishName: dishName.trim() === '' ? undefined : dishName.trim(),
      userContext: context,
      portionGrams: parseNumber(portionGrams),
      caloriesMin: parseNumber(calMin),
      caloriesMax: parseNumber(calMax),
      proteinMinG: parseNumber(proteinMin),
      proteinMaxG: parseNumber(proteinMax),
      fatMinG: parseNumber(fatMin),
      fatMaxG: parseNumber(fatMax),
      carbsMinG: parseNumber(carbsMin),
      carbsMaxG: parseNumber(carbsMax),
      eatenAt: eatenAt ? new Date(eatenAt).toISOString() : undefined,
    }

    try {
      const updated = await api.updateFoodLog(log.id, body)
      setLog(updated)
      setEditOpen(false)
      haptic('success')
      onChanged()
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось сохранить')
    } finally {
      setSaving(false)
    }
  }

  async function handleReanalyze() {
    if (!log) {
      return
    }

    setSaving(true)
    setError(null)

    try {
      const updated = await api.reanalyzeFoodLog(log.id, context, parseNumber(portionGrams))
      setLog(updated)
      setEditOpen(false)
      onChanged()
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось запустить повторный анализ')
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete() {
    if (!log) {
      return
    }

    setDeleting(true)
    setError(null)

    try {
      await api.deleteFoodLog(log.id)
      haptic('success')
      onChanged()
      onBack()
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось удалить')
      setDeleting(false)
    }
  }

  return (
    <section className="screen">
      <header className="screen-header">
        <button className="ghost back" onClick={onBack} aria-label="Назад">
          ‹
        </button>
        <h1>{log?.dishName ?? 'Блюдо'}</h1>
        <div className="header-actions">
          <button className="icon-button" onClick={toggleFavorite} aria-label="В избранное">
            {isFavorite ? '★' : '☆'}
          </button>
          <button className="icon-button" onClick={() => setMenuOpen(true)} aria-label="Ещё">
            ⋯
          </button>
        </div>
      </header>

      {loading && <Skeleton rows={3} />}
      {error && (
        <p className="error" aria-live="polite">
          {error}
        </p>
      )}

      {log && (
        <>
          <div className="status-row">
            <p className="status">{STATUS_LABELS[log.status] ?? log.status}</p>
            <p className="status">Оценка AI, может отличаться</p>
          </div>

          {previewUrl && (
            <img
              className="receipt-image"
              src={previewUrl}
              alt="Блюдо"
              width={1200}
              height={900}
              loading="lazy"
              decoding="async"
            />
          )}

          <div className="card">
            <span className="muted small">Калории (оценка)</span>
            <strong>{range(log.caloriesMin, log.caloriesMax)} ккал</strong>
            {log.portionGrams != null && <span className="muted small">Порция ≈ {log.portionGrams} г</span>}
          </div>

          <div className="macro-grid">
            <div className="card">
              <span className="muted small">Белки</span>
              <strong>{range(log.proteinMinG, log.proteinMaxG)} г</strong>
            </div>
            <div className="card">
              <span className="muted small">Жиры</span>
              <strong>{range(log.fatMinG, log.fatMaxG)} г</strong>
            </div>
            <div className="card">
              <span className="muted small">Углеводы</span>
              <strong>{range(log.carbsMinG, log.carbsMaxG)} г</strong>
            </div>
          </div>

          {log.userContext && (
            <div className="card">
              <span className="muted small">Описание</span>
              <span>{log.userContext}</span>
            </div>
          )}

          <p className="muted small center">
            {formatDate(log.eatenAt)} · {formatTime(log.eatenAt)}
          </p>

          <button className="primary" onClick={openEditor}>
            Скорректировать
          </button>
        </>
      )}

      <BottomSheet open={menuOpen} title="Ещё" onClose={() => setMenuOpen(false)}>
        <button className="action-card" disabled={sharing} onClick={handleShare}>
          <span className="action-icon" aria-hidden="true">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="18" cy="5" r="3" />
              <circle cx="6" cy="12" r="3" />
              <circle cx="18" cy="19" r="3" />
              <path d="M8.6 10.6l6.8-3.2M8.6 13.4l6.8 3.2" />
            </svg>
          </span>
          <span className="action-text">
            <strong>{sharing ? 'Подготовка…' : 'Поделиться'}</strong>
            <span className="muted small">Отправить блюдо другому</span>
          </span>
        </button>

        <button className="action-card" onClick={() => { setMenuOpen(false); setDeleteOpen(true) }}>
          <span className="action-icon danger-icon" aria-hidden="true">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M3 6h18M8 6V4h8v2M19 6l-1 14H6L5 6" />
            </svg>
          </span>
          <span className="action-text">
            <strong>Удалить блюдо</strong>
            <span className="muted small">Фото и оценка будут удалены</span>
          </span>
        </button>
      </BottomSheet>

      <BottomSheet open={editOpen} title="Скорректировать" onClose={() => setEditOpen(false)}>
        <form className="form" onSubmit={handleSave}>
          <label className="field">
            <span>Название</span>
            <input value={dishName} onChange={(event) => setDishName(event.target.value)} enterKeyHint="done" />
          </label>

          <label className="field">
            <span>Граммовка, г</span>
            <input
              inputMode="decimal"
              enterKeyHint="done"
              value={portionGrams}
              onChange={(event) => setPortionGrams(event.target.value)}
              placeholder="например 250"
            />
          </label>

          <label className="field">
            <span>Описание / контекст</span>
            <textarea
              className="textarea"
              rows={3}
              placeholder="Например: порция ~300 г, с маслом"
              value={context}
              onChange={(event) => setContext(event.target.value)}
            />
          </label>

          <div className="item-grid">
            <label className="field">
              <span>Ккал от</span>
              <input inputMode="decimal" value={calMin} onChange={(event) => setCalMin(event.target.value)} />
            </label>
            <label className="field">
              <span>Ккал до</span>
              <input inputMode="decimal" value={calMax} onChange={(event) => setCalMax(event.target.value)} />
            </label>
          </div>

          <div className="item-grid">
            <label className="field">
              <span>Белки от</span>
              <input inputMode="decimal" value={proteinMin} onChange={(event) => setProteinMin(event.target.value)} />
            </label>
            <label className="field">
              <span>Белки до</span>
              <input inputMode="decimal" value={proteinMax} onChange={(event) => setProteinMax(event.target.value)} />
            </label>
          </div>

          <div className="item-grid">
            <label className="field">
              <span>Жиры от</span>
              <input inputMode="decimal" value={fatMin} onChange={(event) => setFatMin(event.target.value)} />
            </label>
            <label className="field">
              <span>Жиры до</span>
              <input inputMode="decimal" value={fatMax} onChange={(event) => setFatMax(event.target.value)} />
            </label>
          </div>

          <div className="item-grid">
            <label className="field">
              <span>Углеводы от</span>
              <input inputMode="decimal" value={carbsMin} onChange={(event) => setCarbsMin(event.target.value)} />
            </label>
            <label className="field">
              <span>Углеводы до</span>
              <input inputMode="decimal" value={carbsMax} onChange={(event) => setCarbsMax(event.target.value)} />
            </label>
          </div>

          <label className="field">
            <span>Время</span>
            <input type="datetime-local" value={eatenAt} onChange={(event) => setEatenAt(event.target.value)} />
          </label>

          <button type="button" className="ghost" disabled={saving} onClick={handleReanalyze}>
            Распознать заново (с граммовкой и контекстом)
          </button>

          <div className="actions">
            <button type="button" className="ghost" onClick={() => setEditOpen(false)}>
              Отмена
            </button>
            <button type="submit" className="primary" disabled={saving}>
              {saving ? 'Сохранение…' : 'Сохранить'}
            </button>
          </div>
        </form>
      </BottomSheet>

      <BottomSheet open={deleteOpen} title="Удалить блюдо?" onClose={() => setDeleteOpen(false)}>
        <p className="muted">Фото и оценка будут удалены.</p>
        <div className="actions">
          <button type="button" className="ghost" onClick={() => setDeleteOpen(false)}>
            Отмена
          </button>
          <button type="button" className="danger" disabled={deleting} onClick={handleDelete}>
            {deleting ? 'Удаление…' : 'Удалить'}
          </button>
        </div>
      </BottomSheet>
    </section>
  )
}
