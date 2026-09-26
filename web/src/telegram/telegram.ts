export interface TelegramWebApp {
  initData?: string
  version?: string
  colorScheme?: string
  ready?: () => void
  expand?: () => void
  close?: () => void
  disableVerticalSwipes?: () => void
  enableVerticalSwipes?: () => void
  enableClosingConfirmation?: () => void
  disableClosingConfirmation?: () => void
  setHeaderColor?: (color: string) => void
  setBackgroundColor?: (color: string) => void
  HapticFeedback?: {
    impactOccurred?: (style: string) => void
    notificationOccurred?: (type: string) => void
    selectionChanged?: () => void
  }
  BackButton?: {
    show?: () => void
    hide?: () => void
    onClick?: (callback: () => void) => void
    offClick?: (callback: () => void) => void
  }
  MainButton?: {
    setText: (text: string) => void
    show: () => void
    hide: () => void
    enable: () => void
    disable: () => void
    showProgress: (leaveActive?: boolean) => void
    hideProgress: () => void
    onClick: (callback: () => void) => void
    offClick: (callback: () => void) => void
    setParams?: (params: Record<string, unknown>) => void
  }
}

declare global {
  interface Window {
    Telegram?: { WebApp?: TelegramWebApp }
  }
}

export function getWebApp(): TelegramWebApp | undefined {
  return typeof window === 'undefined' ? undefined : window.Telegram?.WebApp
}

export function isTelegram(): boolean {
  const app = getWebApp()
  return Boolean(app && (app.initData || app.version))
}

export function isMainButtonAvailable(): boolean {
  return Boolean(getWebApp()?.MainButton)
}

export function initializeTelegramUi(): void {
  const app = getWebApp()

  if (!app) {
    return
  }

  try {
    app.ready?.()
  } catch {
    // no-op
  }

  try {
    app.expand?.()
  } catch {
    // no-op
  }

  try {
    app.disableVerticalSwipes?.()
  } catch {
    // не поддерживается на старых клиентах
  }

  try {
    app.setHeaderColor?.('bg_color')
    app.setBackgroundColor?.('bg_color')
  } catch {
    // no-op
  }
}

export type HapticKind = 'light' | 'medium' | 'heavy' | 'success' | 'error' | 'warning' | 'select'

export function haptic(kind: HapticKind): void {
  const feedback = getWebApp()?.HapticFeedback

  if (!feedback) {
    return
  }

  try {
    if (kind === 'success' || kind === 'error' || kind === 'warning') {
      feedback.notificationOccurred?.(kind)
    } else if (kind === 'select') {
      feedback.selectionChanged?.()
    } else {
      feedback.impactOccurred?.(kind)
    }
  } catch {
    // no-op
  }
}

const backHandlers: Array<() => void> = []
let backBound = false

function updateBackVisibility(): void {
  const button = getWebApp()?.BackButton

  if (!button) {
    return
  }

  try {
    if (backHandlers.length > 0) {
      button.show?.()
    } else {
      button.hide?.()
    }
  } catch {
    // no-op
  }
}

export function pushBackHandler(handler: () => void): () => void {
  const button = getWebApp()?.BackButton

  if (button && !backBound) {
    backBound = true

    try {
      button.onClick?.(() => {
        backHandlers[backHandlers.length - 1]?.()
      })
    } catch {
      // no-op
    }
  }

  backHandlers.push(handler)
  updateBackVisibility()

  return () => {
    const index = backHandlers.lastIndexOf(handler)

    if (index >= 0) {
      backHandlers.splice(index, 1)
    }

    updateBackVisibility()
  }
}
