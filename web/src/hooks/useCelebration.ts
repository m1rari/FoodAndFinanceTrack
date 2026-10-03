import { useCallback, useState } from 'react'

/** Счётчик-триггер для коротких celebratory-анимаций (конфетти). */
export function useCelebration(): [number, () => void] {
  const [count, setCount] = useState(0)
  const fire = useCallback(() => setCount((value) => value + 1), [])

  return [count, fire]
}
