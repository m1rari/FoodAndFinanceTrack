import type { CSSProperties } from 'react'
import { prefersReducedMotion } from '../utils/motion'

const COLORS = ['#5b8cff', '#8b6cff', '#35d07f', '#f5b942', '#22d3ee', '#ff6b8a']
const COUNT = 26

interface Piece {
  id: string
  left: number
  delay: number
  duration: number
  drift: number
  rotate: number
  color: string
  width: number
  height: number
  round: boolean
}

/** Детерминированный «шум»: одинаковые значения при одном и том же триггере. */
function noise(index: number, salt: number): number {
  const value = Math.sin(index * 12.9898 + salt * 78.233) * 43758.5453

  return value - Math.floor(value)
}

function makePieces(trigger: number): Piece[] {
  return Array.from({ length: COUNT }, (_, index) => ({
    id: `${trigger}-${index}`,
    left: noise(index, 1) * 100,
    delay: noise(index, 2) * 0.22,
    duration: 0.95 + noise(index, 3) * 0.6,
    drift: (noise(index, 4) - 0.5) * 170,
    rotate: (noise(index, 5) > 0.5 ? 1 : -1) * (240 + noise(index, 6) * 480),
    color: COLORS[index % COLORS.length],
    width: 5 + noise(index, 7) * 5,
    height: 8 + noise(index, 8) * 9,
    round: noise(index, 9) > 0.7,
  }))
}

interface Props {
  /** Увеличивается при каждом праздничном событии. 0 — ничего не показывать. */
  trigger: number
}

export default function Confetti({ trigger }: Props) {
  const pieces = trigger > 0 && !prefersReducedMotion() ? makePieces(trigger) : null

  if (!pieces) {
    return null
  }

  return (
    <div className="confetti" aria-hidden="true">
      {pieces.map((piece) => (
        <span
          key={piece.id}
          style={
            {
              left: `${piece.left}%`,
              width: piece.width,
              height: piece.height,
              background: piece.color,
              borderRadius: piece.round ? '999px' : '2px',
              animationDelay: `${piece.delay}s`,
              animationDuration: `${piece.duration}s`,
              '--drift': `${piece.drift}px`,
              '--rotate': `${piece.rotate}deg`,
            } as CSSProperties
          }
        />
      ))}
    </div>
  )
}
