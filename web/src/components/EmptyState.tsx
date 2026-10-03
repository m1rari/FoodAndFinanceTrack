import type { ReactNode } from 'react'
import Illustration from './Illustration'
import type { IllustrationName } from './Illustration'

interface Props {
  art: IllustrationName
  text: string
  title?: string
  children?: ReactNode
}

export default function EmptyState({ art, text, title, children }: Props) {
  return (
    <div className="empty">
      <Illustration name={art} />
      {title && <p className="empty-title">{title}</p>}
      <p className="muted small">{text}</p>
      {children}
    </div>
  )
}
