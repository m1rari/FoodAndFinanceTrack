import type {
  CategoryDto,
  FoodLogDto,
  ReceiptDto,
  ReceiptSummaryDto,
  ReportSummaryDto,
  SavedDishDto,
  StatementDto,
  TransactionDto,
  UserDto,
} from './types'

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export function setInitData(_value: string) {}

function at(dayOffset: number, hour: number, minute = 0): string {
  const date = new Date()
  date.setDate(date.getDate() + dayOffset)
  date.setHours(hour, minute, 0, 0)
  return date.toISOString()
}

const CATEGORIES: CategoryDto[] = [
  { id: 'c1', name: 'Продукты', type: 'Expense', parentId: null, isSystem: true },
  { id: 'c2', name: 'Транспорт', type: 'Expense', parentId: null, isSystem: true },
  { id: 'c3', name: 'Жильё', type: 'Expense', parentId: null, isSystem: true },
  { id: 'c4', name: 'Здоровье', type: 'Expense', parentId: null, isSystem: true },
  { id: 'c5', name: 'Развлечения', type: 'Expense', parentId: null, isSystem: true },
  { id: 'c6', name: 'Связь', type: 'Expense', parentId: null, isSystem: true },
  { id: 'i1', name: 'Зарплата', type: 'Income', parentId: null, isSystem: true },
  { id: 'i2', name: 'Подработка', type: 'Income', parentId: null, isSystem: true },
]

function tx(
  id: string,
  categoryId: string,
  categoryName: string,
  type: string,
  amount: number,
  occurredAt: string,
  extra: Partial<TransactionDto> = {},
): TransactionDto {
  return {
    id,
    accountId: 'a1',
    categoryId,
    categoryName,
    type,
    amount,
    currency: 'BYN',
    occurredAt,
    source: 'Manual',
    comment: null,
    receiptId: null,
    receiptMerchantName: null,
    isTransfer: false,
    createdAt: occurredAt,
    ...extra,
  }
}

const TRANSACTIONS: TransactionDto[] = [
  tx('t1', 'c1', 'Продукты', 'Expense', 42.9, at(0, 18, 20), { source: 'Receipt', receiptId: 'r1', receiptMerchantName: 'Евроопт' }),
  tx('t2', 'c1', 'Продукты', 'Expense', 12.35, at(0, 18, 22), { source: 'Receipt', receiptId: 'r1', receiptMerchantName: 'Евроопт' }),
  tx('t3', 'c2', 'Транспорт', 'Expense', 3.5, at(0, 9, 5), { comment: 'метро' }),
  tx('t4', 'i1', 'Зарплата', 'Income', 1850, at(0, 11, 0)),
  tx('t5', 'c5', 'Развлечения', 'Expense', 24, at(-1, 20, 30), { comment: 'кино' }),
  tx('t6', 'c1', 'Продукты', 'Expense', 61.4, at(-1, 13, 10), { source: 'Receipt', receiptId: 'r2', receiptMerchantName: 'Green' }),
  tx('t7', 'c6', 'Связь', 'Expense', 32.5, at(-1, 10, 0)),
  tx('t8', 'i2', 'Подработка', 'Income', 300, at(-2, 15, 0)),
  tx('t9', 'c3', 'Жильё', 'Expense', 480, at(-2, 12, 0), { comment: 'квартплата' }),
  tx('t10', 'c1', 'Продукты', 'Expense', 18.7, at(-3, 19, 0)),
]

const RECEIPTS: ReceiptSummaryDto[] = [
  {
    id: 'r3',
    merchantName: 'Санта',
    purchaseDate: at(-1, 18, 0),
    totalAmount: 87.45,
    status: 'NeedsReview',
    itemCount: 9,
    confirmed: false,
    createdAt: at(-1, 18, 5),
  },
  {
    id: 'r4',
    merchantName: null,
    purchaseDate: null,
    totalAmount: null,
    status: 'Pending',
    itemCount: 0,
    confirmed: false,
    createdAt: at(0, 8, 0),
  },
]

