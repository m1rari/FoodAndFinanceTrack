interface Props {
  rows?: number
}

export default function Skeleton({ rows = 4 }: Props) {
  return (
    <div className="skeleton-list" role="status" aria-live="polite">
      <span className="sr-only">Загрузка…</span>
      {Array.from({ length: rows }).map((_, index) => (
        <div key={index} className="skeleton-row" aria-hidden="true" />
      ))}
    </div>
  )
}
