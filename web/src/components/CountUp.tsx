import { useEffect, useRef, useState } from 'react'
import { prefersReducedMotion } from '../utils/motion'

interface Props {
  value: number
  /** Как отрисовать текущее (промежуточное) значение. */
  format: (value: number) => string
  className?: string
  duration?: number
}

/** Число, «набегающее» до значения — оживляет метрики без потери доступности. */
export default function CountUp({ value, format, className, duration = 720 }: Props) {
  const [reduced] = useState(prefersReducedMotion)
  const [display, setDisplay] = useState(0)
  const fromRef = useRef(0)

  useEffect(() => {
    if (reduced) {
      fromRef.current = value
      return
    }

    const from = fromRef.current

    if (from === value) {
      return
    }

    let raf = 0
    const start = performance.now()

    const tick = (now: number) => {
      // Отметка кадра может быть раньше момента старта — не даём прогрессу уйти в минус.
      const progress = Math.min(1, Math.max(0, (now - start) / duration))
      const eased = 1 - Math.pow(1 - progress, 3)

      setDisplay(from + (value - from) * eased)

      if (progress < 1) {
        raf = requestAnimationFrame(tick)
      } else {
        fromRef.current = value
      }
    }

    raf = requestAnimationFrame(tick)

    return () => cancelAnimationFrame(raf)
  }, [value, duration, reduced])

  const shown = reduced ? value : display

  return (
    <span className={className}>
      <span aria-hidden="true">{format(shown)}</span>
      <span className="sr-only">{format(value)}</span>
    </span>
  )
}
