import { useEffect, useState } from 'react'
import type { CSSProperties } from 'react'
import { api, ApiError } from '../api/client'
import type { CategoryDto, StatementDto, StatementOperationDto, TransactionDto } from '../api/types'
import BottomSheet from '../components/BottomSheet'
import CategorySelect from '../components/CategorySelect'
import Confetti from '../components/Confetti'
import Icon from '../components/Icon'
import Skeleton from '../components/Skeleton'
import { useCelebration } from '../hooks/useCelebration'
import { haptic } from '../telegram/telegram'
import { dayKey, formatDayLabel } from '../utils/date'
import { formatDate, formatMoney, formatTime, plural } from '../utils/format'
import { categoryVisual } from '../utils/visuals'

interface Props {
  statementId: string
  onBack: () => void
  onChanged: () => void
}

const STATUS_LABELS: Record<string, string> = {
  Pending: 'Разбор…',
  Processed: 'Проверьте и подтвердите',
  NeedsReview: 'Мало данных — проверьте',
  Failed: 'Не удалось разобрать',
}

function parseNumber(value: string): number {
  return Number(value.replace(/\s/g, '').replace(',', '.'))
}

export default function StatementReviewScreen({ statementId, onBack, onChanged }: Props) {
  const [statement, setStatement] = useState<StatementDto | null>(null)
  const [operations, setOperations] = useState<StatementOperationDto[]>([])
  const [categories, setCategories] = useState<CategoryDto[]>([])
  const [matches, setMatches] = useState<Record<number, TransactionDto[]>>({})
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [confirming, setConfirming] = useState(false)

  const [editIndex, setEditIndex] = useState<number | null>(null)
  const [direction, setDirection] = useState('expense')
  const [amount, setAmount] = useState('')
  const [description, setDescription] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [isTransfer, setIsTransfer] = useState(false)
  const [linkId, setLinkId] = useState<string | null>(null)
  const [celebration, celebrate] = useCelebration()

  useEffect(() => {
    let cancelled = false

    api
      .categories()
      .then((data) => {
        if (!cancelled) {
          setCategories(data)
        }
      })
      .catch(() => setCategories([]))

    api
      .statement(statementId)
      .then((data) => {
        if (!cancelled) {
          setStatement(data)
          setOperations(data.operations)
          setError(null)
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(err instanceof ApiError ? err.message : 'Не удалось загрузить выписку')
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
  }, [statementId])

  useEffect(() => {
    if (!statement || statement.status !== 'Pending') {
      return
    }

    let cancelled = false
    const timer = window.setInterval(() => {
      api
        .statement(statement.id)
        .then((data) => {
          if (!cancelled) {
            setStatement(data)
            setOperations(data.operations)
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
  }, [statement])

  useEffect(() => {
    if (!statement || statement.confirmed || statement.status === 'Pending') {
      return
    }

    let cancelled = false

    api
      .statementMatches(statement.id)
      .then((rows) => {
        if (!cancelled) {
          setMatches(Object.fromEntries(rows.map((row) => [row.index, row.candidates])))
        }
      })
      .catch(() => {
        // сопоставление необязательно
      })

    return () => {
      cancelled = true
    }
  }, [statement])

  function openEditor(index: number) {
    const operation = operations[index]
    setEditIndex(index)
    setDirection(operation.direction)
    setAmount(String(operation.amount))
    setDescription(operation.description ?? '')
    setCategoryId(operation.categoryId ?? '')
    setIsTransfer(operation.isTransfer)
    setLinkId(operation.linkTransactionId ?? null)
  }

  function saveEditor() {
    if (editIndex === null) {
      return
    }

    const parsed = parseNumber(amount)

    if (!Number.isFinite(parsed) || parsed <= 0) {
      setError('Некорректная сумма.')
      return
    }

    setOperations((items) =>
      items.map((item, index) =>
        index === editIndex
          ? {
              ...item,
              direction,
              amount: parsed,
              description: description.trim() || null,
              categoryId: categoryId || null,
              categoryName: categoryId ? categories.find((c) => c.id === categoryId)?.name ?? null : null,
              isTransfer,
              linkTransactionId: linkId,
            }
          : item,
      ),
    )
    setEditIndex(null)
    setError(null)
  }

  function removeOperation() {
    if (editIndex === null) {
      return
    }

    setOperations((items) => items.filter((_, index) => index !== editIndex))
    setEditIndex(null)
  }

  async function handleConfirm() {
    if (!statement) {
      return
    }

    const payload = operations.filter((operation) => operation.amount > 0)

    if (payload.length === 0) {
      setError('Нет операций для проведения.')
      return
    }

    setConfirming(true)
    setError(null)

    try {
      await api.confirmStatement(statement.id, payload)
      haptic('success')
      celebrate()
      onChanged()
      onBack()
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось провести выписку')
      haptic('error')
      setConfirming(false)
    }
  }

  const grouped = operations.reduce<Record<string, Array<{ operation: StatementOperationDto; index: number }>>>(
    (acc, operation, index) => {
      const key = dayKey(operation.occurredAt)
      const bucket = acc[key] ?? []
      bucket.push({ operation, index })
      acc[key] = bucket
      return acc
    },
    {},
  )
  const dayKeys = Object.keys(grouped).sort((a, b) => b.localeCompare(a))

  const categoryOptions = categories.filter((category) => category.type === (direction === 'income' ? 'Income' : 'Expense'))
  const confirmed = statement?.confirmed ?? false

  return (
    <section className="screen">
      <Confetti trigger={celebration} />

      <header className="screen-header">
        <button className="ghost back" onClick={onBack} aria-label="Назад">
          <Icon name="chevron-left" size={20} strokeWidth={2.2} />
        </button>
        <h1>
          <span className="title-icon" style={{ '--cat': 'var(--cyan)' } as CSSProperties} aria-hidden="true">
            <Icon name="file" size={18} />
          </span>
          Выписка
        </h1>
        <span />
      </header>

      {loading && <Skeleton rows={4} />}
      {error && (
        <p className="error loading-row" aria-live="polite">
          <Icon name="alert" size={16} strokeWidth={2.1} />
          {error}
        </p>
      )}

      {statement && (
        <>
          <div className="status-row">
            <p className={`status ${confirmed ? 'confirmed' : ''}`}>
              {statement.status === 'Pending' && !confirmed && <span className="spinner" aria-hidden="true" />}
              {confirmed && <Icon name="check" size={13} strokeWidth={2.4} />}
              {confirmed ? 'Проведена' : STATUS_LABELS[statement.status] ?? statement.status}
            </p>
            <p className="status">
              {operations.length} {plural(operations.length, 'операция', 'операции', 'операций')}
            </p>
          </div>

          <p className="muted small">{statement.fileName}</p>

          {statement.duplicate && (
            <div className="match-card">
              <p className="small">
                <Icon name="info" size={15} strokeWidth={2.2} />
                Эта выписка уже загружалась ранее.
              </p>
              <p className="muted small">
                Если она уже проведена — повторно проводить не нужно. Иначе продолжите проверку и проведение.
              </p>
            </div>
          )}

          {statement.status === 'Pending' && (
            <p className="muted loading-row">
              <span className="spinner" aria-hidden="true" />
              Идёт разбор выписки — это может занять до минуты. Экран обновится сам.
            </p>
          )}

          {statement.status === 'Failed' && statement.error && (
            <p className="error loading-row small">
              <Icon name="alert" size={16} strokeWidth={2.1} />
              {statement.error}
            </p>
          )}

          {dayKeys.map((key) => (
            <div className="day-group" key={key}>
              <div className="day-header">
                <span>{formatDayLabel(grouped[key][0].operation.occurredAt)}</span>
              </div>

              <ul className="list perf">
                {grouped[key].map(({ operation, index }) => {
                  const visual = operation.isTransfer
                    ? { icon: 'swap' as const, color: '#f5b942' }
                    : categoryVisual(operation.categoryName, operation.direction === 'income' ? 'Income' : 'Expense')

                  return (
                    <li key={index}>
                      <button
                        className={`list-item ${operation.isTransfer ? 'is-neutral' : operation.direction === 'income' ? 'is-income' : 'is-expense'}`}
                        disabled={confirmed}
                        onClick={() => openEditor(index)}
                      >
                        <span className="list-badge" style={{ '--cat': visual.color } as CSSProperties} aria-hidden="true">
                          <Icon name={visual.icon} size={18} />
                        </span>
                        <span className="list-main">
                          <span className="list-title">{operation.description ?? 'Операция'}</span>
                          <span className="muted small">
                            {formatTime(operation.occurredAt)}
                            {operation.isTransfer ? ' · перевод' : ''}
                            {operation.linkTransactionId ? ' · связано' : ''}
                            {operation.categoryName ? ` · ${operation.categoryName}` : ''}
                            {operation.mcc ? ` · MCC ${operation.mcc}` : ''}
                          </span>
                        </span>
                        <span className="list-right">
                          <span className={operation.direction === 'income' ? 'amount income' : 'amount expense'}>
                            {operation.direction === 'income' ? '+' : '−'}
                            {formatMoney(operation.amount, operation.currency ?? 'BYN')}
                          </span>
                        </span>
                      </button>
                    </li>
                  )
                })}
              </ul>
            </div>
          ))}

          {!confirmed && operations.length > 0 && (
            <button className="primary" disabled={confirming} onClick={handleConfirm}>
              <Icon name="check" size={18} strokeWidth={2.2} />
              {confirming
                ? 'Проведение…'
                : `Провести ${operations.length} ${plural(operations.length, 'операцию', 'операции', 'операций')}`}
            </button>
          )}
        </>
      )}

      <BottomSheet open={editIndex !== null} title="Операция" onClose={() => setEditIndex(null)}>
        <div className="segmented">
          <button
            type="button"
            className={direction === 'expense' ? 'segment active' : 'segment'}
            onClick={() => setDirection('expense')}
          >
            <Icon name="arrow-down" size={16} strokeWidth={2.2} />
            Расход
          </button>
          <button
            type="button"
            className={direction === 'income' ? 'segment active' : 'segment'}
            onClick={() => setDirection('income')}
          >
            <Icon name="arrow-up" size={16} strokeWidth={2.2} />
            Доход
          </button>
        </div>

        <label className="field">
          <span>Сумма</span>
          <input inputMode="decimal" enterKeyHint="done" value={amount} onChange={(event) => setAmount(event.target.value)} />
        </label>

        <label className="field">
          <span>Описание</span>
          <input value={description} onChange={(event) => setDescription(event.target.value)} />
        </label>

        <label className="field">
          <span>Категория</span>
          <CategorySelect value={categoryId} onChange={setCategoryId} categories={categoryOptions} />
        </label>

        <div className="segmented">
          <button
            type="button"
            className={!isTransfer ? 'segment active' : 'segment'}
            onClick={() => setIsTransfer(false)}
          >
            Обычная
          </button>
          <button
            type="button"
            className={isTransfer ? 'segment active' : 'segment'}
            onClick={() => setIsTransfer(true)}
          >
            <Icon name="swap" size={16} strokeWidth={2.2} />
            Перевод
          </button>
        </div>

        <p className="muted small">Переводы между своими счетами и снятие наличных не учитываются в доходах и расходах.</p>

        {editIndex !== null && (matches[editIndex]?.length ?? 0) > 0 && (
          <>
            <h3 className="sheet-section">Похожие операции</h3>
            <ul className="list">
              {matches[editIndex].map((candidate) => (
                <li key={candidate.id} className="saved-row">
                  <button
                    type="button"
                    className={linkId === candidate.id ? 'saved-main linked' : 'saved-main'}
                    onClick={() => setLinkId(linkId === candidate.id ? null : candidate.id)}
                  >
                    <span className="list-title">{candidate.categoryName ?? candidate.comment ?? 'Операция'}</span>
                    <span className="muted small">
                      {formatDate(candidate.occurredAt)} ·{' '}
                      {candidate.source === 'Receipt'
                        ? 'чек'
                        : candidate.source === 'Statement'
                          ? 'выписка'
                          : 'вручную'}
                    </span>
                  </button>
                  <span className="amount">{formatMoney(candidate.amount, candidate.currency)}</span>
                </li>
              ))}
            </ul>
            <p className="muted small">
              {linkId
                ? 'Отмечено как уже существующая — новая операция не создаётся.'
                : 'Нажмите на похожую, чтобы не создавать дубль.'}
            </p>
          </>
        )}

        <div className="actions">
          <button type="button" className="danger" onClick={removeOperation}>
            <Icon name="trash" size={18} />
            Убрать
          </button>
          <button type="button" className="primary" onClick={saveEditor}>
            <Icon name="check" size={18} strokeWidth={2.2} />
            Сохранить
          </button>
        </div>
      </BottomSheet>
    </section>
  )
}
