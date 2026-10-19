"use client"

import { ListOrdered } from "lucide-react"
import { useParams } from "next/navigation"

import { EmptyState } from "@/components/layout/empty-state"
import { Skeleton } from "@/components/ui/skeleton"
import {
  useChildResults,
  useChildSchedule,
  useChildStanding,
  useChildTeams,
} from "@/features/panels/api"
import { SimpleMatchList } from "@/features/panels/components/simple-match-list"
import { cn } from "@/lib/utils"

function useChildTeamName(playerId: string) {
  return useChildTeams(playerId).data?.[0]?.teamName
}

export function ChildScheduleView() {
  const { id } = useParams<{ id: string }>()
  const schedule = useChildSchedule(id)
  return (
    <SimpleMatchList
      matches={schedule.data}
      isPending={schedule.isPending}
      highlightTeam={useChildTeamName(id)}
      emptyTitle="Brak zaplanowanych meczów"
    />
  )
}

export function ChildResultsView() {
  const { id } = useParams<{ id: string }>()
  const results = useChildResults(id)
  return (
    <SimpleMatchList
      matches={
        results.data
          ? [...results.data].sort((a, b) => b.scheduledAt.localeCompare(a.scheduledAt))
          : undefined
      }
      isPending={results.isPending}
      highlightTeam={useChildTeamName(id)}
      emptyTitle="Brak rozegranych meczów"
    />
  )
}

export function ChildStandingView() {
  const { id } = useParams<{ id: string }>()
  const standing = useChildStanding(id)

  if (standing.isPending) return <Skeleton className="h-48 w-full rounded-xl" />
  if (!standing.data?.length) {
    return (
      <EmptyState
        icon={ListOrdered}
        title="Brak tabeli"
        description="Tabela pojawia się w turniejach z fazą grupową, gdy drużyna trafi do grupy."
      />
    )
  }

  return (
    <div className="space-y-4">
      {standing.data.map((group) => (
        <section
          key={group.groupId}
          className="bg-card rounded-xl border"
          aria-label={`Tabela ${group.groupName ?? ""}`}
        >
          <div className="border-b px-4 py-3">
            <h2 className="font-semibold">{group.groupName ?? "Grupa"}</h2>
            {group.tournamentName && (
              <p className="text-muted-foreground text-xs">{group.tournamentName}</p>
            )}
          </div>
          <table className="w-full text-sm">
            <thead className="text-muted-foreground text-xs">
              <tr>
                <th className="w-10 px-3 py-2 text-left font-medium">#</th>
                <th className="px-2 py-2 text-left font-medium">Drużyna</th>
                <th className="w-10 px-2 py-2 text-center font-medium">M</th>
                <th className="hidden w-16 px-2 py-2 text-center font-medium sm:table-cell">
                  Bramki
                </th>
                <th className="w-12 px-3 py-2 text-center font-medium">Pkt</th>
              </tr>
            </thead>
            <tbody>
              {group.standings.map((row) => {
                const mine = row.tournamentTeamId === group.tournamentTeamId
                return (
                  <tr
                    key={row.tournamentTeamId}
                    className={cn("border-t", mine && "bg-primary/10 font-semibold")}
                  >
                    <td className="text-muted-foreground px-3 py-2 font-mono tabular-nums">
                      {row.position}
                    </td>
                    <td className="truncate px-2 py-2">
                      {group.teamNames?.[row.tournamentTeamId] ?? "Drużyna"}
                    </td>
                    <td className="px-2 py-2 text-center font-mono tabular-nums">
                      {row.matchesPlayed}
                    </td>
                    <td className="hidden px-2 py-2 text-center font-mono tabular-nums sm:table-cell">
                      {row.goalsFor}:{row.goalsAgainst}
                    </td>
                    <td className="px-3 py-2 text-center font-mono tabular-nums">{row.points}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </section>
      ))}
    </div>
  )
}
