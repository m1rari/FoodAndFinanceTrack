import { useEffect, useRef, useState } from 'react'
import type { ChangeEvent, CSSProperties } from 'react'
import { api, ApiError } from '../api/client'
import type { FoodLogDto, SavedDishDto } from '../api/types'
import BottomSheet from '../components/BottomSheet'
import Confetti from '../components/Confetti'
import EmptyState from '../components/EmptyState'
import Icon from '../components/Icon'
import MacroRing from '../components/MacroRing'
import Skeleton from '../components/Skeleton'
import { useCelebration } from '../hooks/useCelebration'
import { haptic } from '../telegram/telegram'
import { compressImage } from '../utils/image'
import { addDays, dayKey, dayRange, formatDayTitle } from '../utils/date'
import { formatTime } from '../utils/format'
import { MACRO_COLORS, mealVisual } from '../utils/visuals'

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
  const [adding, setAdding] = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)
  const [composeOpen, setComposeOpen] = useState(false)
  const [confirmDish, setConfirmDish] = useState<SavedDishDto | null>(null)
  const [summaryOpen, setSummaryOpen] = useState(false)
  const [localRefresh, setLocalRefresh] = useState(0)
  const [pending, setPending] = useState<{ blob: Blob; fileName: string; previewUrl: string } | null>(null)
  const [context, setContext] = useState('')
  const [celebration, celebrate] = useCelebration()

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
  }, [day, refreshKey, localRefresh])

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
      celebrate()
      onUploaded(created.id)
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось добавить блюдо')
    } finally {
      setUploading(false)
    }
  }

  function openConfirm(dish: SavedDishDto) {
    setConfirmDish(dish)
  }

  async function confirmAdd() {
    if (!confirmDish) {
      return
    }

    setError(null)
    setAdding(true)

    try {
      await api.addSavedDishToDiary(confirmDish.id)
      setConfirmDish(null)
      setMenuOpen(false)
      setLocalRefresh((value) => value + 1)
      haptic('success')
      celebrate()
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось добавить блюдо')
    } finally {
      setAdding(false)
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
  const quickDishes = [...favorites, ...recents].slice(0, 12)
  const isToday = dayKey(day.toISOString()) === dayKey(new Date().toISOString())
  const caloriesMin = sum(logs.map((log) => log.caloriesMin))
  const caloriesMax = sum(logs.map((log) => log.caloriesMax))
  const hasCalories = logs.some((log) => log.caloriesMin != null || log.caloriesMax != null)
  const proteinMid = (sum(logs.map((log) => log.proteinMinG)) + sum(logs.map((log) => log.proteinMaxG))) / 2
  const fatMid = (sum(logs.map((log) => log.fatMinG)) + sum(logs.map((log) => log.fatMaxG))) / 2
  const carbsMid = (sum(logs.map((log) => log.carbsMinG)) + sum(logs.map((log) => log.carbsMaxG))) / 2

  const mealGroups = new Map<string, FoodLogDto[]>()
  const order: Array<{ type: 'single'; log: FoodLogDto } | { type: 'meal'; id: string }> = []

  for (const log of logs) {
    if (log.mealGroupId) {
      if (!mealGroups.has(log.mealGroupId)) {
        mealGroups.set(log.mealGroupId, [])
        order.push({ type: 'meal', id: log.mealGroupId })
      }

      mealGroups.get(log.mealGroupId)!.push(log)
    } else {
      order.push({ type: 'single', log })
    }
  }

  return (
    <section className="screen">
      <Confetti trigger={celebration} />

      <header className="screen-header">
        <h1>
          <span className="title-icon" aria-hidden="true">
            <Icon name="utensils" size={18} />
          </span>
          Питание
        </h1>
        <button className="primary" onClick={() => setMenuOpen(true)}>
          <Icon name="plus" size={18} />
          Блюдо
        </button>
      </header>

      <input ref={cameraInput} type="file" accept="image/*" capture="environment" hidden onChange={handleFile} />
      <input ref={galleryInput} type="file" accept="image/*" hidden onChange={handleFile} />

      <div className="day-switch">
        <button className="ghost" aria-label="Предыдущий день" onClick={() => setDay((value) => addDays(value, -1))}>
          <Icon name="chevron-left" size={20} strokeWidth={2.1} />
        </button>
        <span className="day-switch-title">{formatDayTitle(day)}</span>
        <button className="ghost" aria-label="Следующий день" onClick={() => setDay((value) => addDays(value, 1))}>
          <Icon name="chevron-right" size={20} strokeWidth={2.1} />
        </button>
      </div>

      {isToday && quickDishes.length > 0 && (
        <>
          <h2 className="section-title">Быстро добавить</h2>
          <div className="chips-row">
            {quickDishes.map((dish) => (
              <button key={dish.id} className="chip" onClick={() => openConfirm(dish)}>
                <Icon name={dish.isFavorite ? 'star-filled' : 'plus'} size={14} strokeWidth={2.2} />
                {dish.name}
              </button>
            ))}
          </div>
        </>
      )}

      {logs.length > 0 && (
        <div className="collapsible">
          <button
            className="collapsible-head"
            onClick={() => setSummaryOpen((value) => !value)}
            aria-expanded={summaryOpen}
          >
            <span className="collapsible-title">
              <Icon name="chart" size={16} />
              Сводка за день
              {!summaryOpen && hasCalories && (
                <span className="muted small">· ~{Math.round((caloriesMin + caloriesMax) / 2)} ккал</span>
              )}
            </span>
            <span className={summaryOpen ? 'collapsible-caret is-open' : 'collapsible-caret'} aria-hidden="true">
              <Icon name="chevron-down" size={18} />
            </span>
          </button>

          {summaryOpen && (
            <div className="card day-summary">
              <MacroRing
                calories={hasCalories ? String(Math.round((caloriesMin + caloriesMax) / 2)) : '—'}
                protein={proteinMid}
                fat={fatMid}
                carbs={carbsMid}
              />

              <div className="macro-legend">
                <span className="muted small">Калории за день (оценка AI)</span>
                <span className="macro-row">
                  <span className="macro-dot" style={{ '--cat': MACRO_COLORS.protein } as CSSProperties} aria-hidden="true" />
                  Белки
                  <strong>{range(sum(logs.map((log) => log.proteinMinG)), sum(logs.map((log) => log.proteinMaxG)))} г</strong>
                </span>
                <span className="macro-row">
                  <span className="macro-dot" style={{ '--cat': MACRO_COLORS.fat } as CSSProperties} aria-hidden="true" />
                  Жиры
                  <strong>{range(sum(logs.map((log) => log.fatMinG)), sum(logs.map((log) => log.fatMaxG)))} г</strong>
                </span>
                <span className="macro-row">
                  <span className="macro-dot" style={{ '--cat': MACRO_COLORS.carbs } as CSSProperties} aria-hidden="true" />
                  Углеводы
                  <strong>{range(sum(logs.map((log) => log.carbsMinG)), sum(logs.map((log) => log.carbsMaxG)))} г</strong>
                </span>
              </div>
            </div>
          )}
        </div>
      )}

      {loading && <Skeleton rows={3} />}
      {error && (
        <p className="error" aria-live="polite">
          {error}
        </p>
      )}

      {!loading && logs.length === 0 && (
        <EmptyState art="plate" title="Дневник пуст" text="Сфотографируйте блюдо или опишите его словами — AI оценит калории и БЖУ.">
          <button className="primary" onClick={() => setMenuOpen(true)}>
            <Icon name="plus" size={18} />
            Добавить блюдо
          </button>
        </EmptyState>
      )}

      <ul className="list">
        {order.map((entry) => {
          if (entry.type === 'single') {
            const meal = mealVisual(entry.log.eatenAt)

            return (
              <li key={entry.log.id}>
                <button className="list-item is-neutral" onClick={() => onOpen(entry.log.id)}>
                  <span className="list-badge" style={{ '--cat': meal.color } as CSSProperties} aria-hidden="true">
                    <Icon name={meal.icon} size={18} />
                  </span>
                  <span className="list-main">
                    <span className="list-title">{entry.log.dishName ?? 'Блюдо'}</span>
                    <span className="muted small">
                      {meal.label} · {formatTime(entry.log.eatenAt)}
                      {STATUS_LABELS[entry.log.status] ? ` · ${STATUS_LABELS[entry.log.status]}` : ''}
                    </span>
                  </span>
                  <span className="list-right">
                    <span className="amount">
                      {entry.log.caloriesMin != null || entry.log.caloriesMax != null
                        ? `${range(entry.log.caloriesMin, entry.log.caloriesMax)} ккал`
                        : '—'}
                    </span>
                    <span className="muted small">Открыть ›</span>
                  </span>
                </button>
              </li>
            )
          }

          const group = mealGroups.get(entry.id)!
          const meal = mealVisual(group[0].eatenAt)

          return (
            <li key={entry.id} className="meal-card" style={{ '--cat': meal.color } as CSSProperties}>
              <div className="meal-head">
                <span>
                  <Icon name={meal.icon} size={14} strokeWidth={2.1} />
                  {meal.label}
                </span>
                <span className="muted small">{formatTime(group[0].eatenAt)}</span>
              </div>
              {group.map((log) => (
                <button key={log.id} className="meal-item" onClick={() => onOpen(log.id)}>
                  <span className="list-title">{log.dishName ?? 'Блюдо'}</span>
                  <span className="amount">
                    {log.caloriesMin != null || log.caloriesMax != null
                      ? `${range(log.caloriesMin, log.caloriesMax)} ккал`
                      : log.portionGrams
                        ? `${log.portionGrams} г`
                        : '—'}
                  </span>
                </button>
              ))}
            </li>
          )
        })}
      </ul>

      <BottomSheet open={menuOpen} title="Добавить блюдо" onClose={() => setMenuOpen(false)}>
        <button className="action-card" style={{ '--cat': 'var(--brand)' } as CSSProperties} onClick={openCompose}>
          <span className="action-icon" aria-hidden="true">
            <Icon name="sparkles" size={20} />
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
                  <button className="saved-main" onClick={() => openConfirm(dish)}>
                    <span className="list-title">{dish.name}</span>
                    <span className="muted small">{range(dish.caloriesMin, dish.caloriesMax)} ккал</span>
                  </button>
                  <button className="star on" onClick={() => toggleFavorite(dish)} aria-label="Убрать из избранного">
                    <Icon name="star-filled" size={20} />
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
                  <button className="saved-main" onClick={() => openConfirm(dish)}>
                    <span className="list-title">{dish.name}</span>
                    <span className="muted small">{range(dish.caloriesMin, dish.caloriesMax)} ккал</span>
                  </button>
                  <button className="star" onClick={() => toggleFavorite(dish)} aria-label="В избранное">
                    <Icon name="star" size={20} />
                  </button>
                </li>
              ))}
            </ul>
          </>
        )}
      </BottomSheet>

      <BottomSheet open={composeOpen} title="Новое блюдо" onClose={closeCompose}>
        {pending ? (
          <img className="receipt-image" src={pending.previewUrl} alt="Блюдо" width={1200} height={900} />
        ) : (
          <p className="muted small">Добавьте фото, опишите блюдо или заполните оба поля.</p>
        )}

        <div className="segmented">
          <button type="button" className="segment" onClick={() => cameraInput.current?.click()}>
            <Icon name="camera" size={17} />
            {pending ? 'Переснять' : 'Сфотографировать'}
          </button>
          <button type="button" className="segment" onClick={() => galleryInput.current?.click()}>
            <Icon name="image" size={17} />
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
            <Icon name="trash" size={17} />
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

      <BottomSheet open={confirmDish !== null} title="Добавить в дневник?" onClose={() => setConfirmDish(null)}>
        {confirmDish && (
          <>
            <div className="card">
              <strong>{confirmDish.name}</strong>
              <span className="muted small">
                {range(confirmDish.caloriesMin, confirmDish.caloriesMax)} ккал · Б{' '}
                {range(confirmDish.proteinMinG, confirmDish.proteinMaxG)} · Ж{' '}
                {range(confirmDish.fatMinG, confirmDish.fatMaxG)} · У{' '}
                {range(confirmDish.carbsMinG, confirmDish.carbsMaxG)} г
              </span>
            </div>

            <div className="actions">
              <button type="button" className="ghost" onClick={() => setConfirmDish(null)}>
                Отмена
              </button>
              <button type="button" className="primary" disabled={adding} onClick={confirmAdd}>
                <Icon name="plus" size={18} />
                {adding ? 'Добавление…' : 'Добавить в дневник'}
              </button>
            </div>
          </>
        )}
      </BottomSheet>
    </section>
  )
}
