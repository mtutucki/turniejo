"use client"

import { ListOrdered } from "lucide-react"
import { LayoutGroup, motion } from "motion/react"
import Link from "next/link"
import { useState } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"

import { usePublicStandings } from "../api"
import { readFavoriteTeam } from "../favorite-team"
import { usePublicContext } from "../public-context"

const COLUMNS =
  "grid grid-cols-[2rem_minmax(0,1fr)_2rem_2.5rem] sm:grid-cols-[2rem_minmax(0,1fr)_2rem_2rem_2rem_2rem_3.5rem_2.5rem_2.5rem] items-center gap-x-2"

function GroupTable({
  groupId,
  title,
  headingLevel,
}: {
  groupId: string
  title: string
  /** h3 pod nagłówkiem kategorii, h2 gdy kategoria jest jedna (bez przeskoku h1 → h3). */
  headingLevel: "h2" | "h3"
}) {
  const Heading = headingLevel
  const { slug, tournament, lookups } = usePublicContext()
  const standings = usePublicStandings(slug, groupId)
  const [favoriteTeam] = useState(() => readFavoriteTeam(slug))
  const rows = [...(standings.data ?? [])].sort((a, b) => a.position - b.position)

  return (
    <section className="bg-card rounded-xl border" aria-label={`Tabela: ${title}`}>
      <Heading className="border-b px-4 py-3 font-semibold">{title}</Heading>
      {standings.isPending ? (
        <Skeleton className="m-4 h-32" />
      ) : rows.length === 0 ? (
        <p className="text-muted-foreground px-4 py-6 text-sm">Brak drużyn w grupie.</p>
      ) : (
        <div className="px-2 pb-2">
          <div
            className={cn(COLUMNS, "text-muted-foreground px-2 py-2 text-xs font-medium")}
            aria-hidden
          >
            <span>#</span>
            <span>Drużyna</span>
            <span className="text-center">M</span>
            <span className="hidden text-center sm:block">W</span>
            <span className="hidden text-center sm:block">R</span>
            <span className="hidden text-center sm:block">P</span>
            <span className="hidden text-center sm:block">Bramki</span>
            <span className="hidden text-center sm:block">+/−</span>
            <span className="text-center">Pkt</span>
          </div>
          <LayoutGroup id={groupId}>
            <ol>
              {rows.map((row) => (
                // layout: przy zmianie kolejności wiersz płynnie przesuwa się na nową pozycję.
                <motion.li
                  key={row.tournamentTeamId}
                  layout
                  transition={{ type: "spring", stiffness: 500, damping: 40 }}
                  className="relative"
                  data-testid="standing-row"
                >
                  {/* Klucz z pozycją: nowa pozycja = nowy element = krótkie podświetlenie wiersza. */}
                  <motion.div
                    key={`${row.tournamentTeamId}-${row.position}-${row.points}`}
                    initial={{ backgroundColor: "rgba(16, 185, 129, 0.25)" }}
                    animate={{ backgroundColor: "rgba(16, 185, 129, 0)" }}
                    transition={{ duration: 1.6 }}
                    className={cn(
                      COLUMNS,
                      "rounded-lg px-2 py-2.5 text-sm",
                      // Drużyna wybrana w filtrze meczów — łatwiej znaleźć "swoich" w tabeli.
                      row.tournamentTeamId === favoriteTeam && "ring-primary/60 ring-1 ring-inset",
                    )}
                    data-favorite={row.tournamentTeamId === favoriteTeam || undefined}
                  >
                    <span className="text-muted-foreground font-mono tabular-nums">
                      {row.position}
                    </span>
                    {tournament.settings.showTeams ? (
                      <Link
                        href={`/t/${slug}/teams/${row.tournamentTeamId}`}
                        className="truncate font-medium hover:underline"
                      >
                        {lookups.teamName(row.tournamentTeamId)}
                      </Link>
                    ) : (
                      <span className="truncate font-medium">
                        {lookups.teamName(row.tournamentTeamId)}
                      </span>
                    )}
                    <span className="text-center font-mono tabular-nums">{row.matchesPlayed}</span>
                    <span className="hidden text-center font-mono tabular-nums sm:block">
                      {row.wins}
                    </span>
                    <span className="hidden text-center font-mono tabular-nums sm:block">
                      {row.draws}
                    </span>
                    <span className="hidden text-center font-mono tabular-nums sm:block">
                      {row.losses}
                    </span>
                    <span className="hidden text-center font-mono tabular-nums sm:block">
                      {row.goalsFor}:{row.goalsAgainst}
                    </span>
                    <span className="hidden text-center font-mono tabular-nums sm:block">
                      {row.goalDifference > 0 ? `+${row.goalDifference}` : row.goalDifference}
                    </span>
                    <span className="text-center font-mono font-semibold tabular-nums">
                      {row.points}
                    </span>
                  </motion.div>
                </motion.li>
              ))}
            </ol>
          </LayoutGroup>
        </div>
      )}
    </section>
  )
}

export function StandingsView() {
  const { tournament } = usePublicContext()
  if (tournament.groups.length === 0) {
    return <EmptyState icon={ListOrdered} title="Tabele pojawią się po utworzeniu grup" />
  }

  return (
    <div className="space-y-8">
      {tournament.categories.map((category) => {
        const groups = tournament.groups.filter((g) => g.categoryId === category.id)
        if (groups.length === 0) return null
        return (
          <section key={category.id} className="space-y-3">
            {tournament.categories.length > 1 && (
              <h2 className="text-muted-foreground text-xs font-semibold tracking-wide uppercase">
                {category.name}
              </h2>
            )}
            <div className="grid gap-4 lg:grid-cols-2">
              {groups.map((group) => (
                <GroupTable
                  key={group.id}
                  groupId={group.id}
                  title={group.name}
                  headingLevel={tournament.categories.length > 1 ? "h3" : "h2"}
                />
              ))}
            </div>
          </section>
        )
      })}
      <p className="text-muted-foreground text-xs">
        M – mecze, W – wygrane, R – remisy, P – porażki. Tabela liczy zakończone mecze.
      </p>
    </div>
  )
}
