"use client"

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { cn } from "@/lib/utils"

/** Do tylu kategorii pokazujemy przyciski; przy większej liczbie zawijałyby się w kilka rzędów. */
const MAX_PILLS = 4

/** Wybór kategorii: przyciski przy kilku kategoriach, lista rozwijana przy wielu. */
export function CategoryPicker({
  categories,
  value,
  onChange,
}: {
  categories: { id: string; name: string }[]
  value: string
  onChange: (categoryId: string) => void
}) {
  if (categories.length <= 1) return null

  if (categories.length > MAX_PILLS) {
    return (
      <div className="flex items-center gap-2">
        <label htmlFor="category-picker" className="text-muted-foreground text-sm">
          Kategoria:
        </label>
        <Select value={value} onValueChange={onChange}>
          <SelectTrigger id="category-picker" className="w-full max-w-xs">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {categories.map((c) => (
              <SelectItem key={c.id} value={c.id}>
                {c.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    )
  }

  return (
    <div className="flex flex-wrap items-center gap-2" role="group" aria-label="Kategoria">
      <span className="text-muted-foreground mr-1 text-sm">Kategoria:</span>
      {categories.map((c) => (
        <button
          key={c.id}
          type="button"
          aria-pressed={c.id === value}
          onClick={() => onChange(c.id)}
          className={cn(
            "h-9 rounded-full border px-4 text-sm font-medium",
            c.id === value
              ? "bg-primary text-primary-foreground border-primary"
              : "bg-card text-muted-foreground",
          )}
        >
          {c.name}
        </button>
      ))}
    </div>
  )
}
