import { useEffect, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { CategoryDto, StatementDto, StatementOperationDto } from '../api/types'
import BottomSheet from '../components/BottomSheet'
import { haptic } from '../telegram/telegram'
import { dayKey, formatDayLabel } from '../utils/date'
import { formatMoney, formatTime, plural } from '../utils/format'

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
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [confirming, setConfirming] = useState(false)

  const [editIndex, setEditIndex] = useState<number | null>(null)
  const [direction, setDirection] = useState('expense')
  const [amount, setAmount] = useState('')
  const [description, setDescription] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [isTransfer, setIsTransfer] = useState(false)

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

  function openEditor(index: number) {
    const operation = operations[index]
    setEditIndex(index)
    setDirection(operation.direction)
    setAmount(String(operation.amount))
    setDescription(operation.description ?? '')
    setCategoryId(operation.categoryId ?? '')
    setIsTransfer(operation.isTransfer)
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
      onChanged()
      onBack()
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось провести выписку')
      haptic('error')
      setConfirming(false)
    }
  }

  const grouped = operations.reduce<Record<string, StatementOperationDto[]>>((acc, operation) => {
    const key = dayKey(operation.occurredAt)
    acc[key] = acc[key] ?? []
    acc[key].push(operation)
    return acc
  }, {})
  const dayKeys = Object.keys(grouped).sort((a, b) => b.localeCompare(a))

  const categoryOptions = categories.filter((category) => category.type === (direction === 'income' ? 'Income' : 'Expense'))
  const confirmed = statement?.confirmed ?? false

  return (
    <section className="screen">
      <header className="screen-header">
        <button className="ghost back" onClick={onBack}>
          ‹ Назад
        </button>
        <h1>Выписка</h1>
        <span />
      </header>

      {loading && <p className="muted">Загрузка…</p>}
      {error && <p className="error">{error}</p>}

      {statement && (
        <>
          <div className="status-row">
            <p className="status">{confirmed ? 'Проведена' : STATUS_LABELS[statement.status] ?? statement.status}</p>
            <p className="status">
              {operations.length} {plural(operations.length, 'операция', 'операции', 'операций')}
            </p>
          </div>

          <p className="muted small">{statement.fileName}</p>

          {statement.status === 'Failed' && statement.error && <p className="error small">{statement.error}</p>}

          {dayKeys.map((key) => (
            <div className="day-group" key={key}>
              <div className="day-header">
                <span>{formatDayLabel(grouped[key][0].occurredAt)}</span>
              </div>

              <ul className="list">
                {grouped[key].map((operation) => {
                  const index = operations.indexOf(operation)

                  return (
                    <li key={`${operation.occurredAt}-${index}`}>
                      <button
                        className={`list-item ${operation.isTransfer ? 'is-neutral' : operation.direction === 'income' ? 'is-income' : 'is-expense'}`}
                        disabled={confirmed}
                        onClick={() => openEditor(index)}
                      >
                        <span className="list-main">
                          <span className="list-title">{operation.description ?? 'Операция'}</span>
                          <span className="muted small">
                            {formatTime(operation.occurredAt)}
                            {operation.isTransfer ? ' · перевод' : ''}
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
              {confirming ? 'Проведение…' : `Провести ${operations.length} ${plural(operations.length, 'операцию', 'операции', 'операций')}`}
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
            Расход
          </button>
          <button
            type="button"
            className={direction === 'income' ? 'segment active' : 'segment'}
            onClick={() => setDirection('income')}
          >
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
          <select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
            <option value="">Без категории</option>
            {categoryOptions.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
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
            Перевод
          </button>
        </div>

        <p className="muted small">Переводы между своими счетами и снятие наличных не учитываются в доходах и расходах.</p>

        <div className="actions">
          <button type="button" className="danger" onClick={removeOperation}>
            Убрать
          </button>
          <button type="button" className="primary" onClick={saveEditor}>
            Сохранить
          </button>
        </div>
      </BottomSheet>
    </section>
  )
}
