import { useEffect, useRef } from 'react'
import { getWebApp } from '../telegram/telegram'

interface MainButtonOptions {
  text: string
  visible: boolean
  enabled: boolean
  loading: boolean
  onClick: () => void
}

export function useMainButton({ text, visible, enabled, loading, onClick }: MainButtonOptions): void {
  const handlerRef = useRef(onClick)

  useEffect(() => {
    handlerRef.current = onClick
  })

  useEffect(() => {
    const button = getWebApp()?.MainButton

    if (!button) {
      return
    }

    const callback = () => handlerRef.current()

    try {
      button.setText(text)
      button.onClick(callback)

      if (visible) {
        button.show()
      } else {
        button.hide()
      }
    } catch {
      // no-op
    }

    return () => {
      try {
        button.offClick?.(callback)
        button.hide()
      } catch {
        // no-op
      }
    }
  }, [text, visible])

  useEffect(() => {
    const button = getWebApp()?.MainButton

    if (!button) {
      return
    }

    try {
      if (loading) {
        button.showProgress?.()
        button.disable()
      } else {
        button.hideProgress?.()

        if (enabled) {
          button.enable()
        } else {
          button.disable()
        }
      }
    } catch {
      // no-op
    }
  }, [loading, enabled])
}
