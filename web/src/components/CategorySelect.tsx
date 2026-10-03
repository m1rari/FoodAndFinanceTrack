import type { CSSProperties } from 'react'
import type { CategoryDto } from '../api/types'
import Icon from './Icon'
import { categoryVisual } from '../utils/visuals'

interface Props {
  value: string
  onChange: (value: string) => void
  categories: CategoryDto[]
  emptyLabel?: string
  name?: string
}

/** Select категорий с живым превью иконки и цвета выбранной категории. */
export default function CategorySelect({ value, onChange, categories, emptyLabel = 'Без категории', name }: Props) {
  const selected = categories.find((category) => category.id === value)
  const visual = categoryVisual(selected?.name, selected?.type)

  return (
    <div className="select-wrap">
      <span className="select-icon" style={{ '--cat': visual.color } as CSSProperties} aria-hidden="true">
        <Icon name={visual.icon} size={18} />
      </span>
      <select name={name} value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="">{emptyLabel}</option>
        {categories.map((category) => (
          <option key={category.id} value={category.id}>
            {category.name}
          </option>
        ))}
      </select>
    </div>
  )
}
