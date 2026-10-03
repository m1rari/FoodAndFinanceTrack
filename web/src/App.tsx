import { useEffect, useMemo, useState } from 'react'
import type { CSSProperties, MouseEvent } from 'react'
import { api, ApiError, setInitData } from './api/client'
import type { FoodSharePreviewDto, TransactionDto, UserDto } from './api/types'
import BottomSheet from './components/BottomSheet'
import Confetti from './components/Confetti'
import EmptyState from './components/EmptyState'
import Icon from './components/Icon'
import type { IconName } from './components/Icon'
import Splash from './components/Splash'
import { useCelebration } from './hooks/useCelebration'
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
type NoticeTone = 'info' | 'success' | 'error'

interface Notice {
  text: string
  tone: NoticeTone
}

const TABS: Array<{ id: Tab; label: string; icon: IconName }> = [
  { id: 'operations', label: 'Операции', icon: 'ledger' },
  { id: 'food', label: 'Питание', icon: 'utensils' },
  { id: 'reports', label: 'Отчёты', icon: 'chart' },
]

const NOTICE_ICONS: Record<NoticeTone, IconName> = {
  info: 'info',
  success: 'check',
  error: 'alert',
}

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
  const [notice, setNotice] = useState<Notice | null>(null)
  const [pendingShare, setPendingShare] = useState<{ token: string; preview: FoodSharePreviewDto } | null>(null)
  const [sharing, setSharing] = useState(false)
  const [celebration, celebrate] = useCelebration()

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
      const timer = window.setTimeout(
        () => setNotice({ text: 'Это блюдо уже добавлено ранее.', tone: 'info' }),
        0,
      )
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
          setNotice({
            text: 'Ссылку на блюдо не удалось открыть: она недействительна или устарела.',
            tone: 'error',
          })
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
    return <Splash label="Авторизация…" />
  }

  if (error || !user) {
    return (
      <div className="centered">
        <EmptyState
          art="error"
          title="Не удалось открыть"
          text={error ?? 'Ошибка авторизации'}
        />
      </div>
    )
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
      celebrate()
      setPendingShare(null)
      setNotice({ text: 'Блюдо добавлено в дневник.', tone: 'success' })
      setRefreshKey((value) => value + 1)
    } catch (err: unknown) {
      setNotice({
        text: err instanceof ApiError ? err.message : 'Не удалось добавить блюдо',
        tone: 'error',
      })
    } finally {
      setSharing(false)
    }
  }

  return (
    <div className="app">
      <Confetti trigger={celebration} />

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
                <Icon name="plus" size={18} />
                {sharing ? 'Добавление…' : 'Добавить в дневник'}
              </button>
            </div>
          </>
        )}
      </BottomSheet>

      {notice && (
        <div className={`notice is-${notice.tone}`} role="status" aria-live="polite">
          <span className="notice-icon" aria-hidden="true">
            <Icon name={NOTICE_ICONS[notice.tone]} size={16} strokeWidth={2.2} />
          </span>
          <span className="notice-text">{notice.text}</span>
          <button className="notice-close" aria-label="Закрыть" onClick={() => setNotice(null)}>
            <Icon name="close" size={16} strokeWidth={2.2} />
          </button>
        </div>
      )}

      <main className="content" onClick={handleContentClick}>
        {!overlayOpen && (
          <div className="screen-slot" key={tab}>
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
          </div>
        )}

        {overlayOpen && purchaseId !== null && (
          <div className="screen-slot" key={purchaseId}>
            <PurchaseScreen
              receiptId={purchaseId}
              onBack={closeOverlays}
              onChanged={() => setRefreshKey((value) => value + 1)}
            />
          </div>
        )}

        {overlayOpen && purchaseId === null && foodId !== null && (
          <div className="screen-slot" key={foodId}>
            <FoodDetailScreen
              foodId={foodId}
              onBack={closeOverlays}
              onChanged={() => setRefreshKey((value) => value + 1)}
            />
          </div>
        )}

        {overlayOpen && purchaseId === null && foodId === null && statementId !== null && (
          <div className="screen-slot" key={statementId}>
            <StatementReviewScreen
              statementId={statementId}
              onBack={closeOverlays}
              onChanged={() => setRefreshKey((value) => value + 1)}
            />
          </div>
        )}

        {overlayOpen &&
          purchaseId === null &&
          foodId === null &&
          statementId === null &&
          (manualOpen || editing) && (
            <div className="screen-slot" key={editing?.id ?? 'manual'}>
              <TransactionFormScreen
                transaction={editing}
                onDone={handleSaved}
                onCancel={closeOverlays}
              />
            </div>
          )}
      </main>

      {!overlayOpen && (
        <nav
          className="tabbar"
          aria-label="Навигация"
          style={{ '--tab-index': TABS.findIndex((item) => item.id === tab) } as CSSProperties}
        >
          <span className="tab-indicator" aria-hidden="true" />
          {TABS.map((item) => (
            <button
              key={item.id}
              className={tab === item.id ? 'tab active' : 'tab'}
              aria-current={tab === item.id ? 'page' : undefined}
              onClick={() => switchTab(item.id)}
            >
              <Icon name={item.icon} size={21} />
              {item.label}
            </button>
          ))}
        </nav>
      )}
    </div>
  )
}
