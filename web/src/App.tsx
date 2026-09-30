import { useEffect, useMemo, useState } from 'react'
import type { MouseEvent } from 'react'
import { api, ApiError, setInitData } from './api/client'
import type { FoodSharePreviewDto, TransactionDto, UserDto } from './api/types'
import BottomSheet from './components/BottomSheet'
import { initTelegram } from './telegram/init'
import { getStartParam, haptic } from './telegram/telegram'
import { readUrlParam, writeUrlParams } from './utils/url'
import { useBackButton } from './hooks/useBackButton'
import OperationsScreen from './screens/OperationsScreen'
import TransactionFormScreen from './screens/TransactionFormScreen'
import ReportScreen from './screens/ReportScreen'
import PurchaseScreen from './screens/PurchaseScreen'
import FoodScreen from './screens/FoodScreen'
import FoodDetailScreen from './screens/FoodDetailScreen'
import StatementReviewScreen from './screens/StatementReviewScreen'

type Tab = 'operations' | 'food' | 'reports'

const EDITABLE_TAGS = ['INPUT', 'SELECT', 'TEXTAREA']

function formatRange(min: number | null, max: number | null): string {
  if (min == null && max == null) {
    return '—'
  }

  if (min != null && max != null) {
    return `${Math.round(min)}–${Math.round(max)}`
  }

  return `${Math.round((min ?? max) as number)}`
}

export default function App() {
  const context = useMemo(() => initTelegram(), [])
  const [user, setUser] = useState<UserDto | null>(null)
  const [error, setError] = useState<string | null>(
    context.initData ? null : 'Откройте приложение через Telegram — не удалось получить initData.',
  )
  const [loading, setLoading] = useState(context.initData.length > 0)
  const [tab, setTab] = useState<Tab>(() => {
    const value = readUrlParam('tab')
    return value === 'food' || value === 'reports' ? value : 'operations'
  })
  const [editing, setEditing] = useState<TransactionDto | null>(null)
  const [manualOpen, setManualOpen] = useState(false)
  const [purchaseId, setPurchaseId] = useState<string | null>(null)
  const [foodId, setFoodId] = useState<string | null>(null)
  const [statementId, setStatementId] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)
  const [notice, setNotice] = useState<string | null>(null)
  const [pendingShare, setPendingShare] = useState<{ token: string; preview: FoodSharePreviewDto } | null>(null)
  const [sharing, setSharing] = useState(false)

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

  useEffect(() => {
    if (!user) {
      return
    }

    const param = getStartParam()

    if (!param.startsWith('fd-')) {
      return
    }

    const token = param.slice(3)
    const claimedKey = 'fft-claimed-shares'
    const dismissedKey = 'fft-dismissed-shares'

    const readTokens = (key: string): string[] => {
      try {
        return JSON.parse(localStorage.getItem(key) ?? '[]') as string[]
      } catch {
        return []
      }
    }

    if (readTokens(claimedKey).includes(token)) {
      const timer = window.setTimeout(() => setNotice('Это блюдо уже добавлено ранее.'), 0)
      return () => window.clearTimeout(timer)
    }

    if (readTokens(dismissedKey).includes(token)) {
      return
    }

    let cancelled = false

    api
      .foodShare(token)
      .then((preview) => {
        if (!cancelled) {
          setPendingShare({ token, preview })
        }
      })
      .catch(() => {
        if (!cancelled) {
          setNotice('Ссылку на блюдо не удалось открыть: она недействительна или устарела.')
        }
      })

    return () => {
      cancelled = true
    }
  }, [user])

  useEffect(() => {
    if (!notice) {
      return
    }

    const timer = window.setTimeout(() => setNotice(null), 6000)

    return () => window.clearTimeout(timer)
  }, [notice])

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
    writeUrlParams({ tab: 'operations' })
    setTab('operations')
  }

  function switchTab(next: Tab) {
    haptic('select')
    writeUrlParams({ tab: next })
    setEditing(null)
    setTab(next)
  }

  function persistToken(key: string, token: string) {
    try {
      const list = JSON.parse(localStorage.getItem(key) ?? '[]') as string[]
      localStorage.setItem(key, JSON.stringify([...list, token]))
    } catch {
      // localStorage может быть недоступен
    }
  }

  function dismissSharedDish() {
    if (pendingShare) {
      persistToken('fft-dismissed-shares', pendingShare.token)
    }

    setPendingShare(null)
  }

  async function acceptSharedDish() {
    if (!pendingShare) {
      return
    }

    setSharing(true)

    try {
      await api.claimFoodShare(pendingShare.token)
      persistToken('fft-claimed-shares', pendingShare.token)
      haptic('success')
      setPendingShare(null)
      setNotice('Блюдо добавлено в дневник.')
      setRefreshKey((value) => value + 1)
    } catch (err: unknown) {
      setNotice(err instanceof ApiError ? err.message : 'Не удалось добавить блюдо')
    } finally {
      setSharing(false)
    }
  }

  return (
    <div className="app">
      <BottomSheet open={pendingShare !== null} title="Поделились блюдом" onClose={dismissSharedDish}>
        {pendingShare && (
          <>
            <div className="card">
              <strong>{pendingShare.preview.dishName}</strong>
              <span className="muted small">
                {formatRange(pendingShare.preview.caloriesMin, pendingShare.preview.caloriesMax)} ккал
                {pendingShare.preview.userContext ? ` · ${pendingShare.preview.userContext}` : ''}
              </span>
            </div>

            <div className="actions">
              <button type="button" className="ghost" onClick={dismissSharedDish}>
                Отмена
              </button>
              <button type="button" className="primary" disabled={sharing} onClick={acceptSharedDish}>
                {sharing ? 'Добавление…' : 'Добавить в дневник'}
              </button>
            </div>
          </>
        )}
      </BottomSheet>

      {notice && (
        <div className="notice" role="status" aria-live="polite">
          <span>{notice}</span>
          <button className="notice-close" aria-label="Закрыть" onClick={() => setNotice(null)}>
            ✕
          </button>
        </div>
      )}

      <main className="content" onClick={handleContentClick}>
        {!overlayOpen && (
          <>
            {tab === 'operations' && (
              <OperationsScreen
                refreshKey={refreshKey}
                onEdit={(transaction) => setEditing(transaction)}
                onOpenPurchase={(id) => setPurchaseId(id)}
                onOpenStatement={(id) => setStatementId(id)}
                onManual={() => setManualOpen(true)}
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
        <nav className="tabbar" aria-label="Навигация">
          <button
            className={tab === 'operations' ? 'tab active' : 'tab'}
            aria-current={tab === 'operations' ? 'page' : undefined}
            onClick={() => switchTab('operations')}
          >
            <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <path d="M5 3h14v18l-7-4-7 4z" />
            </svg>
            Операции
          </button>
          <button
            className={tab === 'food' ? 'tab active' : 'tab'}
            aria-current={tab === 'food' ? 'page' : undefined}
            onClick={() => switchTab('food')}
          >
            <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 3c3 3 5 5 5 9a5 5 0 0 1-10 0c0-4 2-6 5-9z" />
              <path d="M12 21v-6" />
            </svg>
            Питание
          </button>
          <button
            className={tab === 'reports' ? 'tab active' : 'tab'}
            aria-current={tab === 'reports' ? 'page' : undefined}
            onClick={() => switchTab('reports')}
          >
            <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <path d="M4 20V10M12 20V4M20 20v-6" />
            </svg>
            Отчёты
          </button>
        </nav>
      )}
    </div>
  )
}
