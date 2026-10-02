"use client"

import { cn } from "@/lib/utils"
import type { PublicMatch } from "@/types/public"

import { usePublicContext } from "../public-context"

export type MatchFilter =
  { kind: "all" } | { kind: "group"; id: string } | { kind: "pitch"; id: string }

export const ALL_MATCHES: MatchFilter = { kind: "all" }

export function matchesFilter(match: PublicMatch, filter: MatchFilter) {
  if (filter.kind === "group") return match.groupId === filter.id
  if (filter.kind === "pitch") return match.pitchId === filter.id
  return true
}

/** Poziomy pasek pigułek: [Wszystkie] [Grupa A] … [Boisko 1] … — przewijany na telefonie. */
export function MatchFilterPills({
  value,
  onChange,
}: {
  value: MatchFilter
  onChange: (filter: MatchFilter) => void
}) {
  const { tournament } = usePublicContext()
  const multipleCategories = tournament.categories.length > 1
  const categoryName = (id: string) => tournament.categories.find((c) => c.id === id)?.name

  const pills: { key: string; label: string; filter: MatchFilter }[] = [
    { key: "all", label: "Wszystkie", filter: ALL_MATCHES },
    // W pucharze mecze nie należą do grup — grupa (np. pozostałość po zmianie formatu) dawałaby
    // zawsze pusty wynik.
    ...(tournament.format === "Knockout"
      ? []
      : tournament.groups.map((g) => ({
          key: `g-${g.id}`,
          // Przy kilku kategoriach "Grupa A" byłaby niejednoznaczna.
          label: multipleCategories ? `${categoryName(g.categoryId)} · ${g.name}` : g.name,
          filter: { kind: "group", id: g.id } as MatchFilter,
        }))),
    ...(tournament.pitches.length > 1
      ? tournament.pitches.map((p) => ({
          key: `p-${p.id}`,
          label: p.name,
          filter: { kind: "pitch", id: p.id } as MatchFilter,
        }))
      : []),
  ]

  const isActive = (filter: MatchFilter) =>
    filter.kind === value.kind && ("id" in filter ? "id" in value && filter.id === value.id : true)

  return (
    <div className="-mx-4 overflow-x-auto px-4 pb-1" role="group" aria-label="Filtr meczów">
      <div className="flex min-w-max gap-2">
        {pills.map(({ key, label, filter }) => (
          <button
            key={key}
            type="button"
            aria-pressed={isActive(filter)}
            onClick={() => onChange(filter)}
            className={cn(
              "h-9 rounded-full border px-4 text-sm font-medium whitespace-nowrap transition-colors",
              isActive(filter)
                ? "bg-primary text-primary-foreground border-primary"
                : "bg-card text-muted-foreground hover:text-foreground",
            )}
          >
            {label}
          </button>
        ))}
      </div>
    </div>
  )
}
