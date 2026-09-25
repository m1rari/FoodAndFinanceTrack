import { useEffect, useMemo, useState } from 'react'
import type { MouseEvent } from 'react'
import { api, ApiError, setInitData } from './api/client'
import type { TransactionDto, UserDto } from './api/types'
import { initTelegram } from './telegram/init'
import OperationsScreen from './screens/OperationsScreen'
import AddScreen from './screens/AddScreen'
import TransactionFormScreen from './screens/TransactionFormScreen'
import ReportScreen from './screens/ReportScreen'
import PurchaseScreen from './screens/PurchaseScreen'

type Tab = 'operations' | 'add' | 'reports'

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
  }

  function handleSaved() {
    setEditing(null)
    setManualOpen(false)
    setRefreshKey((value) => value + 1)
    setTab('operations')
  }

  const overlayOpen = manualOpen || editing !== null || purchaseId !== null

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
              <AddScreen onManual={() => setManualOpen(true)} onUploaded={(id) => setPurchaseId(id)} />
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

        {overlayOpen && purchaseId === null && (manualOpen || editing) && (
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
            onClick={() => setTab('operations')}
          >
            Операции
          </button>
          <button className={tab === 'add' ? 'tab active' : 'tab'} onClick={() => setTab('add')}>
            Добавить
          </button>
          <button className={tab === 'reports' ? 'tab active' : 'tab'} onClick={() => setTab('reports')}>
            Отчёты
          </button>
        </nav>
      )}
    </div>
  )
}
