import type { CSSProperties } from 'react'
import { MACRO_COLORS } from '../utils/visuals'

interface Props {
  calories: string
  protein: number
  fat: number
  carbs: number
  size?: number
}

const RADIUS = 34
const STROKE = 9
const CIRCUMFERENCE = 2 * Math.PI * RADIUS

/** Кольцо распределения калорий по БЖУ; раскрытие — CSS-анимацией (без rAF). */
export default function MacroRing({ calories, protein, fat, carbs, size = 108 }: Props) {
  const proteinKcal = protein * 4
  const fatKcal = fat * 9
  const carbsKcal = carbs * 4
  const total = proteinKcal + fatKcal + carbsKcal
  const hasData = total > 0

  const segments = [
    { color: MACRO_COLORS.protein, share: hasData ? proteinKcal / total : 0 },
    { color: MACRO_COLORS.fat, share: hasData ? fatKcal / total : 0 },
    { color: MACRO_COLORS.carbs, share: hasData ? carbsKcal / total : 0 },
  ]

  const gap = hasData ? 5 : 0
  let cursor = 0

  return (
    <div className="ring" style={{ width: size, height: size }}>
      <svg viewBox="0 0 92 92" width={size} height={size} aria-hidden="true">
        <circle cx="46" cy="46" r={RADIUS} fill="none" stroke="rgba(255,255,255,0.07)" strokeWidth={STROKE} />
        <g transform="rotate(-90 46 46)">
          {segments.map((segment, index) => {
            const length = Math.max(0, segment.share * CIRCUMFERENCE - (index < segments.length - 1 ? gap : 0))
            const element = (
              <circle
                key={`${segment.color}-${Math.round(length)}`}
                className="ring-seg"
                cx="46"
                cy="46"
                r={RADIUS}
                fill="none"
                stroke={segment.color}
                strokeWidth={STROKE}
                strokeLinecap="round"
                strokeDashoffset={-cursor}
                style={
                  {
                    '--len': `${length} ${Math.max(0, CIRCUMFERENCE - length)}`,
                    '--circ': `${CIRCUMFERENCE}`,
                  } as CSSProperties
                }
              />
            )

            cursor += segment.share * CIRCUMFERENCE

            return element
          })}
        </g>
      </svg>

      <div className="ring-center">
        <strong>{calories}</strong>
        <span className="muted small">{hasData ? 'ккал' : 'нет данных'}</span>
      </div>
    </div>
  )
}
