"use client"

import { Search, SlidersHorizontal, Trophy, X } from "lucide-react"
import { useMemo, useState } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { formatDate, plural } from "@/lib/format"
import type { PublicTournamentListItem } from "@/types/public"

import { usePublicTournaments } from "../api"
import {
  categoriesOf,
  type DateRangePreset,
  type DirectoryFilters,
  type DirectorySection,
  EMPTY_FILTERS,
  filtersToSearch,
  filterTournaments,
  groupTournaments,
  organizersOf,
  sectionOf,
  STATUS_LABELS,
  WHEN_LABELS,
} from "../directory"
import { DirectoryCard } from "./directory-card"
import {
  ActiveFilters,
  ALL,
  DateField,
  FilterGrid,
  FilterOption,
  FilterPanel,
  FilterSelect,
} from "./filter-bar"

/** Ile zakończonych pokazać, zanim kibic kliknie „Pokaż więcej”. */
const FINISHED_PREVIEW = 6
const SECTIONS: DirectorySection[] = ["live", "upcoming", "finished"]

/** Filtry w adresie, żeby dało się podesłać link np. do turniejów jednego organizatora. */
function writeUrl(filters: DirectoryFilters) {
  const search = filtersToSearch(filters)
  window.history.replaceState(null, "", search ? `?${search}` : window.location.pathname)
}

