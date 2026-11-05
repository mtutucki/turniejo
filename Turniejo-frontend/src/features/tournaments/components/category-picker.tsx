"use client"

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"

/** Wybór kategorii jako lista rozwijana (przy jednej kategorii nie ma czego wybierać). */
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

  return (
    <div className="flex items-center gap-2">
      <label htmlFor="category-picker" className="text-muted-foreground shrink-0 text-sm">
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
