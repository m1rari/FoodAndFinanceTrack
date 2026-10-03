import { useId } from 'react'

export type IllustrationName = 'ledger' | 'plate' | 'chart' | 'error'

interface Props {
  name: IllustrationName
}

/** Локальная SVG-графика: без внешних запросов, наследует палитру приложения. */
export default function Illustration({ name }: Props) {
  const uid = useId().replace(/[^a-zA-Z0-9_-]/g, '')
  const surface = `${uid}-surface`
  const brand = `${uid}-brand`
  const gold = `${uid}-gold`

  const defs = (
    <defs>
      <linearGradient id={surface} x1="0" y1="0" x2="1" y2="1">
        <stop offset="0" stopColor="#1b2336" />
        <stop offset="1" stopColor="#121a29" />
      </linearGradient>
      <linearGradient id={brand} x1="0" y1="0" x2="1" y2="1">
        <stop offset="0" stopColor="#7ba4ff" />
        <stop offset="1" stopColor="#5b8cff" />
      </linearGradient>
      <linearGradient id={gold} x1="0" y1="0" x2="1" y2="1">
        <stop offset="0" stopColor="#ffd479" />
        <stop offset="1" stopColor="#e8a52c" />
      </linearGradient>
    </defs>
  )

  return (
    <svg className="illus" viewBox="0 0 200 150" role="presentation" aria-hidden="true">
      {defs}
      <ellipse className="illus-shadow" cx="100" cy="134" rx="56" ry="7" fill="rgba(0,0,0,0.4)" />

      {name === 'ledger' && (
        <>
          <g className="illus-float">
            <path
              d="M64 16h72v104l-12-8.5-12 8.5-12-8.5-12 8.5-12-8.5-12 8.5z"
              fill={`url(#${surface})`}
              stroke="rgba(255,255,255,0.14)"
              strokeWidth="2"
            />
            <rect x="76" y="32" width="48" height="7" rx="3.5" fill="rgba(233,237,246,0.16)" />
            <rect x="76" y="46" width="30" height="7" rx="3.5" fill={`url(#${brand})`} opacity="0.9" />
            <rect x="76" y="60" width="42" height="7" rx="3.5" fill="rgba(233,237,246,0.12)" />
            <rect x="76" y="80" width="34" height="11" rx="5.5" fill="rgba(53,208,127,0.22)" stroke="rgba(53,208,127,0.55)" />
            <circle cx="146" cy="110" r="16" fill={`url(#${gold})`} opacity="0.9" />
            <circle cx="146" cy="110" r="16" fill="none" stroke="rgba(255,255,255,0.28)" strokeWidth="2" />
            <path d="M139 110a7 7 0 0 0 14 0" fill="none" stroke="rgba(20,27,43,0.55)" strokeWidth="2.6" strokeLinecap="round" />
            <path d="M42 44l1.7 4.3 4.3 1.7-4.3 1.7L42 56l-1.7-4.3L36 50l4.3-1.7z" fill="#8b6cff" opacity="0.85" />
            <path d="M158 40l1.2 3 3 1.2-3 1.2-1.2 3-1.2-3-3-1.2 3-1.2z" fill="#5b8cff" opacity="0.8" />
          </g>
        </>
      )}

      {name === 'plate' && (
        <>
          <g className="illus-steam" opacity="0.5">
            <path d="M84 34c6 5 6 11 0 16" fill="none" stroke="#e9edf6" strokeWidth="3" strokeLinecap="round" />
          </g>
          <g className="illus-steam illus-delay-1" opacity="0.35">
            <path d="M100 26c6 5 6 11 0 16" fill="none" stroke="#e9edf6" strokeWidth="3" strokeLinecap="round" />
          </g>
          <g className="illus-steam illus-delay-2" opacity="0.3">
            <path d="M116 34c6 5 6 11 0 16" fill="none" stroke="#e9edf6" strokeWidth="3" strokeLinecap="round" />
          </g>
          <g className="illus-float">
            <circle cx="100" cy="88" r="44" fill={`url(#${surface})`} stroke="rgba(255,255,255,0.13)" strokeWidth="2" />
            <circle cx="100" cy="88" r="32" fill="none" stroke="rgba(255,255,255,0.09)" strokeWidth="2" />
            <circle cx="100" cy="88" r="22" fill="rgba(91,140,255,0.10)" />
            <path d="M88 86c4-7 12-8 16-2 3 5-1 11-8 11-6 0-10-4-8-9z" fill="#35d07f" opacity="0.85" />
            <circle cx="109" cy="80" r="5.2" fill="#f5b942" opacity="0.85" />
            <circle cx="94" cy="76" r="4" fill="#ff6b8a" opacity="0.7" />
            <path d="M34 54v13a5.5 5.5 0 0 0 11 0V54" fill="none" stroke="rgba(233,237,246,0.5)" strokeWidth="3" strokeLinecap="round" />
            <path d="M39.5 72v32" fill="none" stroke="rgba(233,237,246,0.5)" strokeWidth="3" strokeLinecap="round" />
            <path d="M167 54c-6 6-9 13-9 19 0 5 3 8 9 8v23" fill="none" stroke="rgba(233,237,246,0.5)" strokeWidth="3" strokeLinecap="round" />
          </g>
        </>
      )}

      {name === 'chart' && (
        <>
          <g className="illus-float">
            <rect x="44" y="22" width="112" height="98" rx="16" fill={`url(#${surface})`} stroke="rgba(255,255,255,0.11)" strokeWidth="2" />
            <rect x="62" y="80" width="18" height="24" rx="6" fill={`url(#${brand})`} opacity="0.45" />
            <rect x="90" y="66" width="18" height="38" rx="6" fill={`url(#${brand})`} opacity="0.72" />
            <rect x="118" y="50" width="18" height="54" rx="6" fill={`url(#${brand})`} />
            <path
              className="illus-dash"
              d="M62 98C84 96 100 76 136 46"
              fill="none"
              stroke="#35d07f"
              strokeWidth="2.6"
              strokeLinecap="round"
              strokeDasharray="7 7"
            />
            <path d="M138 42l-8.5 2.5 5 5.5z" fill="#35d07f" />
            <path d="M44 116h112" stroke="rgba(255,255,255,0.08)" strokeWidth="2" strokeLinecap="round" />
          </g>
        </>
      )}

      {name === 'error' && (
        <>
          <g className="illus-float">
            <rect x="52" y="20" width="96" height="104" rx="16" fill={`url(#${surface})`} stroke="rgba(255,255,255,0.11)" strokeWidth="2" />
            <path d="M100 46l30 52H70z" fill="rgba(255,107,107,0.14)" stroke="#ff6b6b" strokeWidth="3" strokeLinejoin="round" />
            <path d="M100 66v14" fill="none" stroke="#ff6b6b" strokeWidth="3.4" strokeLinecap="round" />
            <circle cx="100" cy="88" r="2.4" fill="#ff6b6b" />
            <path d="M38 48l6 6M44 48l-6 6" fill="none" stroke="rgba(255,107,107,0.55)" strokeWidth="2.4" strokeLinecap="round" />
            <path d="M160 74l6 6M166 74l-6 6" fill="none" stroke="rgba(255,107,107,0.4)" strokeWidth="2.4" strokeLinecap="round" />
          </g>
        </>
      )}
    </svg>
  )
}

/** Логотип приложения: знак «рост + монета». Используется в сплэше и шапке. */
export function LogoMark({ size = 72 }: { size?: number }) {
  const uid = useId().replace(/[^a-zA-Z0-9_-]/g, '')
  const gradient = `${uid}-logo`

  return (
    <svg className="logo-mark" viewBox="0 0 64 64" width={size} height={size} aria-hidden="true">
      <defs>
        <linearGradient id={gradient} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#7ba4ff" />
          <stop offset="0.55" stopColor="#5b8cff" />
          <stop offset="1" stopColor="#8b6cff" />
        </linearGradient>
      </defs>
      <rect x="2" y="2" width="60" height="60" rx="18" fill="#101728" stroke="rgba(255,255,255,0.10)" />
      <circle
        className="logo-ring"
        cx="32"
        cy="32"
        r="19"
        fill="none"
        stroke={`url(#${gradient})`}
        strokeWidth="4.5"
        strokeLinecap="round"
        strokeDasharray="96 24"
      />
      <path
        d="M23 38l6.5-7 4.5 4.5L41 27"
        fill="none"
        stroke="#e9edf6"
        strokeWidth="3.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path d="M35.6 27H41v5.4" fill="none" stroke="#e9edf6" strokeWidth="3.6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}
