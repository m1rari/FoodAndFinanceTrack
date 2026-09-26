import { useEffect, useMemo, useState } from 'react'
import type { MouseEvent } from 'react'
import { api, ApiError, setInitData } from './api/client'
import type { TransactionDto, UserDto } from './api/types'
import { initTelegram } from './telegram/init'
import { haptic } from './telegram/telegram'
import { useBackButton } from './hooks/useBackButton'
import OperationsScreen from './screens/OperationsScreen'
import AddScreen from './screens/AddScreen'
import TransactionFormScreen from './screens/TransactionFormScreen'
import ReportScreen from './screens/ReportScreen'
import PurchaseScreen from './screens/PurchaseScreen'
import FoodScreen from './screens/FoodScreen'
import FoodDetailScreen from './screens/FoodDetailScreen'
import StatementReviewScreen from './screens/StatementReviewScreen'

type Tab = 'operations' | 'add' | 'food' | 'reports'

const EDITABLE_TAGS = ['INPUT', 'SELECT', 'TEXTAREA']

export default function App() {
  const context = useMemo(() => initTelegram(), [])
  const [user, setUser] = useState<UserDto | null>(null)
  const [error, setError] = useState<string | null>(
    context.initData ? null : 'Откройте приложение через Telegram — не удалось получить initData.',
  )
  const [loading, setLoading] = useState(context.initData.length > 0)
  const [tab, setTab] = useState<Tab>('operations')
  const [editing, setEditing] = useState<TransactionDto | null>(null)
  const [manualOpen, setManualOpen] = useState(false)
  const [purchaseId, setPurchaseId] = useState<string | null>(null)
  const [foodId, setFoodId] = useState<string | null>(null)
  const [statementId, setStatementId] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)

  useEffect(() => {
    if (!context.initData) {
      return
    }

    setInitData(context.initData)

    api
      .auth(context.initData)
      .then((data) => {
        setUser(data)
        setError(null)
      })
      .catch((err: unknown) => {
        setError(err instanceof ApiError ? err.message : 'Не удалось авторизоваться')
      })
      .finally(() => setLoading(false))
  }, [context])

  const overlayOpen =
    manualOpen || editing !== null || purchaseId !== null || foodId !== null || statementId !== null
  useBackButton(overlayOpen, closeOverlays)

  function handleContentClick(event: MouseEvent<HTMLElement>) {
    const target = event.target as HTMLElement

    if (EDITABLE_TAGS.includes(target.tagName)) {
      return
    }

    const active = document.activeElement as HTMLElement | null

    if (active && EDITABLE_TAGS.includes(active.tagName)) {
      active.blur()
    }
  }

  if (loading) {
    return <div className="centered">Авторизация…</div>
  }

  if (error || !user) {
    return <div className="centered error">{error ?? 'Ошибка авторизации'}</div>
  }

  function closeOverlays() {
    setEditing(null)
    setManualOpen(false)
    setPurchaseId(null)
    setFoodId(null)
    setStatementId(null)
  }

  function handleSaved() {
    setEditing(null)
    setManualOpen(false)
    setRefreshKey((value) => value + 1)
    setTab('operations')
  }

  function switchTab(next: Tab) {
    haptic('select')
    setEditing(null)
    setTab(next)
  }

  return (
    <div className="app">
      <main className="content" onClick={handleContentClick}>
        {!overlayOpen && (
          <>
            {tab === 'operations' && (
              <OperationsScreen
                refreshKey={refreshKey}
                onAdd={() => setTab('add')}
                onEdit={(transaction) => setEditing(transaction)}
                onOpenPurchase={(id) => setPurchaseId(id)}
              />
            )}
            {tab === 'add' && (
              <AddScreen
                onManual={() => setManualOpen(true)}
                onUploaded={(id) => setPurchaseId(id)}
                onStatement={(id) => setStatementId(id)}
              />
            )}
            {tab === 'food' && (
              <FoodScreen
                refreshKey={refreshKey}
                onOpen={(id) => setFoodId(id)}
                onUploaded={(id) => {
                  setRefreshKey((value) => value + 1)
                  setFoodId(id)
                }}
              />
            )}
            {tab === 'reports' && <ReportScreen refreshKey={refreshKey} />}
          </>
        )}

        {overlayOpen && purchaseId !== null && (
          <PurchaseScreen
            key={purchaseId}
            receiptId={purchaseId}
            onBack={closeOverlays}
            onChanged={() => setRefreshKey((value) => value + 1)}
          />
        )}

        {overlayOpen && purchaseId === null && foodId !== null && (
          <FoodDetailScreen
            key={foodId}
            foodId={foodId}
            onBack={closeOverlays}
            onChanged={() => setRefreshKey((value) => value + 1)}
          />
        )}

        {overlayOpen && purchaseId === null && foodId === null && statementId !== null && (
          <StatementReviewScreen
            key={statementId}
            statementId={statementId}
            onBack={closeOverlays}
            onChanged={() => setRefreshKey((value) => value + 1)}
          />
        )}

        {overlayOpen &&
          purchaseId === null &&
          foodId === null &&
          statementId === null &&
          (manualOpen || editing) && (
            <TransactionFormScreen
              transaction={editing}
              onDone={handleSaved}
              onCancel={closeOverlays}
            />
          )}
      </main>

      {!overlayOpen && (
        <nav className="tabbar">
          <button
            className={tab === 'operations' ? 'tab active' : 'tab'}
            onClick={() => switchTab('operations')}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <path d="M5 3h14v18l-7-4-7 4z" />
            </svg>
            Операции
          </button>
          <button className={tab === 'add' ? 'tab active' : 'tab'} onClick={() => switchTab('add')}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="9" />
              <path d="M12 8v8M8 12h8" />
            </svg>
            Добавить
          </button>
          <button className={tab === 'food' ? 'tab active' : 'tab'} onClick={() => switchTab('food')}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 3c3 3 5 5 5 9a5 5 0 0 1-10 0c0-4 2-6 5-9z" />
              <path d="M12 21v-6" />
            </svg>
            Питание
          </button>
          <button className={tab === 'reports' ? 'tab active' : 'tab'} onClick={() => switchTab('reports')}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <path d="M4 20V10M12 20V4M20 20v-6" />
            </svg>
            Отчёты
          </button>
        </nav>
      )}
    </div>
  )
}
