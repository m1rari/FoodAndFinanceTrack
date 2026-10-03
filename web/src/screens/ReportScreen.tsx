import { useEffect, useMemo, useState } from 'react'
import type { CSSProperties } from 'react'
import { api, ApiError } from '../api/client'
import type { ReportSummaryDto, TransactionDto } from '../api/types'
import BottomSheet from '../components/BottomSheet'
import CountUp from '../components/CountUp'
import EmptyState from '../components/EmptyState'
import Icon from '../components/Icon'
import Skeleton from '../components/Skeleton'
import { customPeriod, dayKey, formatPeriodLabel, periodFor } from '../utils/date'
import type { Period, PeriodPreset } from '../utils/date'
import { formatMoney } from '../utils/format'
import { categoryVisual } from '../utils/visuals'
import { readUrlParam, writeUrlParams } from '../utils/url'

interface Props {
  refreshKey: number
}

export default function ReportScreen({ refreshKey }: Props) {
  const [period, setPeriod] = useState<Period>(() => {
    const preset = readUrlParam('rperiod')

    if (preset === 'today' || preset === 'week' || preset === 'month') {
      return periodFor(preset)
    }

    if (preset === 'custom') {
      const from = readUrlParam('rfrom')
      const to = readUrlParam('rto')

      if (from && to) {
        return customPeriod(from, to)
      }
    }

    return periodFor('month')
  })
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

  useEffect(() => {
    writeUrlParams({
      rperiod: period.preset,
      rfrom: period.preset === 'custom' ? dayKey(period.from) : null,
      rto: period.preset === 'custom' ? dayKey(period.to) : null,
    })
  }, [period])

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

  const typeTotals = useMemo(() => {
    const map = new Map<string, number>()

    for (const item of summary?.byCategory ?? []) {
      map.set(item.type, (map.get(item.type) ?? 0) + item.total)
    }

    return map
  }, [summary])

  const isEmpty = summary !== null && summary.byCategory.length === 0 && expenses.length === 0
  const net = (summary?.totalIncome ?? 0) - (summary?.totalExpense ?? 0)

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
        <h1>
          <span className="title-icon" aria-hidden="true">
            <Icon name="chart" size={18} />
          </span>
          Отчёты
        </h1>
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
          <Icon name="calendar" size={15} />
          {period.preset === 'custom' ? formatPeriodLabel(period) : 'Период…'}
        </button>
      </div>

      {loading && <Skeleton rows={3} />}
      {error && (
        <p className="error" aria-live="polite">
          {error}
        </p>
      )}

      {isEmpty && (
        <EmptyState
          art="chart"
          title="Нет данных за период"
          text="Отчёт появится, как только в выбранном периоде будут операции."
        />
      )}

      {summary && !isEmpty && (
        <>
          <div className="cards">
            <div className="card">
              <span className="stat-label">
                <Icon name="arrow-down" size={13} strokeWidth={2.4} />
                Расходы
              </span>
              <CountUp
                className="stat-value expense"
                value={summary.totalExpense}
                format={(value) => `−${formatMoney(value, summary.currency)}`}
              />
            </div>
            <div className="card">
              <span className="stat-label">
                <Icon name="arrow-up" size={13} strokeWidth={2.4} />
                Доходы
              </span>
              <CountUp
                className="stat-value income"
                value={summary.totalIncome}
                format={(value) => `+${formatMoney(value, summary.currency)}`}
              />
            </div>
          </div>

          <div className={`card net-card ${net >= 0 ? 'is-income' : 'is-expense'}`}>
            <span className="stat-label">
              <Icon name={net >= 0 ? 'sparkles' : 'info'} size={13} strokeWidth={2.2} />
              Итого за период
            </span>
            <CountUp
              className={`stat-value ${net >= 0 ? 'income' : 'expense'}`}
              value={Math.abs(net)}
              format={(value) => `${net >= 0 ? '+' : '−'}${formatMoney(value, summary.currency)}`}
            />
          </div>

          <h2 className="section-title">По категориям</h2>

          {summary.byCategory.length === 0 && <p className="muted">Нет данных за период.</p>}

          <ul className="bars">
            {summary.byCategory.map((item) => {
              const visual = categoryVisual(item.categoryName, item.type)
              const typeTotal = typeTotals.get(item.type) ?? item.total
              const share = Math.round((item.total / Math.max(1, typeTotal)) * 100)

              return (
                <li key={`${item.categoryId ?? 'none'}-${item.type}`} style={{ '--cat': visual.color } as CSSProperties}>
                  <div className="bar-head">
                    <span className="bar-name">
                      <span className="bar-icon" aria-hidden="true">
                        <Icon name={visual.icon} size={15} />
                      </span>
                      <span>
                        {item.categoryName ?? 'Без категории'}
                        {item.type === 'Income' ? ' · доход' : ''}
                      </span>
                    </span>
                    <span className="bar-values">
                      <span className="bar-share">{share}%</span>
                      <span className="amount">{formatMoney(item.total, summary.currency)}</span>
                    </span>
                  </div>
                  <div className="bar-track">
                    <div
                      className="bar-fill"
                      style={{ '--w': `${(item.total / maxCategory) * 100}%` } as CSSProperties}
                    />
                  </div>
                </li>
              )
            })}
          </ul>

          {topStores.length > 0 && (
            <>
              <h2 className="section-title">Топ магазинов</h2>
              <ul className="list">
                {topStores.map(([store, total]) => (
                  <li key={store} className="store-row">
                    <span className="bar-name">
                      <span className="bar-icon" style={{ '--cat': 'var(--brand)' } as CSSProperties} aria-hidden="true">
                        <Icon name="cart" size={15} />
                      </span>
                      <span>{store}</span>
                    </span>
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