const FOOD_LOGS: FoodLogDto[] = [
  {
    id: 'f1',
    dishName: 'Овсянка с ягодами и орехами',
    userContext: 'порция ~300 г',
    portionGrams: 300,
    mealGroupId: null,
    caloriesMin: 320,
    caloriesMax: 380,
    proteinMinG: 11,
    proteinMaxG: 14,
    fatMinG: 9,
    fatMaxG: 12,
    carbsMinG: 48,
    carbsMaxG: 56,
    proteinG: 12.5,
    fatG: 10.5,
    carbsG: 52,
    status: 'Processed',
    eatenAt: at(0, 8, 30),
    imageUrl: '/api/food-logs/f1/image',
    createdAt: at(0, 8, 31),
  },
  {
    id: 'f2',
    dishName: 'Борщ со сметаной',
    userContext: null,
    portionGrams: 400,
    mealGroupId: 'm1',
    caloriesMin: 210,
    caloriesMax: 260,
    proteinMinG: 8,
    proteinMaxG: 10,
    fatMinG: 11,
    fatMaxG: 14,
    carbsMinG: 18,
    carbsMaxG: 22,
    proteinG: 9,
    fatG: 12,
    carbsG: 20,
    status: 'Processed',
    eatenAt: at(0, 13, 10),
    imageUrl: '/api/food-logs/f2/image',
    createdAt: at(0, 13, 11),
  },
  {
    id: 'f3',
    dishName: 'Хлеб ржаной',
    userContext: null,
    portionGrams: 60,
    mealGroupId: 'm1',
    caloriesMin: 130,
    caloriesMax: 150,
    proteinMinG: 4,
    proteinMaxG: 5,
    fatMinG: 1,
    fatMaxG: 2,
    carbsMinG: 26,
    carbsMaxG: 30,
    proteinG: 4.5,
    fatG: 1.5,
    carbsG: 28,
    status: 'Processed',
    eatenAt: at(0, 13, 12),
    imageUrl: '/api/food-logs/f3/image',
    createdAt: at(0, 13, 13),
  },
  {
    id: 'f4',
    dishName: 'Куриная грудка с овощами',
    userContext: null,
    portionGrams: 350,
    mealGroupId: null,
    caloriesMin: 380,
    caloriesMax: 440,
    proteinMinG: 42,
    proteinMaxG: 48,
    fatMinG: 12,
    fatMaxG: 16,
    carbsMinG: 22,
    carbsMaxG: 28,
    proteinG: 45,
    fatG: 14,
    carbsG: 25,
    status: 'Pending',
    eatenAt: at(0, 19, 40),
    imageUrl: '/api/food-logs/f4/image',
    createdAt: at(0, 19, 41),
  },
]

const SAVED_DISHES: SavedDishDto[] = [
  {
    id: 's1',
    name: 'Борщ со сметаной',
    caloriesMin: 210,
    caloriesMax: 260,
    proteinMinG: 8,
    proteinMaxG: 10,
    fatMinG: 11,
    fatMaxG: 14,
    carbsMinG: 18,
    carbsMaxG: 22,
    isFavorite: true,
    useCount: 12,
    lastUsedAt: at(-1, 13, 0),
  },
  {
    id: 's2',
    name: 'Овсянка с ягодами',
    caloriesMin: 320,
    caloriesMax: 380,
    proteinMinG: 11,
    proteinMaxG: 14,
    fatMinG: 9,
    fatMaxG: 12,
    carbsMinG: 48,
    carbsMaxG: 56,
    isFavorite: true,
    useCount: 8,
    lastUsedAt: at(0, 8, 30),
  },
  {
    id: 's3',
    name: 'Куриная грудка с овощами',
    caloriesMin: 380,
    caloriesMax: 440,
    proteinMinG: 42,
    proteinMaxG: 48,
    fatMinG: 12,
    fatMaxG: 16,
    carbsMinG: 22,
    carbsMaxG: 28,
    isFavorite: false,
    useCount: 5,
    lastUsedAt: at(-2, 19, 0),
  },
  {
    id: 's4',
    name: 'Творог 5% с мёдом',
    caloriesMin: 180,
    caloriesMax: 220,
    proteinMinG: 18,
    proteinMaxG: 22,
    fatMinG: 6,
    fatMaxG: 8,
    carbsMinG: 14,
    carbsMaxG: 18,
    isFavorite: false,
    useCount: 3,
    lastUsedAt: at(-3, 21, 0),
  },
]

const SUMMARY: ReportSummaryDto = {
  from: at(-30, 0, 0),
  to: at(0, 23, 59),
  currency: 'BYN',
  totalIncome: 2150,
  totalExpense: 1248.35,
  byCategory: [
    { categoryId: 'c1', categoryName: 'Продукты', type: 'Expense', total: 512.4 },
    { categoryId: 'c3', categoryName: 'Жильё', type: 'Expense', total: 480 },
    { categoryId: 'c2', categoryName: 'Транспорт', type: 'Expense', total: 124.6 },
    { categoryId: 'c5', categoryName: 'Развлечения', type: 'Expense', total: 86.5 },
    { categoryId: 'c6', categoryName: 'Связь', type: 'Expense', total: 32.5 },
    { categoryId: 'c4', categoryName: 'Здоровье', type: 'Expense', total: 12.35 },
    { categoryId: 'i1', categoryName: 'Зарплата', type: 'Income', total: 1850 },
    { categoryId: 'i2', categoryName: 'Подработка', type: 'Income', total: 300 },
  ],
}

