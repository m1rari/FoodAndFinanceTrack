import { useEffect, useMemo, useState } from 'react'
import { api, ApiError, setInitData } from './api/client'
import type { TransactionDto, UserDto } from './api/types'
import { initTelegram } from './telegram/init'
import TransactionsScreen from './screens/TransactionsScreen'
import TransactionFormScreen from './screens/TransactionFormScreen'
import DashboardScreen from './screens/DashboardScreen'

type Tab = 'transactions' | 'dashboard' | 'form'

export default function App() {
  const context = useMemo(() => initTelegram(), [])
  const [user, setUser] = useState<UserDto | null>(null)
  const [error, setError] = useState<string | null>(
    context.initData ? null : 'Откройте приложение через Telegram — не удалось получить initData.',
  )
  const [loading, setLoading] = useState(context.initData.length > 0)
  const [tab, setTab] = useState<Tab>('transactions')
  const [editing, setEditing] = useState<TransactionDto | null>(null)
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

  if (loading) {
    return <div className="centered">Авторизация…</div>
  }

  if (error || !user) {
    return <div className="centered error">{error ?? 'Ошибка авторизации'}</div>
  }

  function openAdd() {
    setEditing(null)
    setTab('form')
  }

  function openEdit(transaction: TransactionDto) {
    setEditing(transaction)
    setTab('form')
  }

  function closeForm() {
    setEditing(null)
    setTab('transactions')
  }

  function handleSaved() {
    setEditing(null)
    setRefreshKey((value) => value + 1)
    setTab('transactions')
  }

  return (
    <div className="app">
      <main className="content">
        {tab === 'transactions' && (
          <TransactionsScreen refreshKey={refreshKey} onAdd={openAdd} onEdit={openEdit} />
        )}
        {tab === 'dashboard' && <DashboardScreen refreshKey={refreshKey} />}
        {tab === 'form' && (
          <TransactionFormScreen transaction={editing} onDone={handleSaved} onCancel={closeForm} />
        )}
      </main>

      <nav className="tabbar">
        <button
          className={tab === 'transactions' ? 'tab active' : 'tab'}
          onClick={() => {
            setEditing(null)
            setTab('transactions')
          }}
        >
          Операции
        </button>
        <button className={tab === 'form' && !editing ? 'tab active' : 'tab'} onClick={openAdd}>
          Добавить
        </button>
        <button
          className={tab === 'dashboard' ? 'tab active' : 'tab'}
          onClick={() => {
            setEditing(null)
            setTab('dashboard')
          }}
        >
          Отчёты
        </button>
      </nav>
    </div>
  )
}
