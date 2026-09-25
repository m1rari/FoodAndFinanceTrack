export type PeriodPreset = 'today' | 'week' | 'month' | 'custom'

export interface Period {
  from: string
  to: string
  preset: PeriodPreset
}

function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate(), 0, 0, 0, 0)
}

function endOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate(), 23, 59, 59, 999)
}

export function periodFor(preset: PeriodPreset, now = new Date()): Period {
  if (preset === 'today') {
    return { from: startOfDay(now).toISOString(), to: endOfDay(now).toISOString(), preset }
  }

  if (preset === 'week') {
    const start = startOfDay(now)
    const offset = (start.getDay() + 6) % 7
    start.setDate(start.getDate() - offset)

    return { from: start.toISOString(), to: endOfDay(now).toISOString(), preset }
  }

  const from = new Date(now.getFullYear(), now.getMonth(), 1, 0, 0, 0, 0)
  const to = endOfDay(new Date(now.getFullYear(), now.getMonth() + 1, 0))

  return { from: from.toISOString(), to: to.toISOString(), preset: 'month' }
}

export function customPeriod(fromDate: string, toDate: string): Period {
  return {
    from: startOfDay(new Date(`${fromDate}T00:00:00`)).toISOString(),
    to: endOfDay(new Date(`${toDate}T00:00:00`)).toISOString(),
    preset: 'custom',
  }
}

export function dayRange(date: Date): { from: string; to: string } {
  return { from: startOfDay(date).toISOString(), to: endOfDay(date).toISOString() }
}

export function addDays(date: Date, days: number): Date {
  const next = new Date(date)
  next.setDate(next.getDate() + days)
  return next
}

export function formatDayTitle(date: Date): string {
  const today = startOfDay(new Date())
  const value = startOfDay(date)
  const diff = Math.round((value.getTime() - today.getTime()) / 86400000)

  if (diff === 0) {
    return 'Сегодня'
  }

  if (diff === -1) {
    return 'Вчера'
  }

  return new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long', weekday: 'short' }).format(date)
}

export function dayKey(iso: string): string {
  const date = new Date(iso)

  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

export function formatDayLabel(iso: string): string {
  const date = new Date(iso)
  const today = startOfDay(new Date())
  const yesterday = new Date(today)
  yesterday.setDate(today.getDate() - 1)

  if (dayKey(iso) === dayKey(today.toISOString())) {
    return 'Сегодня'
  }

  if (dayKey(iso) === dayKey(yesterday.toISOString())) {
    return 'Вчера'
  }

  return new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long', weekday: 'short' }).format(date)
}

export function formatPeriodLabel(period: Period): string {
  if (period.preset === 'today') {
    return 'Сегодня'
  }

  if (period.preset === 'week') {
    return 'Неделя'
  }

  const formatter = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long' })

  if (period.preset === 'month') {
    return formatter.format(new Date(period.from))
  }

  const from = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short' }).format(new Date(period.from))
  const to = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short' }).format(new Date(period.to))

  return `${from} – ${to}`
}

export function toDateInput(iso: string): string {
  return dayKey(iso)
}

export function todayInput(): string {
  return dayKey(new Date().toISOString())
}
