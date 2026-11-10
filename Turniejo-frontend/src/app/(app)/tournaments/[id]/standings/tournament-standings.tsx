"use client"

import { ListOrdered } from "lucide-react"
import Link from "next/link"

import { EmptyState } from "@/components/layout/empty-state"
import { Skeleton } from "@/components/ui/skeleton"
import { useStandings } from "@/features/matches/api"
import { useTournamentMatchesRealtime } from "@/features/matches/use-tournament-matches-realtime"
import { useTournamentTeams } from "@/features/teams/api"
import { useCategories, useGroups } from "@/features/tournaments/api"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"

function GroupTable({
  groupId,
  title,
  teamName,
}: {
  groupId: string
  title: string
  teamName: (tournamentTeamId: string) => string
}) {
  const { id } = useTournamentContext()
  const standings = useStandings(id, groupId)
  const rows = [...(standings.data ?? [])].sort((a, b) => a.position - b.position)

  return (
    <section className="bg-card rounded-xl border" aria-label={`Tabela: ${title}`}>
      <h3 className="border-b px-4 py-3 font-semibold">{title}</h3>
      {standings.isPending ? (
        <Skeleton className="m-4 h-32" />
      ) : rows.length === 0 ? (
        <p className="text-muted-foreground px-4 py-6 text-sm">Brak drużyn w grupie.</p>
      ) : (
        <table className="w-full text-sm">
          <thead className="text-muted-foreground text-xs">
            <tr>
              <th className="w-10 px-3 py-2 text-left font-medium">#</th>
              <th className="px-2 py-2 text-left font-medium">Drużyna</th>
              <th className="w-10 px-2 py-2 text-center font-medium">M</th>
              <th className="hidden w-10 px-2 py-2 text-center font-medium sm:table-cell">W</th>
              <th className="hidden w-10 px-2 py-2 text-center font-medium sm:table-cell">R</th>
              <th className="hidden w-10 px-2 py-2 text-center font-medium sm:table-cell">P</th>
              <th className="hidden w-16 px-2 py-2 text-center font-medium sm:table-cell">
                Bramki
              </th>
              <th className="hidden w-12 px-2 py-2 text-center font-medium sm:table-cell">+/−</th>
              <th className="w-12 px-3 py-2 text-center font-medium">Pkt</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.tournamentTeamId} className="border-t">
                <td className="text-muted-foreground px-3 py-2 font-mono tabular-nums">
                  {row.position}
                </td>
                <td className="truncate px-2 py-2 font-medium">
                  <Link
                    href={`/tournaments/${id}/teams/${row.tournamentTeamId}`}
                    className="hover:underline"
                  >
                    {teamName(row.tournamentTeamId)}
                  </Link>
                </td>
                <td className="px-2 py-2 text-center font-mono tabular-nums">
                  {row.matchesPlayed}
                </td>
                <td className="hidden px-2 py-2 text-center font-mono tabular-nums sm:table-cell">
                  {row.wins}
                </td>
                <td className="hidden px-2 py-2 text-center font-mono tabular-nums sm:table-cell">
                  {row.draws}
                </td>
                <td className="hidden px-2 py-2 text-center font-mono tabular-nums sm:table-cell">
                  {row.losses}
                </td>
                <td className="hidden px-2 py-2 text-center font-mono tabular-nums sm:table-cell">
                  {row.goalsFor}:{row.goalsAgainst}
                </td>
                <td className="hidden px-2 py-2 text-center font-mono tabular-nums sm:table-cell">
                  {row.goalDifference > 0 ? `+${row.goalDifference}` : row.goalDifference}
                </td>
                <td className="px-3 py-2 text-center font-mono font-semibold tabular-nums">
                  {row.points}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}

export function TournamentStandingsView() {
  const { id, tournament } = useTournamentContext()
  const categories = useCategories(id)
  const groups = useGroups(id)
  const teams = useTournamentTeams(id)
  // Wynik zakończonego meczu pojawia się w tabeli bez odświeżania strony.
  useTournamentMatchesRealtime([id])

  if (tournament.data?.format === "Knockout") {
    return (
      <EmptyState
        icon={ListOrdered}
        title="Ten turniej nie ma fazy grupowej"
        description="W formacie „Pucharowy” wyniki znajdziesz w drabince."
      />
    )
  }
  if (categories.isPending || groups.isPending || teams.isPending) {
    return <Skeleton className="h-64 w-full rounded-xl" />
  }

  const categoryList = categories.data ?? []
  const groupList = groups.data ?? []
  if (groupList.length === 0) {
    return (
      <EmptyState
        icon={ListOrdered}
        title="Tabele pojawią się po utworzeniu grup"
        description="Grupy dodasz w sekcji „Kategorie i grupy”."
      />
    )
  }

  const teamById = new Map((teams.data ?? []).map((t) => [t.id, t.teamName]))
  const teamName = (tournamentTeamId: string) => teamById.get(tournamentTeamId) ?? "Drużyna"

  return (
    <div className="space-y-8">
      {categoryList.map((category) => {
        const categoryGroups = groupList.filter((g) => g.categoryId === category.id)
        if (categoryGroups.length === 0) return null
        return (
          <section key={category.id} className="space-y-3">
            {categoryList.length > 1 && (
              <h2 className="text-muted-foreground text-xs font-semibold tracking-wide uppercase">
                {category.name}
              </h2>
            )}
            <div className="grid gap-4 lg:grid-cols-2">
              {categoryGroups.map((group) => (
                <GroupTable
                  key={group.id}
                  groupId={group.id}
                  title={group.name}
                  teamName={teamName}
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
