import type { IconName } from '../components/Icon'

export interface Visual {
  icon: IconName
  color: string
}

/** Палитра приложения: используется и в CSS (`--cat`), и в SVG-иллюстрациях. */
export const MACRO_COLORS = {
  protein: '#35d07f',
  fat: '#f5b942',
  carbs: '#5b8cff',
} as const

const CATEGORY_VISUALS: Record<string, Visual> = {
  продукты: { icon: 'cart', color: '#35d07f' },
  транспорт: { icon: 'bus', color: '#5b8cff' },
  жильё: { icon: 'home', color: '#f5b942' },
  жилье: { icon: 'home', color: '#f5b942' },
  здоровье: { icon: 'pulse', color: '#ff6b8a' },
  развлечения: { icon: 'ticket', color: '#a78bfa' },
  одежда: { icon: 'shirt', color: '#f472b6' },
  связь: { icon: 'phone', color: '#22d3ee' },
  ерип: { icon: 'credit-card', color: '#2dd4bf' },
  прочее: { icon: 'tag', color: '#8b96ad' },
  зарплата: { icon: 'briefcase', color: '#34d399' },
  подработка: { icon: 'banknote', color: '#a3e635' },
}

const EXPENSE_FALLBACK: Visual = { icon: 'tag', color: '#8b96ad' }
const INCOME_FALLBACK: Visual = { icon: 'banknote', color: '#34d399' }
const TRANSFER: Visual = { icon: 'swap', color: '#f5b942' }
const PURCHASE: Visual = { icon: 'ledger', color: '#5b8cff' }

export function categoryVisual(name: string | null | undefined, type?: string | null): Visual {
  const key = name?.trim().toLowerCase()

  if (key && CATEGORY_VISUALS[key]) {
    return CATEGORY_VISUALS[key]
  }

  return type === 'Income' ? INCOME_FALLBACK : EXPENSE_FALLBACK
}

export function transactionVisual(tx: {
  categoryName: string | null
  type: string
  isTransfer: boolean
}): Visual {
  if (tx.isTransfer) {
    return TRANSFER
  }

  return categoryVisual(tx.categoryName, tx.type)
}

export function purchaseVisual(categoryName: string | null): Visual {
  return categoryName ? categoryVisual(categoryName, 'Expense') : PURCHASE
}

export interface MealVisual extends Visual {
  label: string
}

/** Приём пищи по времени — используется как подпись и иконка в дневнике. */
export function mealVisual(iso: string): MealVisual {
  const hour = new Date(iso).getHours()

  if (hour < 5) {
    return { label: 'Ночной перекус', icon: 'moon', color: '#8b6cff' }
  }

  if (hour < 11) {
    return { label: 'Завтрак', icon: 'sunrise', color: '#f5b942' }
  }

  if (hour < 16) {
    return { label: 'Обед', icon: 'sun', color: '#5b8cff' }
  }

  if (hour < 22) {
    return { label: 'Ужин', icon: 'utensils', color: '#35d07f' }
  }

  return { label: 'Поздний перекус', icon: 'coffee', color: '#a78bfa' }
}
