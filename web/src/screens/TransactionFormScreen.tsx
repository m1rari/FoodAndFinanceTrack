import { useEffect, useState } from 'react'
import type { CSSProperties, FormEvent } from 'react'
import { api, ApiError } from '../api/client'
import type { CategoryDto, TransactionDto } from '../api/types'
import BottomSheet from '../components/BottomSheet'
import CategorySelect from '../components/CategorySelect'
import Confetti from '../components/Confetti'
import Icon from '../components/Icon'
import { useCelebration } from '../hooks/useCelebration'
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
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [deleting, setDeleting] = useState(false)
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
      celebrate()
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

  async function handleDelete() {
    if (!transaction) {
      return
    }

    setDeleting(true)
    setError(null)

    try {
      await api.deleteTransaction(transaction.id)
      haptic('success')
      onDone()
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось удалить операцию')
      haptic('error')
      setDeleting(false)
    }
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
      <Confetti trigger={celebration} />

      <header className="screen-header">
        <h1>
          <span
            className="title-icon"
            style={{ '--cat': isEdit ? 'var(--violet)' : 'var(--brand)' } as CSSProperties}
            aria-hidden="true"
          >
            <Icon name={isEdit ? 'pencil' : 'plus'} size={18} />
          </span>
          {isEdit ? 'Редактирование' : 'Новая операция'}
        </h1>
      </header>

      <form className="form" onSubmit={handleSubmit}>
        {isEdit ? (
          <label className="field">
            <span>Тип</span>
            <div className="static-value">
              <Icon name={type === 'Income' ? 'arrow-up' : 'arrow-down'} size={16} strokeWidth={2.2} />
              {type === 'Income' ? 'Доход' : 'Расход'}
            </div>
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
              <Icon name="arrow-down" size={16} strokeWidth={2.2} />
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
              <Icon name="arrow-up" size={16} strokeWidth={2.2} />
              Доход
            </button>
          </div>
        )}

        <label className="field">
          <span>Сумма</span>
          <input
            name="amount"
            autoComplete="off"
            inputMode="decimal"
            enterKeyHint="done"
            value={amount}
            onChange={(event) => setAmount(event.target.value)}
            placeholder="0.00"
          />
        </label>

        <label className="field">
          <span>Категория</span>
          <CategorySelect name="category" value={categoryId} onChange={setCategoryId} categories={available} />
        </label>

        <label className="field">
          <span>Дата</span>
          <input type="date" name="date" value={date} onChange={(event) => setDate(event.target.value)} />
        </label>

        <label className="field">
          <span>Комментарий</span>
          <input
            name="comment"
            autoComplete="off"
            value={comment}
            onChange={(event) => setComment(event.target.value)}
            placeholder="Необязательно"
          />
        </label>

        {error && (
          <p className="error loading-row">
            <Icon name="alert" size={16} strokeWidth={2.1} />
            {error}
          </p>
        )}

        <div className="actions">
          <button type="button" className="ghost" onClick={onCancel}>
            Отмена
          </button>
          {!mainButtonAvailable && (
            <button type="submit" className="primary" disabled={saving}>
              <Icon name="check" size={18} strokeWidth={2.2} />
              {saving ? 'Сохранение…' : 'Сохранить'}
            </button>
          )}
        </div>
      </form>

      {isEdit && (
        <button className="danger" onClick={() => setDeleteOpen(true)}>
          <Icon name="trash" size={18} />
          Удалить операцию
        </button>
      )}

      <BottomSheet open={deleteOpen} title="Удалить операцию?" onClose={() => setDeleteOpen(false)}>
        <p className="muted">Операция будет удалена безвозвратно.</p>
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
