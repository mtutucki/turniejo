"use client"

import { Search, Trophy, X } from "lucide-react"
import { type ReactNode, useMemo, useState } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import type { PublicTournamentListItem } from "@/types/public"

import { usePublicTournaments } from "../api"
import {
  type DirectorySection,
  filterTournaments,
  groupTournaments,
  organizersOf,
} from "../directory"
import { DirectoryCard } from "./directory-card"

/** Ile zakończonych pokazać, zanim kibic kliknie „Pokaż więcej”. */
const FINISHED_PREVIEW = 6
/** Chipy tylko dla najaktywniejszych organizatorów, resztę znajdzie wyszukiwarka. */
const MAX_CHIPS = 8

const SECTIONS: { key: DirectorySection; title: string }[] = [
  { key: "live", title: "Trwają teraz" },
  { key: "upcoming", title: "Nadchodzące" },
  { key: "finished", title: "Zakończone" },
]

/** Filtr w adresie (?q=…&org=…), żeby dało się podesłać link do turniejów jednego organizatora. */
function writeUrl(query: string, organizerId: string | null) {
  const params = new URLSearchParams(window.location.search)
  if (query) params.set("q", query)
  else params.delete("q")
  if (organizerId) params.set("org", organizerId)
  else params.delete("org")
  const search = params.toString()
  window.history.replaceState(null, "", search ? `?${search}` : window.location.pathname)
}

export function TournamentDirectory({
  initialData,
  initialQuery = "",
  initialOrganizerId = null,
}: {
  initialData?: PublicTournamentListItem[]
  initialQuery?: string
  initialOrganizerId?: string | null
}) {
  const { data, isPending, isError, refetch } = usePublicTournaments(initialData)
  const [query, setQuery] = useState(initialQuery)
  const [organizerId, setOrganizerId] = useState<string | null>(initialOrganizerId)
  const [showAllFinished, setShowAllFinished] = useState(false)

  const all = useMemo(() => data ?? [], [data])
  const organizers = useMemo(() => organizersOf(all), [all])
  const groups = useMemo(
    () => groupTournaments(filterTournaments(all, { query, organizerId })),
    [all, query, organizerId],
  )

  function changeQuery(value: string) {
    setQuery(value)
    writeUrl(value, organizerId)
  }
  function changeOrganizer(id: string | null) {
    setOrganizerId(id)
    writeUrl(query, id)
  }
  function clearFilters() {
    setQuery("")
    setOrganizerId(null)
    writeUrl("", null)
  }

  if (isPending) {
    return (
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3" aria-busy="true">
        {[0, 1, 2].map((i) => (
          <Skeleton key={i} className="h-36 rounded-xl" />
        ))}
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

  const selected = organizers.find((o) => o.id === organizerId)
  // Wybrany organizator spoza pierwszej ósemki też dostaje chip, inaczej nie widać, że filtr działa.
  const chips = organizers.slice(0, MAX_CHIPS)
  if (selected && !chips.includes(selected)) chips.push(selected)
  const found = groups.live.length + groups.upcoming.length + groups.finished.length

  return (
    <div className="space-y-8">
      <div className="space-y-3">
        <div className="relative">
          <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" />
          <Input
            type="search"
            value={query}
            onChange={(e) => changeQuery(e.target.value)}
            placeholder="Szukaj turnieju albo organizatora"
            aria-label="Szukaj turnieju albo organizatora"
            className="h-11 pl-9"
          />
        </div>
        {organizers.length > 1 && (
          <div role="group" aria-label="Organizator" className="flex flex-wrap gap-2">
            <Chip active={!organizerId} onClick={() => changeOrganizer(null)}>
              Wszyscy
            </Chip>
            {chips.map((o) => (
              <Chip
                key={o.id}
                active={o.id === organizerId}
                onClick={() => changeOrganizer(o.id === organizerId ? null : o.id)}
              >
                <span className="truncate">{o.name}</span>
                <span className="opacity-60">{o.count}</span>
              </Chip>
            ))}
          </div>
        )}
      </div>

      {found === 0 ? (
        <EmptyState
          icon={Search}
          title="Nic nie znaleziono"
          description="Spróbuj innej nazwy albo wyczyść filtry."
          action={
            <Button variant="outline" onClick={clearFilters}>
              <X /> Wyczyść filtry
            </Button>
          }
        />
      ) : (
        SECTIONS.map(({ key, title }) => {
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
                {title}
                <span className="font-normal">({items.length})</span>
              </h2>
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                {visible.map((t) => (
                  <DirectoryCard
                    key={t.slug}
                    tournament={t}
                    section={key}
                    onOrganizerClick={changeOrganizer}
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

function Chip({
  active,
  onClick,
  children,
}: {
  active: boolean
  onClick: () => void
  children: ReactNode
}) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      className={cn(
        "flex max-w-full items-center gap-1.5 rounded-full border px-3 py-1.5 text-sm font-medium transition-colors",
        active ? "bg-primary text-primary-foreground border-primary" : "bg-card hover:bg-accent",
      )}
    >
      {children}
    </button>
  )
}
