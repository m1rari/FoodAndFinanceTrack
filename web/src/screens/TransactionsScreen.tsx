import { useEffect, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { TransactionDto } from '../api/types'
import { currentMonthRange, formatDate, formatMoney } from '../utils/format'

interface Props {
  refreshKey: number
  onAdd: () => void
  onEdit: (transaction: TransactionDto) => void
}

export default function TransactionsScreen({ refreshKey, onAdd, onEdit }: Props) {
  const [items, setItems] = useState<TransactionDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    const { from, to } = currentMonthRange()

    api
      .transactions({ from, to })
      .then((data) => {
        if (!cancelled) {
          setItems(data)
          setError(null)
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(err instanceof ApiError ? err.message : 'Не удалось загрузить операции')
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
  }, [refreshKey])

  return (
    <section className="screen">
      <header className="screen-header">
        <h1>Операции</h1>
        <button className="primary" onClick={onAdd}>
          + Добавить
        </button>
      </header>

      {loading && <p className="muted">Загрузка…</p>}
      {error && <p className="error">{error}</p>}

      {!loading && !error && items.length === 0 && (
        <p className="muted">За этот месяц операций пока нет.</p>
      )}

      <ul className="list">
        {items.map((item) => (
          <li key={item.id}>
            <button className="list-item" onClick={() => onEdit(item)} aria-label="Редактировать операцию">
              <span className="list-main">
                <span className="list-title">{item.categoryName ?? 'Без категории'}</span>
                <span className="muted small">
                  {formatDate(item.occurredAt)}
                  {item.comment ? ` · ${item.comment}` : ''}
                </span>
              </span>
              <span className="list-right">
                <span className={item.type === 'Income' ? 'amount income' : 'amount expense'}>
                  {item.type === 'Income' ? '+' : '−'}
                  {formatMoney(item.amount, item.currency)}
                </span>
                <span className="muted small">Изменить</span>
              </span>
            </button>
          </li>
        ))}
      </ul>
    </section>
  )
}
