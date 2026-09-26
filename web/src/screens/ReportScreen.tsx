import { useEffect, useMemo, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { ReportSummaryDto, TransactionDto } from '../api/types'
import BottomSheet from '../components/BottomSheet'
import { customPeriod, formatPeriodLabel, periodFor } from '../utils/date'
import type { Period, PeriodPreset } from '../utils/date'
import { formatMoney } from '../utils/format'

interface Props {
  refreshKey: number
}

export default function ReportScreen({ refreshKey }: Props) {
  const [period, setPeriod] = useState<Period>(() => periodFor('month'))
  const [summary, setSummary] = useState<ReportSummaryDto | null>(null)
  const [expenses, setExpenses] = useState<TransactionDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [sheetOpen, setSheetOpen] = useState(false)
  const [customFrom, setCustomFrom] = useState('')
  const [customTo, setCustomTo] = useState('')

  useEffect(() => {
    let cancelled = false

    Promise.all([
      api.reportSummary(period.from, period.to),
      api.transactions({ from: period.from, to: period.to, type: 'Expense' }),
    ])
      .then(([summaryData, expenseData]) => {
        if (!cancelled) {
          setSummary(summaryData)
          setExpenses(expenseData)
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
  }, [period, refreshKey])

  const topStores = useMemo(() => {
    const map = new Map<string, number>()

    for (const tx of expenses) {
      if (tx.isTransfer || !tx.receiptMerchantName) {
        continue
      }

      map.set(tx.receiptMerchantName, (map.get(tx.receiptMerchantName) ?? 0) + tx.amount)
    }

    return [...map.entries()].sort((a, b) => b[1] - a[1]).slice(0, 5)
  }, [expenses])

  const maxCategory = Math.max(1, ...(summary?.byCategory.map((item) => item.total) ?? [1]))

  function applyPreset(preset: PeriodPreset) {
    setPeriod(periodFor(preset))
  }

  function applyCustom() {
    if (!customFrom || !customTo) {
      return
    }

    setPeriod(customPeriod(customFrom, customTo))
    setSheetOpen(false)
  }

  return (
    <section className="screen">
      <header className="screen-header">
        <h1>Отчёты</h1>
      </header>

      <div className="chips-row">
        <button className={period.preset === 'today' ? 'chip active' : 'chip'} onClick={() => applyPreset('today')}>
          День
        </button>
        <button className={period.preset === 'week' ? 'chip active' : 'chip'} onClick={() => applyPreset('week')}>
          Неделя
        </button>
        <button className={period.preset === 'month' ? 'chip active' : 'chip'} onClick={() => applyPreset('month')}>
          Месяц
        </button>
        <button className={period.preset === 'custom' ? 'chip active' : 'chip'} onClick={() => setSheetOpen(true)}>
          {period.preset === 'custom' ? formatPeriodLabel(period) : 'Период…'}
        </button>
      </div>

      {loading && <p className="muted">Загрузка…</p>}
      {error && <p className="error">{error}</p>}

      {summary && (
        <>
          <div className="cards">
            <div className="card">
              <span className="muted small">Расходы</span>
              <strong className="expense">−{formatMoney(summary.totalExpense, summary.currency)}</strong>
            </div>
            <div className="card">
              <span className="muted small">Доходы</span>
              <strong className="income">+{formatMoney(summary.totalIncome, summary.currency)}</strong>
            </div>
          </div>

          <h2 className="section-title">По категориям</h2>

          {summary.byCategory.length === 0 && <p className="muted">Нет данных за период.</p>}

          <ul className="bars">
            {summary.byCategory.map((item) => (
              <li key={`${item.categoryId ?? 'none'}-${item.type}`}>
                <div className="bar-head">
                  <span>
                    {item.categoryName ?? 'Без категории'}
                    {item.type === 'Income' ? ' (доход)' : ''}
                  </span>
                  <span className="amount">{formatMoney(item.total, summary.currency)}</span>
                </div>
                <div className="bar-track">
                  <div className="bar-fill" style={{ width: `${(item.total / maxCategory) * 100}%` }} />
                </div>
              </li>
            ))}
          </ul>

          {topStores.length > 0 && (
            <>
              <h2 className="section-title">Топ магазинов</h2>
              <ul className="list">
                {topStores.map(([store, total]) => (
                  <li key={store} className="store-row">
                    <span>{store}</span>
                    <span className="amount expense">−{formatMoney(total, summary.currency)}</span>
                  </li>
                ))}
              </ul>
            </>
          )}
        </>
      )}

      <BottomSheet open={sheetOpen} title="Период" onClose={() => setSheetOpen(false)}>
        <div className="item-grid">
          <label className="field">
            <span>С</span>
            <input type="date" value={customFrom} onChange={(event) => setCustomFrom(event.target.value)} />
          </label>
          <label className="field">
            <span>По</span>
            <input type="date" value={customTo} onChange={(event) => setCustomTo(event.target.value)} />
          </label>
        </div>

        <button className="primary" onClick={applyCustom} disabled={!customFrom || !customTo}>
          Применить
        </button>
      </BottomSheet>
    </section>
  )
}
