import { useEffect, useMemo, useState } from 'react'
import { api, ApiError, setInitData } from './api/client'
import type { UserDto } from './api/types'
import { initTelegram } from './telegram/init'
import TransactionsScreen from './screens/TransactionsScreen'
import AddTransactionScreen from './screens/AddTransactionScreen'
import DashboardScreen from './screens/DashboardScreen'

type Tab = 'transactions' | 'dashboard' | 'add'

export default function App() {
  const context = useMemo(() => initTelegram(), [])
  const [user, setUser] = useState<UserDto | null>(null)
  const [error, setError] = useState<string | null>(
    context.initData ? null : 'Откройте приложение через Telegram — не удалось получить initData.',
  )
  const [loading, setLoading] = useState(context.initData.length > 0)
  const [tab, setTab] = useState<Tab>('transactions')
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

  function handleCreated() {
    setRefreshKey((value) => value + 1)
    setTab('transactions')
  }

  return (
    <div className="app">
      <main className="content">
        {tab === 'transactions' && (
          <TransactionsScreen refreshKey={refreshKey} onAdd={() => setTab('add')} />
        )}
        {tab === 'dashboard' && <DashboardScreen refreshKey={refreshKey} />}
        {tab === 'add' && (
          <AddTransactionScreen onDone={handleCreated} onCancel={() => setTab('transactions')} />
        )}
      </main>

      <nav className="tabbar">
        <button
          className={tab === 'transactions' ? 'tab active' : 'tab'}
          onClick={() => setTab('transactions')}
        >
          Операции
        </button>
        <button className={tab === 'add' ? 'tab active' : 'tab'} onClick={() => setTab('add')}>
          Добавить
        </button>
        <button
          className={tab === 'dashboard' ? 'tab active' : 'tab'}
          onClick={() => setTab('dashboard')}
        >
          Отчёты
        </button>
      </nav>
    </div>
  )
}
