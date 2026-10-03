import type { ReactNode, SVGProps } from 'react'

/**
 * Единый набор иконок: сетка 24×24, обводка currentColor, скруглённые концы.
 * Стиль согласован с дизайн-системой «Ledger» (см. index.css).
 */
const PATHS = {
  // --- навигация и экраны ---
  ledger: (
    <>
      <path d="M6.5 3.5h11v17l-2.75-1.9-2.75 1.9-2.75-1.9L6.5 20.5z" />
      <path d="M9.8 8.2h4.4M9.8 12h4.4" />
    </>
  ),
  chart: (
    <>
      <path d="M4 20.5h16" />
      <path d="M7 20.5V14M12 20.5V6.5M17 20.5V11" />
    </>
  ),

  // --- действия ---
  plus: <path d="M12 5.5v13M5.5 12h13" />,
  close: <path d="M6.2 6.2l11.6 11.6M17.8 6.2L6.2 17.8" />,
  check: <path d="M4.8 12.6l4.8 4.8L19.2 6.6" />,
  'chevron-left': <path d="M14.5 5.5L8 12l6.5 6.5" />,
  'chevron-right': <path d="M9.5 5.5L16 12l-6.5 6.5" />,
  'chevron-down': <path d="M5.5 9.5L12 16l6.5-6.5" />,
  'arrow-up': <path d="M12 19.5V5M5.8 11.2L12 5l6.2 6.2" />,
  'arrow-down': <path d="M12 4.5V19M5.8 12.8L12 19l6.2-6.2" />,
  swap: <path d="M4.5 8.5h13l-3.2-3.2M19.5 15.5h-13l3.2 3.2" />,
  refresh: (
    <>
      <path d="M12 3.6a8.4 8.4 0 1 0 8.4 8.4" />
      <path d="M20.4 7.8v4.2h-4.2" />
    </>
  ),
  camera: (
    <>
      <path d="M4.5 7.5h3.2l1.5-2.4h5.6l1.5 2.4h3.2a2 2 0 0 1 2 2v7.1a2 2 0 0 1-2 2H4.5a2 2 0 0 1-2-2V9.5a2 2 0 0 1 2-2z" />
      <circle cx="12" cy="13.4" r="3.4" />
    </>
  ),
  image: (
    <>
      <rect x="3" y="4.5" width="18" height="15" rx="3" />
      <circle cx="8.6" cy="10" r="1.7" />
      <path d="M4 17.6l4.6-4.4 3.6 3.4 2.8-2.6 4.9 4.6" />
    </>
  ),
  file: (
    <>
      <path d="M13.6 3.5H7.5a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2h9a2 2 0 0 0 2-2V8.5z" />
      <path d="M13.6 3.5v5h4.9" />
      <path d="M9 13.5h6M9 17h4" />
    </>
  ),
  pencil: (
    <>
      <path d="M4.2 19.8l.7-4.2L16.4 4.1a2.1 2.1 0 0 1 3 0l.5.5a2.1 2.1 0 0 1 0 3L8.4 19.1z" />
      <path d="M15.2 5.3l3.5 3.5" />
    </>
  ),
  share: (
    <>
      <circle cx="17.5" cy="5.5" r="2.6" />
      <circle cx="6.5" cy="12" r="2.6" />
      <circle cx="17.5" cy="18.5" r="2.6" />
      <path d="M8.9 10.8l6.2-3.6M8.9 13.2l6.2 3.6" />
    </>
  ),
  trash: (
    <>
      <path d="M4 6.5h16" />
      <path d="M9.5 6.5V4.8a1.3 1.3 0 0 1 1.3-1.3h2.4a1.3 1.3 0 0 1 1.3 1.3v1.7" />
      <path d="M18.4 6.5l-.8 12.4a2 2 0 0 1-2 1.9H8.4a2 2 0 0 1-2-1.9L5.6 6.5" />
      <path d="M10.2 10.8v6M13.8 10.8v6" />
    </>
  ),
  star: <path d="M12 3.9l2.55 5.2 5.7.83-4.12 4.02.97 5.68L12 16.95l-5.1 2.68.97-5.68L3.75 9.93l5.7-.83z" />,
  'star-filled': <path d="M12 3.9l2.55 5.2 5.7.83-4.12 4.02.97 5.68L12 16.95l-5.1 2.68.97-5.68L3.75 9.93l5.7-.83z" />,
  more: (
    <>
      <circle cx="5.6" cy="12" r="1.5" />
      <circle cx="12" cy="12" r="1.5" />
      <circle cx="18.4" cy="12" r="1.5" />
    </>
  ),
  sparkles: (
    <>
      <path d="M11 3.8l1.6 4 4 1.6-4 1.6L11 15l-1.6-4-4-1.6 4-1.6z" />
      <path d="M18 15.2l.75 1.85 1.85.75-1.85.75-.75 1.85-.75-1.85-1.85-.75 1.85-.75z" />
    </>
  ),
  info: (
    <>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 11.2v5.2" />
      <path d="M12 7.8h.01" />
    </>
  ),
  alert: (
    <>
      <path d="M12 4.2l8.6 15.2H3.4z" />
      <path d="M12 9.6v4.6" />
      <path d="M12 17.2h.01" />
    </>
  ),
  filter: <path d="M4 6.5h16M7.5 12h9M10.5 17.5h3" />,
  calendar: (
    <>
      <rect x="3.5" y="5" width="17" height="15" rx="3" />
      <path d="M3.5 10h17M8 3.2v3.6M16 3.2v3.6" />
    </>
  ),

  // --- категории ---
  cart: (
    <>
      <path d="M3 4.5h2.3l2.4 10a1.7 1.7 0 0 0 1.7 1.3h7.5a1.7 1.7 0 0 0 1.7-1.3l1.4-6.2H6.1" />
      <circle cx="9.6" cy="19.2" r="1.5" />
      <circle cx="17.4" cy="19.2" r="1.5" />
    </>
  ),
  bus: (
    <>
      <rect x="4" y="4.5" width="16" height="12.5" rx="3" />
      <path d="M4 12h16" />
      <path d="M7.5 17v2.4M16.5 17v2.4" />
      <circle cx="8.6" cy="14.6" r="1" />
      <circle cx="15.4" cy="14.6" r="1" />
    </>
  ),
  home: (
    <>
      <path d="M4 10.4L12 4l8 6.4V19a1.6 1.6 0 0 1-1.6 1.6H5.6A1.6 1.6 0 0 1 4 19z" />
      <path d="M9.6 20.6v-6.2h4.8v6.2" />
    </>
  ),
  pulse: <path d="M3 12.6h3.8l2.1-5.4 3.6 10.4 2.4-6.2 1.5 1.2h4.6" />,
  ticket: (
    <>
      <path d="M4 8.6a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v1.1a2.3 2.3 0 0 0 0 4.6v1.1a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2v-1.1a2.3 2.3 0 0 0 0-4.6z" />
      <path d="M13.6 7.4v1.8M13.6 14.8v1.8" />
    </>
  ),
  shirt: (
    <path d="M9.2 3.8L4 6.4l1.6 4.2 1.9-.8v9.4a1 1 0 0 0 1 1h7a1 1 0 0 0 1-1V9.8l1.9.8L20 6.4l-5.2-2.6-1.4 1.9h-2.8z" />
  ),
  phone: (
    <>
      <rect x="6.5" y="3" width="11" height="18" rx="3" />
      <path d="M10.6 18.2h2.8" />
    </>
  ),
  'credit-card': (
    <>
      <rect x="3" y="5.5" width="18" height="13" rx="3" />
      <path d="M3 10.2h18" />
      <path d="M6.8 14.6h3.4" />
    </>
  ),
  briefcase: (
    <>
      <rect x="3" y="7.4" width="18" height="12.2" rx="2.6" />
      <path d="M9.2 7.4V5.9a2 2 0 0 1 2-2h1.6a2 2 0 0 1 2 2v1.5" />
      <path d="M3 12.6h18" />
    </>
  ),
  banknote: (
    <>
      <rect x="2.6" y="6.6" width="18.8" height="10.8" rx="2.6" />
      <circle cx="12" cy="12" r="2.6" />
      <path d="M6.2 12h.01M17.8 12h.01" />
    </>
  ),
  tag: (
    <>
      <path d="M4 10.7V5.6A1.6 1.6 0 0 1 5.6 4h5.1l9.3 9.3a1.6 1.6 0 0 1 0 2.3l-4.4 4.4a1.6 1.6 0 0 1-2.3 0z" />
      <path d="M8.2 8.2h.01" />
    </>
  ),

  // --- питание ---
  flame: (
    <path d="M12 3.4c3.1 3.5 5 6 5 8.9a5 5 0 0 1-10 0c0-1.7.7-3.1 1.9-4.6.5 1.4 1.4 2.1 2.5 2.1.9 0 1.6-.7 1.6-1.9 0-1.3-.4-2.6-1-4.5z" />
  ),
  egg: <path d="M12 3.6c3.4 0 6.1 4.5 6.1 8.7a6.1 6.1 0 0 1-12.2 0C5.9 8.1 8.6 3.6 12 3.6z" />,
  droplet: <path d="M12 3.6s5.6 6.1 5.6 9.9a5.6 5.6 0 0 1-11.2 0C6.4 9.7 12 3.6 12 3.6z" />,
  wheat: (
    <>
      <path d="M12 20.6V8.4" />
      <path d="M12 9.2c0-2 1.3-3.3 3.2-3.5.2 2-1.1 3.3-3.2 3.5zM12 9.2c0-2-1.3-3.3-3.2-3.5-.2 2 1.1 3.3 3.2 3.5z" />
      <path d="M12 14.4c0-2 1.3-3.3 3.2-3.5.2 2-1.1 3.3-3.2 3.5zM12 14.4c0-2-1.3-3.3-3.2-3.5-.2 2 1.1 3.3 3.2 3.5z" />
    </>
  ),
  utensils: (
    <>
      <path d="M7 3.4v6.4a2.6 2.6 0 0 0 5.2 0V3.4" />
      <path d="M9.6 12.4v8.2" />
      <path d="M16.6 3.4c-1.6 1.7-2.1 3.8-2.1 5.8 0 1.7.7 2.6 2.1 2.6v8.8" />
    </>
  ),
  sunrise: (
    <>
      <path d="M12 3.4v4.2" />
      <path d="M5.6 9.8l2.9 2.9M18.4 9.8l-2.9 2.9" />
      <path d="M3.4 18.6h17.2" />
      <path d="M7.2 18.6a4.8 4.8 0 0 1 9.6 0" />
    </>
  ),
  sun: (
    <>
      <circle cx="12" cy="12" r="4.2" />
      <path d="M12 2.6v2.4M12 19v2.4M2.6 12H5M19 12h2.4M5.3 5.3l1.7 1.7M17 17l1.7 1.7M18.7 5.3L17 7M7 17l-1.7 1.7" />
    </>
  ),
  moon: <path d="M20.2 14.6A8.6 8.6 0 0 1 9.4 3.8a8.6 8.6 0 1 0 10.8 10.8z" />,
  coffee: (
    <>
      <path d="M4 8.4h13.2v5.4a5.2 5.2 0 0 1-5.2 5.2H9.2A5.2 5.2 0 0 1 4 13.8z" />
      <path d="M17.2 9.6h1.6a2.8 2.8 0 0 1 0 5.6h-1.6" />
      <path d="M7.4 4.6v1.6M10.8 4v2M14.2 4.6v1.6" />
    </>
  ),
} satisfies Record<string, ReactNode>

export type IconName = keyof typeof PATHS

const FILLED = new Set<IconName>(['star-filled', 'more'])

interface Props extends Omit<SVGProps<SVGSVGElement>, 'name'> {
  name: IconName
  size?: number
}

export default function Icon({ name, size = 20, strokeWidth = 1.8, ...rest }: Props) {
  const filled = FILLED.has(name)

  return (
    <svg
      viewBox="0 0 24 24"
      width={size}
      height={size}
      fill={filled ? 'currentColor' : 'none'}
      stroke={filled ? 'none' : 'currentColor'}
      strokeWidth={filled ? undefined : strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      {...rest}
    >
      {PATHS[name]}
    </svg>
  )
}
