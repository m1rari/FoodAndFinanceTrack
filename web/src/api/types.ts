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

export interface ReceiptItemDto {
  id: string
  name: string
  quantity: number
  unitPrice: number
  totalPrice: number
  categoryId: string | null
  categoryName: string | null
  confidence: number | null
}

export interface ReceiptDto {
  id: string
  merchantName: string | null
  purchaseDate: string | null
  totalAmount: number | null
  status: string
  confidence: number | null
  createdAt: string
  imageUrl: string
  items: ReceiptItemDto[]
}

export interface CreateReceiptItemRequest {
  name: string
  quantity: number
  unitPrice: number
  totalPrice?: number | null
  categoryId?: string | null
}

export interface UpdateReceiptItemRequest {
  name?: string | null
  quantity?: number | null
  unitPrice?: number | null
  totalPrice?: number | null
  categoryId?: string | null
  clearCategory?: boolean
}
