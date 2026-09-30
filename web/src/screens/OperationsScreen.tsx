import { useEffect, useMemo, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { CategoryDto, ReceiptSummaryDto, TransactionDto } from '../api/types'
import AddSheet from '../components/AddSheet'
import BottomSheet from '../components/BottomSheet'
import Skeleton from '../components/Skeleton'
import { customPeriod, dayKey, formatDayLabel, formatPeriodLabel, periodFor } from '../utils/date'
import type { Period, PeriodPreset } from '../utils/date'
import { formatMoney, plural } from '../utils/format'
import { readUrlParam, writeUrlParams } from '../utils/url'

interface Props {
  refreshKey: number
  onEdit: (transaction: TransactionDto) => void
  onOpenPurchase: (receiptId: string) => void
  onOpenStatement: (statementId: string) => void
  onManual: () => void
}

const STATUS_SHORT: Record<string, string> = {
  Pending: 'распознаётся…',
  Processed: 'нужно проверить',
  NeedsReview: 'требует проверки',
  Failed: 'ошибка распознавания',
}

interface PurchaseGroup {
  receiptId: string
  merchant: string | null
  count: number
  total: number
  currency: string
  categoryName: string | null
}

type Row = { kind: 'tx'; tx: TransactionDto } | { kind: 'purchase'; group: PurchaseGroup }

interface DayGroup {
  key: string
  label: string
  expense: number
  income: number
  currency: string
  rows: Row[]
}

function buildDays(items: TransactionDto[]): DayGroup[] {
  const purchases = new Map<string, PurchaseGroup>()

  for (const tx of items) {
    if (tx.type !== 'Expense' || !tx.receiptId) {
      continue
    }

    const group = purchases.get(tx.receiptId) ?? {
      receiptId: tx.receiptId,
      merchant: tx.receiptMerchantName,
      count: 0,
      total: 0,
      currency: tx.currency,
      categoryName: tx.categoryName,
    }

    group.count += 1
    group.total += tx.amount
    group.merchant = group.merchant ?? tx.receiptMerchantName
    group.categoryName = group.categoryName === tx.categoryName ? group.categoryName : null
    purchases.set(tx.receiptId, group)
  }

  const sorted = [...items].sort((a, b) => b.occurredAt.localeCompare(a.occurredAt))
  const days = new Map<string, DayGroup>()
  const addedPurchases = new Set<string>()

  for (const tx of sorted) {
    const key = dayKey(tx.occurredAt)
    let day = days.get(key)

    if (!day) {
      day = { key, label: formatDayLabel(tx.occurredAt), expense: 0, income: 0, currency: tx.currency, rows: [] }
      days.set(key, day)
    }

    if (!tx.isTransfer) {
      if (tx.type === 'Income') {
        day.income += tx.amount
      } else {
        day.expense += tx.amount
      }
    }

    if (tx.type === 'Expense' && tx.receiptId) {
      if (addedPurchases.has(tx.receiptId)) {
        continue
      }

      addedPurchases.add(tx.receiptId)
      day.rows.push({ kind: 'purchase', group: purchases.get(tx.receiptId)! })
    } else {
      day.rows.push({ kind: 'tx', tx })
    }
  }

  return [...days.values()].sort((a, b) => b.key.localeCompare(a.key))
}

export default function OperationsScreen({ refreshKey, onEdit, onOpenPurchase, onOpenStatement, onManual }: Props) {
  const [addOpen, setAddOpen] = useState(false)
  const [period, setPeriod] = useState<Period>(() => {
    const preset = readUrlParam('period')

    if (preset === 'today' || preset === 'week' || preset === 'month') {
      return periodFor(preset)
    }

    if (preset === 'custom') {
      const from = readUrlParam('from')
      const to = readUrlParam('to')

      if (from && to) {
        return customPeriod(from, to)
      }
    }

    return periodFor('month')
  })
  const [type, setType] = useState(() => readUrlParam('type') ?? '')
  const [categoryId, setCategoryId] = useState(() => readUrlParam('category') ?? '')
  const [customFrom, setCustomFrom] = useState('')
  const [customTo, setCustomTo] = useState('')
  const [items, setItems] = useState<TransactionDto[]>([])
  const [categories, setCategories] = useState<CategoryDto[]>([])
  const [receipts, setReceipts] = useState<ReceiptSummaryDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [filtersOpen, setFiltersOpen] = useState(false)

  useEffect(() => {
    api
      .categories()
      .then(setCategories)
      .catch(() => setCategories([]))
  }, [])

  useEffect(() => {
    let cancelled = false

    api
      .receipts(true)
      .then((data) => {
        if (!cancelled) {
          setReceipts(data)
        }
      })
      .catch(() => {
        // раздел необязателен
      })

    return () => {
      cancelled = true
    }
  }, [refreshKey])

  useEffect(() => {
    let cancelled = false

    api
      .transactions({
        from: period.from,
        to: period.to,
        type: type || undefined,
        categoryId: categoryId || undefined,
      })
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
  }, [period, type, categoryId, refreshKey])

  useEffect(() => {
    writeUrlParams({
      period: period.preset,
      from: period.preset === 'custom' ? dayKey(period.from) : null,
      to: period.preset === 'custom' ? dayKey(period.to) : null,
      type: type || null,
      category: categoryId || null,
    })
  }, [period, type, categoryId])

  const days = useMemo(() => buildDays(items), [items])
  const totals = useMemo(() => {
    let income = 0
    let expense = 0
    let currency = 'BYN'

    for (const tx of items) {
      currency = tx.currency

      if (tx.isTransfer) {
        continue
      }

      if (tx.type === 'Income') {
        income += tx.amount
      } else {
        expense += tx.amount
      }
    }

    return { income, expense, net: income - expense, currency }
  }, [items])
  const filtersActive = type !== '' || categoryId !== '' || period.preset === 'custom'

  function applyPreset(preset: PeriodPreset) {
    setPeriod(periodFor(preset))
  }

  function applyCustom() {
    if (!customFrom || !customTo) {
      return
    }

    setPeriod(customPeriod(customFrom, customTo))
    setFiltersOpen(false)
  }

  function resetFilters() {
    setType('')
    setCategoryId('')
    setCustomFrom('')
    setCustomTo('')
    setPeriod(periodFor('month'))
    setFiltersOpen(false)
  }

  const categoryOptions = categories.filter((category) => (type ? category.type === type : true))

  return (
    <section className="screen">
      <header className="screen-header">
        <h1>Операции</h1>
        <button className="primary" onClick={() => setAddOpen(true)}>
          + Добавить
        </button>
      </header>

      <div className="chips-row">
        <button className={period.preset === 'today' ? 'chip active' : 'chip'} onClick={() => applyPreset('today')}>
          Сегодня
        </button>
        <button className={period.preset === 'week' ? 'chip active' : 'chip'} onClick={() => applyPreset('week')}>
          Неделя
        </button>
        <button className={period.preset === 'month' ? 'chip active' : 'chip'} onClick={() => applyPreset('month')}>
          Месяц
        </button>
        <button
          className={filtersActive ? 'chip active' : 'chip'}
          onClick={() => setFiltersOpen(true)}
        >
          {period.preset === 'custom' ? formatPeriodLabel(period) : 'Фильтры'}
        </button>
      </div>

      {items.length > 0 && (
        <div className="stat-strip">
          <div className="stat">
            <span className="stat-label">Доход</span>
            <span className="stat-value income">+{formatMoney(totals.income, totals.currency)}</span>
          </div>
          <div className="stat">
            <span className="stat-label">Расход</span>
            <span className="stat-value expense">−{formatMoney(totals.expense, totals.currency)}</span>
          </div>
        </div>
      )}

      {receipts.length > 0 && (
        <div className="unconfirmed">
          <h2 className="section-title">Неразобранные покупки</h2>
          <ul className="list">
            {receipts.map((receipt) => (
              <li key={receipt.id}>
                <button className="list-item is-purchase" onClick={() => onOpenPurchase(receipt.id)}>
                  <span className="list-main">
                    <span className="list-title">{receipt.merchantName ?? 'Покупка'}</span>
                    <span className="muted small">
                      {receipt.itemCount} {plural(receipt.itemCount, 'товар', 'товара', 'товаров')}
                      {' · '}
                      {STATUS_SHORT[receipt.status] ?? receipt.status}
                    </span>
                  </span>
                  <span className="list-right">
                    <span className="amount expense">
                      {receipt.totalAmount != null ? `−${formatMoney(receipt.totalAmount)}` : '—'}
                    </span>
                    <span className="muted small">Открыть ›</span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {loading && <Skeleton rows={4} />}
      {error && (
        <p className="error" aria-live="polite">
          {error}
        </p>
      )}

      {!loading && !error && items.length === 0 && receipts.length === 0 && (
        <div className="empty">
          <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
            <path d="M5 3h14v18l-7-4-7 4z" />
          </svg>
          <p>За выбранный период операций нет.</p>
          <button className="primary" onClick={() => setAddOpen(true)}>
            Добавить операцию
          </button>
        </div>
      )}

      {days.map((day) => (
        <div className="day-group" key={day.key}>
          <div className="day-header">
            <span>{day.label}</span>
            <span className="day-totals">
              {day.expense > 0 && <span className="expense">−{formatMoney(day.expense, day.currency)}</span>}
              {day.income > 0 && <span className="income">+{formatMoney(day.income, day.currency)}</span>}
            </span>
          </div>

          <ul className="list perf">
            {day.rows.map((row) =>
              row.kind === 'purchase' ? (
                <li key={`p-${row.group.receiptId}`}>
                  <button className="list-item is-purchase" onClick={() => onOpenPurchase(row.group.receiptId)}>
                    <span className="list-main">
                      <span className="list-title">{row.group.merchant ?? 'Покупка'}</span>
                      <span className="muted small">
                        {row.group.count} {plural(row.group.count, 'товар', 'товара', 'товаров')}
                        {row.group.categoryName ? ` · ${row.group.categoryName}` : ''}
                      </span>
                    </span>
                    <span className="list-right">
                      <span className="amount expense">−{formatMoney(row.group.total, row.group.currency)}</span>
                      <span className="muted small">Чек ›</span>
                    </span>
                  </button>
                </li>
              ) : (
                <li key={row.tx.id}>
                  <button
                    className={`list-item ${
                      row.tx.isTransfer ? 'is-neutral' : row.tx.type === 'Income' ? 'is-income' : 'is-expense'
                    }`}
                    onClick={() => onEdit(row.tx)}
                  >
                    <span className="list-main">
                      <span className="list-title">{row.tx.categoryName ?? 'Без категории'}</span>
                      <span className="muted small">
                        {row.tx.comment ? `${row.tx.comment} · ` : ''}
                        {row.tx.isTransfer
                          ? 'перевод'
                          : row.tx.source === 'Receipt'
                            ? 'чек'
                            : row.tx.source === 'Statement'
                              ? 'выписка'
                              : 'вручную'}
                      </span>
                    </span>
                    <span className="list-right">
                      <span className={row.tx.type === 'Income' ? 'amount income' : 'amount expense'}>
                        {row.tx.type === 'Income' ? '+' : '−'}
                        {formatMoney(row.tx.amount, row.tx.currency)}
                      </span>
                      <span className="muted small">Изменить</span>
                    </span>
                  </button>
                </li>
              ),
            )}
          </ul>
        </div>
      ))}

      <BottomSheet open={filtersOpen} title="Фильтры" onClose={() => setFiltersOpen(false)}>
        <div className="segmented">
          <button className={type === '' ? 'segment active' : 'segment'} onClick={() => setType('')}>
            Все
          </button>
          <button className={type === 'Expense' ? 'segment active' : 'segment'} onClick={() => setType('Expense')}>
            Расход
          </button>
          <button className={type === 'Income' ? 'segment active' : 'segment'} onClick={() => setType('Income')}>
            Доход
          </button>
        </div>

        <label className="field">
          <span>Категория</span>
          <select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
            <option value="">Все категории</option>
            {categoryOptions.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </label>

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

        <div className="actions">
          <button type="button" className="ghost" onClick={resetFilters}>
            Сбросить
          </button>
          <button type="button" className="primary" onClick={applyCustom} disabled={!customFrom || !customTo}>
            Применить период
          </button>
        </div>
      </BottomSheet>

      <AddSheet
        open={addOpen}
        onClose={() => setAddOpen(false)}
        onManual={() => {
          setAddOpen(false)
          onManual()
        }}
        onReceipt={onOpenPurchase}
        onStatement={onOpenStatement}
      />
    </section>
  )
}
