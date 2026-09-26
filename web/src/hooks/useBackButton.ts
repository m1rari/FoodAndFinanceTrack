import { useEffect, useRef } from 'react'
import { pushBackHandler } from '../telegram/telegram'

export function useBackButton(enabled: boolean, handler: () => void): void {
  const handlerRef = useRef(handler)

  useEffect(() => {
    handlerRef.current = handler
  })

  useEffect(() => {
    if (!enabled) {
      return
    }

    const callback = () => handlerRef.current()

    return pushBackHandler(callback)
  }, [enabled])
}
