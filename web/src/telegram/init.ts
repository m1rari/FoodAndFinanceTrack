import { init, retrieveRawInitData } from '@telegram-apps/sdk'

export interface TelegramContext {
  initData: string
  hasTelegram: boolean
}

interface TelegramWebApp {
  initData?: string
}

declare global {
  interface Window {
    Telegram?: { WebApp?: TelegramWebApp }
  }
}

export function initTelegram(): TelegramContext {
  try {
    init()
  } catch {
    // SDK может не инициализироваться вне Telegram — это не критично
  }

  let initData = ''

  try {
    initData = retrieveRawInitData() ?? ''
  } catch {
    initData = ''
  }

  if (!initData) {
    initData = window.Telegram?.WebApp?.initData ?? ''
  }

  if (!initData) {
    initData = import.meta.env.VITE_DEV_INIT_DATA ?? ''
  }

  return { initData, hasTelegram: initData.length > 0 }
}
