"use client"

import { Trophy } from "lucide-react"

import type { Match } from "@/types/matches"
import type { Bracket } from "@/types/public"

import { usePublicBracket, usePublicSchedule, usePublicStandings } from "../api"
import { usePublicContext } from "../public-context"

/** Zwycięzca rozegranego meczu — przy remisie rozstrzygają karne (walkower ma już ustawiony wynik). */
export function matchWinner(match: Match | undefined) {
  if (match?.status !== "Finished") return null
  const home = match.homeScore * 1000 + (match.homePenaltyScore ?? 0)
  const away = match.awayScore * 1000 + (match.awayPenaltyScore ?? 0)
  if (home === away) return null
  return home > away ? match.homeTournamentTeamId : match.awayTournamentTeamId
}

/**
 * Kiedy szukać mistrza. Puchar: zaraz po finale, także gdy turniej formalnie trwa - baner i tak czeka,
 * aż finał będzie rozegrany (QA v3: PUC-045, PUB-011). Same grupy: lider jest pewny dopiero po zakończeniu.
 */
export function showsChampion(status: string | undefined, format: string | null | undefined) {
  if (!format) return false
  if (status === "Finished") return true
  return status === "InProgress" && format !== "Group"
}

/** Finał = węzeł ostatniej zwykłej rundy (nie meczu o 3. miejsce), z którego nikt dalej nie awansuje. */
export function bracketChampion(bracket: Bracket, matches: Match[]) {
  const finalRound = bracket.rounds
    .filter((r) => r.roundType === "Standard")
    .sort((a, b) => b.roundNumber - a.roundNumber)[0]
  const finalNode = finalRound?.nodes.find((n) => !n.winnerGoesToNodeId)
  if (!finalNode?.matchId) return null
  return matchWinner(matches.find((m) => m.id === finalNode.matchId))
}

function ChampionRow({ label, teamId }: { label: string | null; teamId: string | null }) {
  const { lookups } = usePublicContext()
  // Bez ShowTeams backend nie zdradza nazw drużyn — wtedy nie ma czego ogłaszać.
  const name = lookups.teamName(teamId, "")
  if (!teamId || !name) return null

  return (
    <li className="flex items-center gap-3">
      <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-yellow-400/20">
        <Trophy className="size-5 text-yellow-500" aria-hidden />
      </span>
      <span className="min-w-0">
        <span className="text-muted-foreground block text-xs font-medium tracking-wide uppercase">
          {label ? `Zwycięzca · ${label}` : "Zwycięzca turnieju"}
        </span>
        <span className="block truncate text-lg font-bold">{name}</span>
      </span>
    </li>
  )
}

function KnockoutChampion({ categoryId, label }: { categoryId: string; label: string | null }) {
  const { slug, tournament } = usePublicContext()
  const bracket = usePublicBracket(slug, categoryId)
  const schedule = usePublicSchedule(slug, tournament.settings.showSchedule)
  if (!bracket.data || !schedule.data) return null
  return <ChampionRow label={label} teamId={bracketChampion(bracket.data, schedule.data)} />
}

function GroupLeader({ groupId, label }: { groupId: string; label: string | null }) {
  const { slug } = usePublicContext()
  const standings = usePublicStandings(slug, groupId)
  const leader = standings.data?.find((row) => row.position === 1)
  return <ChampionRow label={label} teamId={leader?.tournamentTeamId ?? null} />
}

/**
 * Zwycięzca finału drabinki (formaty z pucharem, zaraz po finale) albo lider tabeli
 * (sam format grupowy) — osobno dla każdej kategorii, a przy kilku grupach dla każdej grupy.
 */
export function ChampionBanner() {
  const { tournament } = usePublicContext()
  if (!showsChampion(tournament.status, tournament.format)) return null

  const knockout = tournament.format !== "Group"
  const multipleCategories = tournament.categories.length > 1
  const categories = [...tournament.categories].sort((a, b) => a.order - b.order)

  const rows = categories.flatMap((category) => {
    const categoryLabel = multipleCategories ? category.name : null
    if (knockout) {
      return [<KnockoutChampion key={category.id} categoryId={category.id} label={categoryLabel} />]
    }
    const groups = tournament.groups
      .filter((g) => g.categoryId === category.id)
      .sort((a, b) => a.order - b.order)
    return groups.map((group) => (
      <GroupLeader
        key={group.id}
        groupId={group.id}
        label={
          groups.length > 1
            ? [categoryLabel, group.name].filter(Boolean).join(" · ")
            : categoryLabel
        }
      />
    ))
  })

  return (
    <section
      aria-label="Zwycięzcy"
      className="mb-6 rounded-xl border border-yellow-400/40 bg-yellow-400/10 p-4 [&:not(:has(li))]:hidden"
    >
      <ul className="space-y-3">{rows}</ul>
    </section>
  )
}
