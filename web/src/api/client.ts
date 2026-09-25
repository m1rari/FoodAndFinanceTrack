import type {
  CategoryDto,
  CreateReceiptItemRequest,
  CreateTransactionRequest,
  FoodLogDto,
  ReceiptDto,
  ReceiptSummaryDto,
  ReportSummaryDto,
  UpdateFoodLogRequest,
  TransactionDto,
  UpdateReceiptItemRequest,
  UpdateTransactionRequest,
  UserDto,
} from './types'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''
let initData = ''

export function setInitData(value: string) {
  initData = value
}

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers)

  if (initData) {
    headers.set('X-Telegram-Init-Data', initData)
  }

  if (typeof options.body === 'string') {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(`${API_BASE}${path}`, { ...options, headers })

  if (!response.ok) {
    let message = `Ошибка запроса (${response.status})`

    try {
      const problem = (await response.json()) as Record<string, unknown>
      const detail = problem.detail ?? problem.error ?? problem.title
      if (typeof detail === 'string' && detail.length > 0) {
        message = detail
      }
    } catch {
      // тело ответа не JSON — оставляем общее сообщение
    }

    throw new ApiError(response.status, message)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

function buildQuery(params: Record<string, string | undefined>): string {
  const search = new URLSearchParams()

  for (const [key, value] of Object.entries(params)) {
    if (value) {
      search.set(key, value)
    }
  }

  const query = search.toString()
  return query ? `?${query}` : ''
}

export const api = {
  auth: (value: string) =>
    request<UserDto>('/api/auth/telegram', {
      method: 'POST',
      body: JSON.stringify({ initData: value }),
    }),

  transactions: (params: { from?: string; to?: string; type?: string; categoryId?: string } = {}) =>
    request<TransactionDto[]>(`/api/transactions${buildQuery(params)}`),

  createTransaction: (body: CreateTransactionRequest) =>
    request<TransactionDto>('/api/transactions', {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  updateTransaction: (id: string, body: UpdateTransactionRequest) =>
    request<TransactionDto>(`/api/transactions/${id}`, {
      method: 'PATCH',
      body: JSON.stringify(body),
    }),

  categories: (type?: string) => request<CategoryDto[]>(`/api/categories${buildQuery({ type })}`),

  reportSummary: (from: string, to: string) =>
    request<ReportSummaryDto>(`/api/reports/summary${buildQuery({ from, to })}`),

  uploadReceipt: (file: Blob, fileName: string) => {
    const form = new FormData()
    form.append('file', file, fileName)
    return request<ReceiptDto>('/api/receipts', { method: 'POST', body: form })
  },

  receipts: (unconfirmed = false) =>
    request<ReceiptSummaryDto[]>(`/api/receipts${buildQuery({ unconfirmed: unconfirmed ? 'true' : undefined })}`),

  receipt: (id: string) => request<ReceiptDto>(`/api/receipts/${id}`),

  deleteReceipt: (id: string) => request<void>(`/api/receipts/${id}`, { method: 'DELETE' }),

  addReceiptItem: (receiptId: string, body: CreateReceiptItemRequest) =>
    request<ReceiptDto>(`/api/receipts/${receiptId}/items`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  updateReceiptItem: (receiptId: string, itemId: string, body: UpdateReceiptItemRequest) =>
    request<ReceiptDto>(`/api/receipts/${receiptId}/items/${itemId}`, {
      method: 'PATCH',
      body: JSON.stringify(body),
    }),

  confirmReceipt: (id: string) =>
    request<ReceiptDto>(`/api/receipts/${id}/confirm`, { method: 'POST' }),

  receiptMatches: (id: string) => request<TransactionDto[]>(`/api/receipts/${id}/matches`),

  linkReceipt: (id: string, transactionId: string) =>
    request<ReceiptDto>(`/api/receipts/${id}/link/${transactionId}`, { method: 'POST' }),

  foodLogs: (from: string, to: string) =>
    request<FoodLogDto[]>(`/api/food-logs${buildQuery({ from, to })}`),

  foodLog: (id: string) => request<FoodLogDto>(`/api/food-logs/${id}`),

  uploadFoodLog: (file: Blob, fileName: string) => {
    const form = new FormData()
    form.append('file', file, fileName)
    return request<FoodLogDto>('/api/food-logs', { method: 'POST', body: form })
  },

  updateFoodLog: (id: string, body: UpdateFoodLogRequest) =>
    request<FoodLogDto>(`/api/food-logs/${id}`, { method: 'PATCH', body: JSON.stringify(body) }),

  deleteFoodLog: (id: string) => request<void>(`/api/food-logs/${id}`, { method: 'DELETE' }),
}

export async function fetchFoodImage(imageUrl: string): Promise<Blob> {
  const headers = new Headers()
  if (initData) {
    headers.set('X-Telegram-Init-Data', initData)
  }

  const response = await fetch(`${API_BASE}${imageUrl}`, { headers })

  if (!response.ok) {
    throw new ApiError(response.status, 'Не удалось загрузить изображение блюда')
  }

  return response.blob()
}

export async function fetchReceiptImage(imageUrl: string): Promise<Blob> {
  const headers = new Headers()
  if (initData) {
    headers.set('X-Telegram-Init-Data', initData)
  }

  const response = await fetch(`${API_BASE}${imageUrl}`, { headers })

  if (!response.ok) {
    throw new ApiError(response.status, 'Не удалось загрузить изображение чека')
  }

  return response.blob()
}
