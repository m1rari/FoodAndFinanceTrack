import { useEffect, useRef, useState } from 'react'
import type { ChangeEvent, FormEvent } from 'react'
import { api, ApiError, fetchReceiptImage } from '../api/client'
import type { CategoryDto, ReceiptDto, ReceiptItemDto } from '../api/types'
import { compressImage } from '../utils/image'
import { formatMoney } from '../utils/format'

const STATUS_LABELS: Record<string, string> = {
  Pending: 'Ожидает обработки',
  Processed: 'Обработан',
  NeedsReview: 'Требует ручного разбора',
  Failed: 'Ошибка обработки',
}

function parseNumber(value: string): number {
  return Number(value.replace(',', '.'))
}

function ReceiptItemCard({
  receiptId,
  item,
  categories,
  onUpdated,
}: {
  receiptId: string
  item: ReceiptItemDto
  categories: CategoryDto[]
  onUpdated: (receipt: ReceiptDto) => void
}) {
  const [name, setName] = useState(item.name)
  const [quantity, setQuantity] = useState(String(item.quantity))
  const [unitPrice, setUnitPrice] = useState(String(item.unitPrice))
  const [categoryId, setCategoryId] = useState(item.categoryId ?? '')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSave(event: FormEvent) {
    event.preventDefault()
    setError(null)

    const parsedQuantity = parseNumber(quantity)
    const parsedPrice = parseNumber(unitPrice)

    if (!Number.isFinite(parsedQuantity) || parsedQuantity <= 0) {
      setError('Количество должно быть больше нуля.')
      return
    }

    if (!Number.isFinite(parsedPrice) || parsedPrice < 0) {
      setError('Некорректная цена.')
      return
    }

    setSaving(true)

    try {
      const updated = await api.updateReceiptItem(receiptId, item.id, {
        name,
        quantity: parsedQuantity,
        unitPrice: parsedPrice,
        categoryId: categoryId || null,
        clearCategory: categoryId === '',
      })
      onUpdated(updated)
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось сохранить позицию')
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="item-card" onSubmit={handleSave}>
      <label className="field">
        <span>Название</span>
        <input value={name} onChange={(event) => setName(event.target.value)} />
      </label>

      <div className="item-grid">
        <label className="field">
          <span>Кол-во</span>
          <input
            inputMode="decimal"
            value={quantity}
            onChange={(event) => setQuantity(event.target.value)}
          />
        </label>
        <label className="field">
          <span>Цена</span>
          <input
            inputMode="decimal"
            value={unitPrice}
            onChange={(event) => setUnitPrice(event.target.value)}
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

      <div className="item-footer">
        <span className="muted small">Сумма: {formatMoney(item.totalPrice)}</span>
        <button type="submit" className="ghost small-button" disabled={saving}>
          {saving ? 'Сохранение…' : 'Сохранить'}
        </button>
      </div>

      {error && <p className="error small">{error}</p>}
    </form>
  )
}

export default function ReceiptScreen() {
  const fileInput = useRef<HTMLInputElement>(null)
  const [receipt, setReceipt] = useState<ReceiptDto | null>(null)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [categories, setCategories] = useState<CategoryDto[]>([])
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)
  const [newName, setNewName] = useState('')
  const [newQuantity, setNewQuantity] = useState('1')
  const [newPrice, setNewPrice] = useState('')
  const [newCategory, setNewCategory] = useState('')

  useEffect(() => {
    let cancelled = false

    api
      .categories('Expense')
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
      .catch(() => {
        if (!cancelled) {
          setPreviewUrl(null)
        }
      })

    return () => {
      cancelled = true
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl)
      }
    }
  }, [receipt])

  async function handleFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''

    if (!file) {
      return
    }

    setError(null)
    setUploading(true)

    try {
      const compressed = await compressImage(file)
      const uploaded = await api.uploadReceipt(compressed.blob, compressed.fileName)
      setReceipt(uploaded)
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось загрузить чек')
    } finally {
      setUploading(false)
    }
  }

  function reset() {
    setReceipt(null)
    setError(null)
  }

  async function handleAddItem(event: FormEvent) {
    event.preventDefault()

    if (!receipt) {
      return
    }

    setError(null)

    const parsedQuantity = parseNumber(newQuantity)
    const parsedPrice = parseNumber(newPrice)

    if (!newName.trim()) {
      setError('Введите название позиции.')
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

    try {
      const updated = await api.addReceiptItem(receipt.id, {
        name: newName.trim(),
        quantity: parsedQuantity,
        unitPrice: parsedPrice,
        categoryId: newCategory || null,
      })
      setReceipt(updated)
      setNewName('')
      setNewQuantity('1')
      setNewPrice('')
      setNewCategory('')
      setAdding(false)
    } catch (err: unknown) {
      setError(err instanceof ApiError ? err.message : 'Не удалось добавить позицию')
    }
  }

  if (!receipt) {
    return (
      <section className="screen">
        <header className="screen-header">
          <h1>Чек</h1>
        </header>

        <p className="muted">
          Сфотографируйте чек — оригинал сохранится, а позиции можно будет заполнить и поправить вручную.
        </p>

        <input
          ref={fileInput}
          type="file"
          accept="image/*"
          capture="environment"
          hidden
          onChange={handleFile}
        />

        <button className="primary" disabled={uploading} onClick={() => fileInput.current?.click()}>
          {uploading ? 'Загрузка…' : 'Загрузить фото чека'}
        </button>

        {error && <p className="error">{error}</p>}
      </section>
    )
  }

  return (
    <section className="screen">
      <header className="screen-header">
        <h1>Чек</h1>
        <button className="ghost" onClick={reset}>
          Новый
        </button>
      </header>

      <p className="status">{STATUS_LABELS[receipt.status] ?? receipt.status}</p>

      {previewUrl && <img className="receipt-image" src={previewUrl} alt="Фото чека" />}

      <div className="card">
        <span className="muted small">Сумма по позициям</span>
        <strong>{formatMoney(receipt.totalAmount ?? 0)}</strong>
      </div>

      {error && <p className="error">{error}</p>}

      <h2 className="section-title">Позиции</h2>

      {receipt.items.length === 0 && <p className="muted">Позиций пока нет — добавьте их вручную.</p>}

      <div className="items">
        {receipt.items.map((item) => (
          <ReceiptItemCard
            key={`${item.id}-${item.name}-${item.quantity}-${item.unitPrice}-${item.categoryId ?? ''}`}
            receiptId={receipt.id}
            item={item}
            categories={categories}
            onUpdated={setReceipt}
          />
        ))}
      </div>

      {adding ? (
        <form className="item-card" onSubmit={handleAddItem}>
          <label className="field">
            <span>Название</span>
            <input value={newName} onChange={(event) => setNewName(event.target.value)} autoFocus />
          </label>

          <div className="item-grid">
            <label className="field">
              <span>Кол-во</span>
              <input
                inputMode="decimal"
                value={newQuantity}
                onChange={(event) => setNewQuantity(event.target.value)}
              />
            </label>
            <label className="field">
              <span>Цена</span>
              <input
                inputMode="decimal"
                value={newPrice}
                onChange={(event) => setNewPrice(event.target.value)}
                placeholder="0.00"
              />
            </label>
          </div>

          <label className="field">
            <span>Категория</span>
            <select value={newCategory} onChange={(event) => setNewCategory(event.target.value)}>
              <option value="">Без категории</option>
              {categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
          </label>

          <div className="actions">
            <button type="button" className="ghost" onClick={() => setAdding(false)}>
              Отмена
            </button>
            <button type="submit" className="primary">
              Добавить
            </button>
          </div>
        </form>
      ) : (
        <button className="ghost" onClick={() => setAdding(true)}>
          + Добавить позицию
        </button>
      )}
    </section>
  )
}
