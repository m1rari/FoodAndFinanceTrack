import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { api, ApiError } from '../api/client'
import type { CategoryDto } from '../api/types'

interface Props {
  onDone: () => void
  onCancel: () => void
}

export default function AddTransactionScreen({ onDone, onCancel }: Props) {
  const [type, setType] = useState('Expense')
  const [amount, setAmount] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [comment, setComment] = useState('')
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

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    const parsed = Number(amount.replace(',', '.'))

    if (!Number.isFinite(parsed) || parsed <= 0) {
      setError('Введите сумму больше нуля.')
      return
    }

    setSaving(true)

    try {
      await api.createTransaction({
        amount: parsed,
        type,
        categoryId: categoryId || null,
        comment: comment || null,
      })
      onDone()
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось сохранить операцию')
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="screen">
      <header className="screen-header">
        <h1>Новая операция</h1>
      </header>

      <form className="form" onSubmit={handleSubmit}>
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

        <label className="field">
          <span>Сумма</span>
          <input
            inputMode="decimal"
            value={amount}
            onChange={(event) => setAmount(event.target.value)}
            placeholder="0.00"
            autoFocus
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
          <span>Комментарий</span>
          <input value={comment} onChange={(event) => setComment(event.target.value)} placeholder="Необязательно" />
        </label>

        {error && <p className="error">{error}</p>}

        <div className="actions">
          <button type="button" className="ghost" onClick={onCancel}>
            Отмена
          </button>
          <button type="submit" className="primary" disabled={saving}>
            {saving ? 'Сохранение…' : 'Сохранить'}
          </button>
        </div>
      </form>
    </section>
  )
}
