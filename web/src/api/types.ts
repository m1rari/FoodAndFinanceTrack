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
  receiptMerchantName: string | null
  isTransfer: boolean
  createdAt: string
}

export interface StatementOperationDto {
  occurredAt: string
  amount: number
  direction: string
  description: string | null
  place: string | null
  currency: string | null
  mcc: string | null
  isTransfer: boolean
  categoryId: string | null
  categoryName: string | null
  categoryHint: string | null
  confidence: number | null
  linkTransactionId: string | null
}

export interface StatementMatchDto {
  index: number
  candidates: TransactionDto[]
}

export interface StatementDto {
  id: string
  fileName: string
  status: string
  confirmed: boolean
  createdCount: number
  createdAt: string
  error: string | null
  operations: StatementOperationDto[]
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
  confirmed: boolean
}

export interface ReceiptSummaryDto {
  id: string
  merchantName: string | null
  purchaseDate: string | null
  totalAmount: number | null
  status: string
  itemCount: number
  confirmed: boolean
  createdAt: string
}

export interface FoodLogDto {
  id: string
  dishName: string | null
  userContext: string | null
  caloriesMin: number | null
  caloriesMax: number | null
  proteinMinG: number | null
  proteinMaxG: number | null
  fatMinG: number | null
  fatMaxG: number | null
  carbsMinG: number | null
  carbsMaxG: number | null
  proteinG: number | null
  fatG: number | null
  carbsG: number | null
  status: string
  eatenAt: string
  imageUrl: string
  createdAt: string
}

export interface SavedDishDto {
  id: string
  name: string
  caloriesMin: number | null
  caloriesMax: number | null
  proteinMinG: number | null
  proteinMaxG: number | null
  fatMinG: number | null
  fatMaxG: number | null
  carbsMinG: number | null
  carbsMaxG: number | null
  isFavorite: boolean
  useCount: number
  lastUsedAt: string
}

export interface UpdateFoodLogRequest {
  dishName?: string | null
  userContext?: string | null
  caloriesMin?: number | null
  caloriesMax?: number | null
  proteinMinG?: number | null
  proteinMaxG?: number | null
  fatMinG?: number | null
  fatMaxG?: number | null
  carbsMinG?: number | null
  carbsMaxG?: number | null
  eatenAt?: string | null
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
