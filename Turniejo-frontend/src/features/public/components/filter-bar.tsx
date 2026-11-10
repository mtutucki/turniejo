"use client"

import { X } from "lucide-react"
import { type ReactNode, useId } from "react"

import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectSeparator,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { cn } from "@/lib/utils"

/** Wartość „bez filtra” w selectach (Radix nie przyjmuje pustej wartości). */
export const ALL = "all"

/** Karta z filtrami: lista turniejów i mecze w portalu wyglądają tak samo. */
export function FilterPanel({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div
      role="search"
      aria-label={label}
      className="bg-card space-y-3 rounded-xl border p-3 shadow-xs sm:p-4"
    >
      {children}
    </div>
  )
}

const COLUMNS: Record<number, string> = {
  1: "lg:grid-cols-1",
  2: "lg:grid-cols-2",
  3: "lg:grid-cols-3",
  4: "lg:grid-cols-4",
  5: "lg:grid-cols-5",
}

/** Selecty w siatce: dwa obok siebie na telefonie, wszystkie w jednym rzędzie na desktopie. */
export function FilterGrid({ count, children }: { count: number; children: ReactNode }) {
  return (
    <div className={cn("grid grid-cols-2 gap-2 sm:gap-3", COLUMNS[Math.min(count, 5)])}>
      {children}
    </div>
  )
}

export function FilterSelect({
  label,
  value,
  onChange,
  allLabel,
  disabled,
  children,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  allLabel: string
  disabled?: boolean
  children: ReactNode
}) {
  const id = useId()
  return (
    <div className="min-w-0 space-y-1">
      <span id={id} className="text-muted-foreground text-xs font-medium">
        {label}
      </span>
      <Select value={value} onValueChange={onChange} disabled={disabled}>
        <SelectTrigger
          aria-label={label}
          className={cn(
            "h-10 w-full min-w-0",
            value !== ALL && "border-primary/60 bg-primary/5 dark:bg-primary/10",
          )}
        >
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={ALL}>{allLabel}</SelectItem>
          <SelectSeparator />
          {children}
        </SelectContent>
      </Select>
    </div>
  )
}

export function FilterOption({
  value,
  count,
  children,
}: {
  value: string
  count?: number
  children: ReactNode
}) {
  return (
    <SelectItem value={value}>
      {children}
      {count !== undefined && <span className="text-muted-foreground tabular-nums">{count}</span>}
    </SelectItem>
  )
}

export function DateField({
  label,
  value,
  min,
  max,
  onChange,
}: {
  label: string
  value: string
  min?: string
  max?: string
  onChange: (value: string) => void
}) {
  const id = useId()
  return (
    <div className="min-w-0 space-y-1">
      <label htmlFor={id} className="text-muted-foreground text-xs font-medium">
        {label}
      </label>
      <Input
        id={id}
        type="date"
        value={value}
        min={min}
        max={max}
        onChange={(e) => onChange(e.target.value)}
        className="h-10"
      />
    </div>
  )
}

export type ActiveFilter = { key: string; label: string; clear: () => void }

/** „Znaleziono 5 meczów” i aktywne filtry jako etykiety z ✕. */
export function ActiveFilters({
  summary,
  filters,
  onClearAll,
}: {
  summary: string
  filters: ActiveFilter[]
  onClearAll: () => void
}) {
  return (
    <div className="flex flex-wrap items-center gap-2 text-sm">
      <span className="text-muted-foreground mr-1" role="status">
        {summary}
      </span>
      {filters.map((f) => (
        <span
          key={f.key}
          className="bg-primary/10 text-primary flex max-w-full items-center gap-1 rounded-full py-0.5 pr-1 pl-2.5 text-xs font-medium"
        >
          <span className="truncate">{f.label}</span>
          <button
            type="button"
            onClick={f.clear}
            aria-label={`Usuń filtr ${f.label}`}
            className="hover:bg-primary/15 rounded-full p-0.5"
          >
            <X className="size-3.5" />
          </button>
        </span>
      ))}
      {filters.length > 1 && (
        <button
          type="button"
          onClick={onClearAll}
          className="text-muted-foreground hover:text-foreground text-xs font-medium underline-offset-2 hover:underline"
        >
          Wyczyść wszystko
        </button>
      )}
    </div>
  )
}
