import { LogoMark } from './Illustration'

interface Props {
  label: string
}

/** Экран ожидания: анимированный знак + индикатор прогресса. */
export default function Splash({ label }: Props) {
  return (
    <div className="splash">
      <LogoMark size={78} />
      <p className="muted">{label}</p>
      <div className="splash-track" aria-hidden="true">
        <span />
      </div>
    </div>
  )
}
