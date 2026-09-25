import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { api, ApiError, fetchReceiptImage } from '../api/client'
import type { CategoryDto, ReceiptDto, ReceiptItemDto, TransactionDto } from '../api/types'
import BottomSheet from '../components/BottomSheet'
import { formatDate, formatMoney } from '../utils/format'

interface Props {
  receiptId: string
  onBack: () => void
  onChanged: () => void
}

const STATUS_LABELS: Record<string, string> = {
  Pending: 'Анализ…',
  Processed: 'Готово',
  NeedsReview: 'Проверьте позиции',
  Failed: 'Не удалось распознать',
}

function parseNumber(value: string): number {
  return Number(value.replace(',', '.'))
}

export default function PurchaseScreen({ receiptId, onBack, onChanged }: Props) {
  const [receipt, setReceipt] = useState<ReceiptDto | null>(null)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [categories, setCategories] = useState<CategoryDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [confirming, setConfirming] = useState(false)
  const [matches, setMatches] = useState<TransactionDto[]>([])
  const [matchesDismissed, setMatchesDismissed] = useState(false)

  const [editorOpen, setEditorOpen] = useState(false)
  const [editingItem, setEditingItem] = useState<ReceiptItemDto | null>(null)
  const [name, setName] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [price, setPrice] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    let cancelled = false

    api
      .categories('Expense')
      .then((data) => {
        if (!cancelled) {
          setCategories(data)
        }
      })
      .catch(() => setCategories([]))

    api
      .receipt(receiptId)
      .then((data) => {
        if (!cancelled) {
          setReceipt(data)
          setError(null)
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(err instanceof ApiError ? err.message : 'Не удалось загрузить чек')
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
  }, [receiptId])

  useEffect(() => {
    if (!receipt || receipt.status !== 'Pending') {
      return
    }

    let cancelled = false
    const timer = window.setInterval(() => {
      api
        .receipt(receipt.id)
        .then((data) => {
          if (!cancelled) {
            setReceipt(data)
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
  }, [receipt])

  useEffect(() => {
    if (!receipt) {
      return
    }

    let cancelled = false
    let objectUrl: string | null = null

    fetchReceiptImage(receipt.imageUrl)
      .then((blob) => {
        if (!cancelled) {
          objectUrl = URL.createObjectURL(blob)
          setPreviewUrl(objectUrl)
        }
      })
      .catch(() => setPreviewUrl(null))

    return () => {
      cancelled = true
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl)
      }
    }
  }, [receipt])

  useEffect(() => {
    if (!receipt || receipt.confirmed || receipt.status === 'Pending') {
      return
    }

    let cancelled = false

    api
      .receiptMatches(receipt.id)
      .then((data) => {
        if (!cancelled) {
          setMatches(data)
        }
      })
      .catch(() => {
        // сопоставление необязательно
      })

    return () => {
      cancelled = true
    }
  }, [receipt])

  async function handleLink(transactionId: string) {
    if (!receipt) {
      return
    }

    setError(null)

    try {
      const linked = await api.linkReceipt(receipt.id, transactionId)
      setReceipt(linked)
      setMatches([])
      onChanged()
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось прикрепить чек')
    }
  }

  function openEditor(item: ReceiptItemDto | null) {
    setEditingItem(item)
    setName(item?.name ?? '')
    setQuantity(item ? String(item.quantity) : '1')
    setPrice(item ? String(item.unitPrice) : '')
    setCategoryId(item?.categoryId ?? '')
    setEditorOpen(true)
  }

  async function handleSaveItem(event: FormEvent) {
    event.preventDefault()

    if (!receipt) {
      return
    }

    const parsedQuantity = parseNumber(quantity)
    const parsedPrice = parseNumber(price)

    if (!name.trim()) {
      setError('Введите название товара.')
      return
    }

    if (!Number.isFinite(parsedQuantity) || parsedQuantity <= 0) {
      setError('Количество должно быть больше нуля.')
      return
    }

    if (!Number.isFinite(parsedPrice) || parsedPrice < 0) {
      setError('Некорректная цена.')
      return
    }

    setSaving(true)
    setError(null)

    try {
      const updated = editingItem
        ? await api.updateReceiptItem(receipt.id, editingItem.id, {
            name,
            quantity: parsedQuantity,
            unitPrice: parsedPrice,
            categoryId: categoryId || null,
            clearCategory: categoryId === '',
          })
        : await api.addReceiptItem(receipt.id, {
            name,
            quantity: parsedQuantity,
            unitPrice: parsedPrice,
            categoryId: categoryId || null,
          })

      setReceipt(updated)
      setEditorOpen(false)
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось сохранить товар')
    } finally {
      setSaving(false)
    }
  }

  async function handleConfirm() {
    if (!receipt) {
      return
    }

    setConfirming(true)
    setError(null)

    try {
      const confirmed = await api.confirmReceipt(receipt.id)
      setReceipt(confirmed)
      onChanged()
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось провести покупку')
    } finally {
      setConfirming(false)
    }
  }

  return (
    <section className="screen">
      <header className="screen-header">
        <button className="ghost back" onClick={onBack}>
          ‹ Назад
        </button>
        <h1>Покупка</h1>
        <span />
      </header>

      {loading && <p className="muted">Загрузка…</p>}
      {error && <p className="error">{error}</p>}

      {receipt && (
        <>
          <div className="status-row">
            <p className="status">{STATUS_LABELS[receipt.status] ?? receipt.status}</p>
            {receipt.confirmed && <p className="status confirmed">В операциях</p>}
          </div>

          {previewUrl && <img className="receipt-image" src={previewUrl} alt="Чек" />}

          <div className="purchase-head">
            <div>
              <strong>{receipt.merchantName ?? 'Покупка'}</strong>
              <div className="muted small">
                {receipt.purchaseDate ? formatDate(receipt.purchaseDate) : formatDate(receipt.createdAt)}
              </div>
            </div>
            <span className="amount expense">−{formatMoney(receipt.totalAmount ?? 0)}</span>
          </div>

          {!receipt.confirmed && receipt.status !== 'Pending' && !matchesDismissed && matches.length > 0 && (
            <div className="match-card">
              <p className="small">Похоже, эта покупка уже добавлена вручную:</p>
              {matches.map((match) => (
                <div className="match-row" key={match.id}>
                  <div>
                    <strong>{formatMoney(match.amount, match.currency)}</strong>
                    <div className="muted small">
                      {formatDate(match.occurredAt)}
                      {match.comment ? ` · ${match.comment}` : ''}
                    </div>
                  </div>
                  <button className="primary small-button" onClick={() => handleLink(match.id)}>
                    Прикрепить
                  </button>
                </div>
              ))}
              <button
                className="ghost small-button"
                onClick={() => {
                  setMatches([])
                  setMatchesDismissed(true)
                }}
              >
                Это разные операции
              </button>
            </div>
          )}

          <h2 className="section-title">Товары</h2>

          {receipt.items.length === 0 && receipt.status !== 'Pending' && (
            <p className="muted">Товаров нет — добавьте вручную.</p>
          )}

          <ul className="list">
            {receipt.items.map((item) => (
              <li key={item.id}>
                <button className="list-item" disabled={receipt.confirmed} onClick={() => openEditor(item)}>
                  <span className="list-main">
                    <span className="list-title">{item.name}</span>
                    <span className="muted small">
                      {item.quantity} × {formatMoney(item.unitPrice)}
                      {item.categoryName ? ` · ${item.categoryName}` : ''}
                      {item.confidence !== null && item.confidence < 0.6 ? ' · низкая уверенность' : ''}
                    </span>
                  </span>
                  <span className="list-right">
                    <span className="amount">{formatMoney(item.totalPrice)}</span>
                    {!receipt.confirmed && <span className="muted small">Изменить</span>}
                  </span>
                </button>
              </li>
            ))}
          </ul>

          {!receipt.confirmed && receipt.status !== 'Pending' && (
            <button className="ghost" onClick={() => openEditor(null)}>
              + Добавить товар
            </button>
          )}

          {!receipt.confirmed && receipt.items.length > 0 && receipt.status !== 'Pending' && (
            <button className="primary" disabled={confirming} onClick={handleConfirm}>
              {confirming ? 'Проведение…' : `Провести покупку · ${formatMoney(receipt.totalAmount ?? 0)}`}
            </button>
          )}
        </>
      )}

      <BottomSheet
        open={editorOpen}
        title={editingItem ? 'Товар' : 'Новый товар'}
        onClose={() => setEditorOpen(false)}
      >
        <form className="form" onSubmit={handleSaveItem}>
          <label className="field">
            <span>Название</span>
            <input value={name} onChange={(event) => setName(event.target.value)} enterKeyHint="done" />
          </label>

          <div className="item-grid">
            <label className="field">
              <span>Кол-во</span>
              <input
                inputMode="decimal"
                enterKeyHint="done"
                value={quantity}
                onChange={(event) => setQuantity(event.target.value)}
              />
            </label>
            <label className="field">
              <span>Цена</span>
              <input
                inputMode="decimal"
                enterKeyHint="done"
                value={price}
                onChange={(event) => setPrice(event.target.value)}
                placeholder="0.00"
              />
            </label>
          </div>

          <label className="field">
            <span>Категория</span>
            <select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
              <option value="">Без категории</option>
              {categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
          </label>

          <div className="actions">
            <button type="button" className="ghost" onClick={() => setEditorOpen(false)}>
              Отмена
            </button>
            <button type="submit" className="primary" disabled={saving}>
              {saving ? 'Сохранение…' : 'Сохранить'}
            </button>
          </div>
        </form>
      </BottomSheet>
    </section>
  )
}
