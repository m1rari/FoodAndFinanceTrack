import { useEffect, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { ReportSummaryDto } from '../api/types'
import { currentMonthRange, formatMoney } from '../utils/format'

interface Props {
  refreshKey: number
}

export default function DashboardScreen({ refreshKey }: Props) {
  const [summary, setSummary] = useState<ReportSummaryDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    const { from, to } = currentMonthRange()

    api
      .reportSummary(from, to)
      .then((data) => {
        if (!cancelled) {
          setSummary(data)
          setError(null)
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(err instanceof ApiError ? err.message : 'Не удалось загрузить отчёт')
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

  if (loading) {
    return <p className="muted">Загрузка…</p>
  }

  if (error || !summary) {
    return <p className="error">{error ?? 'Нет данных'}</p>
  }

  const expenses = summary.byCategory.filter((item) => item.type === 'Expense')
  const maxTotal = expenses.reduce((max, item) => Math.max(max, item.total), 0)

  return (
    <section className="screen">
      <header className="screen-header">
        <h1>Отчёт за месяц</h1>
      </header>

      <div className="cards">
        <div className="card">
          <span className="muted small">Расходы</span>
          <strong className="expense">{formatMoney(summary.totalExpense, summary.currency)}</strong>
        </div>
        <div className="card">
          <span className="muted small">Доходы</span>
          <strong className="income">{formatMoney(summary.totalIncome, summary.currency)}</strong>
        </div>
      </div>

      <h2 className="section-title">По категориям</h2>

      {expenses.length === 0 && <p className="muted">Расходов за месяц нет.</p>}

      <ul className="bars">
        {expenses.map((item) => (
          <li key={item.categoryId ?? 'none'}>
            <div className="bar-head">
              <span>{item.categoryName ?? 'Без категории'}</span>
              <span>{formatMoney(item.total, summary.currency)}</span>
            </div>
            <div className="bar-track">
              <div
                className="bar-fill"
                style={{ width: maxTotal > 0 ? `${(item.total / maxTotal) * 100}%` : '0%' }}
              />
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}