export function TournamentDirectory({
  initialData,
  initialFilters = EMPTY_FILTERS,
}: {
  initialData?: PublicTournamentListItem[]
  initialFilters?: DirectoryFilters
}) {
  const { data, isPending, isError, refetch } = usePublicTournaments(initialData)
  const [filters, setFilters] = useState(initialFilters)
  const [showAllFinished, setShowAllFinished] = useState(false)

  const all = useMemo(() => data ?? [], [data])
  const organizers = useMemo(() => organizersOf(all), [all])
  const categories = useMemo(() => categoriesOf(all), [all])
  const filtered = useMemo(() => filterTournaments(all, filters), [all, filters])
  const groups = useMemo(() => groupTournaments(filtered), [filtered])

  function update(patch: Partial<DirectoryFilters>) {
    const next = { ...filters, ...patch }
    setFilters(next)
    writeUrl(next)
  }

  if (isPending) {
    return (
      <div className="space-y-6" aria-busy="true">
        <Skeleton className="h-36 rounded-xl" />
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-36 rounded-xl" />
          ))}
        </div>
      </div>
    )
  }

  if (isError && all.length === 0) {
    return (
      <EmptyState
        icon={Trophy}
        title="Nie udało się wczytać turniejów"
        action={
          <Button variant="outline" onClick={() => refetch()}>
            Spróbuj ponownie
          </Button>
        }
      />
    )
  }

  if (all.length === 0) {
    return (
      <EmptyState
        icon={Trophy}
        title="Nie ma jeszcze publicznych turniejów"
        description="Gdy organizator udostępni turniej, pojawi się tutaj."
      />
    )
  }

  const organizer = organizers.find((o) => o.id === filters.organizerId)
  const statusCount = (s: DirectorySection) => all.filter((t) => sectionOf(t) === s).length

  const active: { key: string; label: string; patch: Partial<DirectoryFilters> }[] = []
  if (filters.query) active.push({ key: "q", label: `„${filters.query}”`, patch: { query: "" } })
  if (filters.status !== "all")
    active.push({ key: "status", label: STATUS_LABELS[filters.status], patch: { status: "all" } })
  if (filters.when !== "any" && (filters.when !== "custom" || filters.from || filters.to))
    active.push({
      key: "when",
      label: filters.when === "custom" ? customRangeLabel(filters) : WHEN_LABELS[filters.when],
      patch: { when: "any", from: "", to: "" },
    })
  if (filters.organizerId)
    active.push({
      key: "org",
      label: organizer?.name ?? "Organizator",
      patch: { organizerId: null },
    })
  if (filters.category)
    active.push({ key: "cat", label: filters.category, patch: { category: null } })

  return (
    <div className="space-y-8">
      <div className="space-y-3">
        <FilterPanel label="Filtry turniejów">
          <div className="relative">
            <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" />
            <Input
              type="search"
              value={filters.query}
              onChange={(e) => update({ query: e.target.value })}
              placeholder="Szukaj turnieju albo organizatora"
              aria-label="Szukaj turnieju albo organizatora"
              className="h-11 pl-9"
            />
          </div>

          <FilterGrid count={4}>
            <FilterSelect
              label="Status"
              value={filters.status}
              onChange={(v) => update({ status: v as DirectoryFilters["status"] })}
              allLabel="Wszystkie"
            >
              {SECTIONS.map((s) => (
                <FilterOption key={s} value={s} count={statusCount(s)}>
                  {STATUS_LABELS[s]}
                </FilterOption>
              ))}
            </FilterSelect>

            <FilterSelect
              label="Termin"
              value={filters.when === "any" ? ALL : filters.when}
              onChange={(v) =>
                update(
                  v === ALL
                    ? { when: "any", from: "", to: "" }
                    : { when: v as DateRangePreset, ...(v !== "custom" && { from: "", to: "" }) },
                )
              }
              allLabel="Dowolny"
            >
              {(Object.keys(WHEN_LABELS) as (keyof typeof WHEN_LABELS)[]).map((key) => (
                <FilterOption key={key} value={key}>
                  {WHEN_LABELS[key]}
                </FilterOption>
              ))}
            </FilterSelect>

            <FilterSelect
              label="Organizator"
              value={filters.organizerId ?? ALL}
              onChange={(v) => update({ organizerId: v === ALL ? null : v })}
              allLabel="Wszyscy"
            >
              {organizers.map((o) => (
                <FilterOption key={o.id} value={o.id} count={o.count}>
                  {o.name}
                </FilterOption>
              ))}
            </FilterSelect>

            <FilterSelect
              label="Kategoria"
              value={filters.category ?? ALL}
              onChange={(v) => update({ category: v === ALL ? null : v })}
              allLabel="Wszystkie"
              disabled={categories.length === 0}
            >
              {categories.map((c) => (
                <FilterOption key={c} value={c}>
                  {c}
                </FilterOption>
              ))}
            </FilterSelect>
          </FilterGrid>

          {filters.when === "custom" && (
            <div className="grid grid-cols-2 gap-2 sm:max-w-md sm:gap-3">
              <DateField
                label="Od"
                value={filters.from}
                max={filters.to || undefined}
                onChange={(from) => update({ from })}
              />
              <DateField
                label="Do"
                value={filters.to}
                min={filters.from || undefined}
                onChange={(to) => update({ to })}
              />
            </div>
          )}
        </FilterPanel>

        <ActiveFilters
          summary={`${active.length > 0 ? "Znaleziono " : ""}${plural(filtered.length, ["turniej", "turnieje", "turniejów"])}`}
          filters={active.map((f) => ({ ...f, clear: () => update(f.patch) }))}
          onClearAll={() => update(EMPTY_FILTERS)}
        />
      </div>

      {filtered.length === 0 ? (
        <EmptyState
          icon={SlidersHorizontal}
          title="Brak turniejów dla tych filtrów"
          description="Zmień filtry albo wyczyść je, żeby zobaczyć wszystkie turnieje."
          action={
            <Button variant="outline" onClick={() => update(EMPTY_FILTERS)}>
              <X /> Wyczyść filtry
            </Button>
          }
        />
      ) : (
        SECTIONS.map((key) => {
          const items = groups[key]
          if (items.length === 0) return null
          const visible =
            key === "finished" && !showAllFinished ? items.slice(0, FINISHED_PREVIEW) : items
          return (
            <section key={key} aria-labelledby={`section-${key}`} className="space-y-3">
              <h2
                id={`section-${key}`}
                className="text-muted-foreground flex items-center gap-2 text-sm font-semibold tracking-wide uppercase"
              >
                {key === "live" && <span className="bg-live size-2 rounded-full" />}
                {STATUS_LABELS[key]}
                <span className="font-normal">({items.length})</span>
              </h2>
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                {visible.map((t) => (
                  <DirectoryCard
                    key={t.slug}
                    tournament={t}
                    section={key}
                    onOrganizerClick={(organizerId) => {
                      update({ organizerId })
                      window.scrollTo({ top: 0, behavior: "smooth" })
                    }}
                  />
                ))}
              </div>
              {visible.length < items.length && (
                <Button
                  variant="outline"
                  className="w-full"
                  onClick={() => setShowAllFinished(true)}
                >
                  Pokaż więcej ({items.length - visible.length})
                </Button>
              )}
            </section>
          )
        })
      )}
    </div>
  )
}

function customRangeLabel({ from, to }: DirectoryFilters) {
  if (from && to) return `${formatDate(from)} – ${formatDate(to)}`
  return from ? `Od ${formatDate(from)}` : `Do ${formatDate(to)}`
}
