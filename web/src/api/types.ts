export interface UserDto {
  id: string
  telegramId: number
  username: string | null
}

export interface TransactionDto {
  id: string
  accountId: string
  categoryId: string | null
  categoryName: string | null
  type: string
  amount: number
  currency: string
  occurredAt: string
  source: string
  comment: string | null
  receiptId: string | null
  createdAt: string
}

export interface CategoryDto {
  id: string
  name: string
  type: string
  parentId: string | null
  isSystem: boolean
}

export interface CategorySummaryDto {
  categoryId: string | null
  categoryName: string | null
  type: string
  total: number
}

export interface ReportSummaryDto {
  from: string
  to: string
  currency: string
  totalIncome: number
  totalExpense: number
  byCategory: CategorySummaryDto[]
}

export interface CreateTransactionRequest {
  amount: number
  type: string
  categoryId?: string | null
  accountId?: string | null
  occurredAt?: string | null
  comment?: string | null
}

export interface UpdateTransactionRequest {
  amount?: number | null
  categoryId?: string | null
  clearCategory?: boolean
  occurredAt?: string | null
  comment?: string | null
  clearComment?: boolean
}
