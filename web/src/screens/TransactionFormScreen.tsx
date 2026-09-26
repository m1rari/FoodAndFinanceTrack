import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { api, ApiError } from '../api/client'
import type { CategoryDto, TransactionDto } from '../api/types'
import { useMainButton } from '../hooks/useMainButton'
import { haptic, isMainButtonAvailable } from '../telegram/telegram'

interface Props {
  transaction: TransactionDto | null
  onDone: () => void
  onCancel: () => void
}

function toDateInput(value: string): string {
  return value.slice(0, 10)
}

function todayInput(): string {
  return new Date().toISOString().slice(0, 10)
}

export default function TransactionFormScreen({ transaction, onDone, onCancel }: Props) {
  const isEdit = transaction !== null
  const [type, setType] = useState(transaction?.type ?? 'Expense')
  const [amount, setAmount] = useState(transaction ? String(transaction.amount) : '')
  const [categoryId, setCategoryId] = useState(transaction?.categoryId ?? '')
  const [comment, setComment] = useState(transaction?.comment ?? '')
  const [date, setDate] = useState(transaction ? toDateInput(transaction.occurredAt) : todayInput())
  const [categories, setCategories] = useState<CategoryDto[]>([])
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    api
      .categories()
      .then((data) => {
        if (!cancelled) {
          setCategories(data)
        }
      })
      .catch(() => {
        if (!cancelled) {
          setCategories([])
        }
      })

    return () => {
      cancelled = true
    }
  }, [])

  const available = categories.filter((category) => category.type === type)
  const mainButtonAvailable = isMainButtonAvailable()

  async function save() {
    setError(null)

    const parsed = Number(amount.replace(',', '.'))

    if (!Number.isFinite(parsed) || parsed <= 0) {
      setError('Введите сумму больше нуля.')
      haptic('error')
      return
    }

    const occurredAt = new Date(`${date}T12:00:00.000Z`).toISOString()
    setSaving(true)

    try {
      if (transaction) {
        await api.updateTransaction(transaction.id, {
          amount: parsed,
          categoryId: categoryId || null,
          clearCategory: categoryId === '',
          occurredAt,
          comment: comment || null,
          clearComment: comment === '',
        })
      } else {
        await api.createTransaction({
          amount: parsed,
          type,
          categoryId: categoryId || null,
          occurredAt,
          comment: comment || null,
        })
      }

      haptic('success')
      onDone()
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось сохранить операцию')
      haptic('error')
    } finally {
      setSaving(false)
    }
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    void save()
  }

  useMainButton({
    text: isEdit ? 'Сохранить' : 'Добавить',
    visible: mainButtonAvailable,
    enabled: !saving,
    loading: saving,
    onClick: () => void save(),
  })

  return (
    <section className="screen">
      <header className="screen-header">
        <h1>{isEdit ? 'Редактирование' : 'Новая операция'}</h1>
      </header>

      <form className="form" onSubmit={handleSubmit}>
        {isEdit ? (
          <label className="field">
            <span>Тип</span>
            <div className="static-value">{type === 'Income' ? 'Доход' : 'Расход'}</div>
          </label>
        ) : (
          <div className="segmented">
            <button
              type="button"
              className={type === 'Expense' ? 'segment active' : 'segment'}
              onClick={() => {
                setType('Expense')
                setCategoryId('')
              }}
            >
              Расход
            </button>
            <button
              type="button"
              className={type === 'Income' ? 'segment active' : 'segment'}
              onClick={() => {
                setType('Income')
                setCategoryId('')
              }}
            >
              Доход
            </button>
          </div>
        )}

        <label className="field">
          <span>Сумма</span>
          <input
            inputMode="decimal"
            enterKeyHint="done"
            value={amount}
            onChange={(event) => setAmount(event.target.value)}
            placeholder="0.00"
          />
        </label>

        <label className="field">
          <span>Категория</span>
          <select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
            <option value="">Без категории</option>
            {available.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>Дата</span>
          <input type="date" value={date} onChange={(event) => setDate(event.target.value)} />
        </label>

        <label className="field">
          <span>Комментарий</span>
          <input value={comment} onChange={(event) => setComment(event.target.value)} placeholder="Необязательно" />
        </label>

        {error && <p className="error">{error}</p>}

        <div className="actions">
          <button type="button" className="ghost" onClick={onCancel}>
            Отмена
          </button>
          {!mainButtonAvailable && (
            <button type="submit" className="primary" disabled={saving}>
              {saving ? 'Сохранение…' : 'Сохранить'}
            </button>
          )}
        </div>
      </form>
    </section>
  )
}
