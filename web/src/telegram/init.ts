import { init, retrieveRawInitData } from '@telegram-apps/sdk'
import { getWebApp, initializeTelegramUi } from './telegram'

export interface TelegramContext {
  initData: string
  hasTelegram: boolean
}

export function initTelegram(): TelegramContext {
  initializeTelegramUi()

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
    initData = getWebApp()?.initData ?? ''
  }

  if (!initData) {
    initData = import.meta.env.VITE_DEV_INIT_DATA ?? ''
  }

  return { initData, hasTelegram: initData.length > 0 }
}