const RECEIPT: ReceiptDto = {
  id: 'r3',
  merchantName: 'Санта',
  purchaseDate: at(-1, 18, 0),
  totalAmount: 87.45,
  status: 'NeedsReview',
  confidence: 0.82,
  createdAt: at(-1, 18, 5),
  imageUrl: '/api/receipts/r3/image',
  confirmed: false,
  items: [
    { id: 'ri1', name: 'Молоко 3,2%', quantity: 2, unitPrice: 2.35, totalPrice: 4.7, categoryId: 'c1', categoryName: 'Продукты', confidence: 0.94 },
    { id: 'ri2', name: 'Хлеб Бородинский', quantity: 1, unitPrice: 2.1, totalPrice: 2.1, categoryId: 'c1', categoryName: 'Продукты', confidence: 0.51 },
    { id: 'ri3', name: 'Средство для мытья', quantity: 1, unitPrice: 8.9, totalPrice: 8.9, categoryId: null, categoryName: null, confidence: 0.44 },
  ],
}

const STATEMENT: StatementDto = {
  id: 'st1',
  fileName: 'prima_statement_september.pdf',
  status: 'Processed',
  confirmed: false,
  createdCount: 0,
  createdAt: at(-1, 9, 0),
  error: null,
  duplicate: false,
  operations: [
    { occurredAt: at(0, 9, 12), amount: 3.5, direction: 'expense', description: 'Оплата проезда', place: 'MINSK', currency: 'BYN', mcc: '4111', isTransfer: false, categoryId: 'c2', categoryName: 'Транспорт', categoryHint: null, confidence: 0.9, linkTransactionId: null },
    { occurredAt: at(0, 12, 40), amount: 42.9, direction: 'expense', description: 'Оплата товаров', place: 'EUROOPT', currency: 'BYN', mcc: '5411', isTransfer: false, categoryId: 'c1', categoryName: 'Продукты', categoryHint: null, confidence: 0.93, linkTransactionId: null },
    { occurredAt: at(0, 15, 5), amount: 200, direction: 'expense', description: 'Перевод на карту', place: null, currency: 'BYN', mcc: null, isTransfer: true, categoryId: null, categoryName: null, categoryHint: null, confidence: null, linkTransactionId: null },
    { occurredAt: at(-1, 11, 0), amount: 1850, direction: 'income', description: 'Заработная плата', place: 'ОАО ТехноСервис', currency: 'BYN', mcc: null, isTransfer: false, categoryId: 'i1', categoryName: 'Зарплата', categoryHint: null, confidence: 0.88, linkTransactionId: null },
  ],
}

const USER: UserDto = { id: 'u1', telegramId: 1000001, username: 'demo' }

export const api = {
  auth: async () => USER,
  transactions: async () => TRANSACTIONS,
  createTransaction: async () => TRANSACTIONS[0],
  updateTransaction: async () => TRANSACTIONS[0],
  deleteTransaction: async () => undefined,
  categories: async (type?: string) => (type ? CATEGORIES.filter((item) => item.type === type) : CATEGORIES),
  reportSummary: async () => SUMMARY,
  uploadReceipt: async () => RECEIPT,
  receipts: async () => RECEIPTS,
  receipt: async () => RECEIPT,
  deleteReceipt: async () => undefined,
  addReceiptItem: async () => RECEIPT,
  updateReceiptItem: async () => RECEIPT,
  confirmReceipt: async () => RECEIPT,
  receiptMatches: async () => [],
  linkReceipt: async () => RECEIPT,
  foodLogs: async () => FOOD_LOGS,
  foodLog: async () => FOOD_LOGS[0],
  uploadFoodLog: async () => FOOD_LOGS[0],
  updateFoodLog: async () => FOOD_LOGS[0],
  reanalyzeFoodLog: async () => FOOD_LOGS[0],
  shareFoodLog: async () => ({ token: 'x', url: null }),
  foodShare: async () => ({ dishName: 'Борщ', userContext: null, caloriesMin: 210, caloriesMax: 260, proteinG: 9, fatG: 12, carbsG: 20 }),
  claimFoodShare: async () => FOOD_LOGS[0],
  deleteFoodLog: async () => undefined,
  createFoodLogText: async () => FOOD_LOGS[0],
  favoriteFoodLog: async () => undefined,
  savedDishes: async () => SAVED_DISHES,
  addSavedDishToDiary: async () => FOOD_LOGS[0],
  setSavedDishFavorite: async () => SAVED_DISHES[0],
  deleteSavedDish: async () => undefined,
  uploadStatement: async () => STATEMENT,
  statement: async () => STATEMENT,
  statementMatches: async () => [],
  confirmStatement: async () => STATEMENT,
}

export async function fetchFoodImage(): Promise<Blob> {
  return new Blob()
}

export async function fetchReceiptImage(): Promise<Blob> {
  return new Blob()
}
