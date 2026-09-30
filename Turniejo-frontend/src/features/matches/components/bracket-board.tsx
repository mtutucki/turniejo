"use client"

import { cn } from "@/lib/utils"
import type { Match } from "@/types/matches"
import type { Bracket, BracketNode, BracketRound, BracketSourceType } from "@/types/public"

export type BracketLookups = {
  teamName: (tournamentTeamId: string) => string
  groupName: (groupId: string | null) => string | null
}

/** Nazwy rund od końca: finał, półfinały, ćwierćfinały, 1/8 finału… (backend zwraca "Round N"). */
function roundLabel(round: BracketRound, standardRounds: number) {
  if (round.roundType === "ThirdPlace") return "Mecz o 3. miejsce"
  const fromEnd = standardRounds - round.roundNumber + 1
  if (fromEnd === 1) return "Finał"
  if (fromEnd === 2) return "Półfinały"
  if (fromEnd === 3) return "Ćwierćfinały"
  return `1/${2 ** (fromEnd - 1)} finału`
}

/**
 * Węzły nie mają pozycji, a API zwraca je w dowolnej kolejności. Układamy od finału wstecz: w każdej
 * rundzie węzły idą w kolejności meczów, do których przechodzą ich zwycięzcy (slot gospodarza przed
 * gościem) — pary zasilające ten sam mecz stoją obok siebie, a układ nie skacze między odświeżeniami.
 */
function orderRounds(rounds: BracketRound[]) {
  const sorted = [...rounds].sort((a, b) => a.roundNumber - b.roundNumber)
  const ordered: BracketNode[][] = []
  for (let r = sorted.length - 1; r >= 0; r--) {
    const next = ordered[r + 1]
    const position = new Map((next ?? []).map((n, i) => [n.id, i]))
    const at = (n: BracketNode) =>
      position.get(n.winnerGoesToNodeId ?? "") ?? Number.MAX_SAFE_INTEGER
    const slot = (n: BracketNode) => (n.winnerGoesToSlot === "Away" ? 1 : 0)
    ordered[r] = [...sorted[r]!.nodes].sort(
      (a, b) => at(a) - at(b) || slot(a) - slot(b) || a.id.localeCompare(b.id),
    )
  }
  return sorted.map((round, i) => ({ round, nodes: ordered[i]! }))
}

function placeholder(
  source: BracketSourceType,
  groupId: string | null,
  position: number | null,
  lookups: BracketLookups,
) {
  if (source === "GroupPosition" && position) {
    const group = lookups.groupName(groupId)
    return group ? `${position}. miejsce · ${group}` : `${position}. miejsce w grupie`
  }
  if (source === "MatchWinner") return "Zwycięzca meczu"
  if (source === "MatchLoser") return "Przegrany meczu"
  if (source === "Bye") return "Wolny los"
  return "Do ustalenia"
}

function Slot({
  label,
  known,
  score,
  penalties,
  winner,
  decided,
}: {
  label: string
  known: boolean
  score: number | null
  penalties: number | null
  winner: boolean
  decided: boolean
}) {
  return (
    <div
      className={cn(
        "flex items-center justify-between gap-2 px-3 py-2 text-sm",
        winner && "bg-primary/10 font-semibold",
        decided && !winner && "text-muted-foreground",
      )}
      data-winner={winner || undefined}
    >
      <span className={cn("min-w-0 truncate", !known && "text-muted-foreground italic")}>
        {label}
      </span>
      {score != null && (
        <span className="shrink-0 font-mono tabular-nums">
          {score}
          {penalties != null && (
            <span className="text-muted-foreground ml-1 text-xs">({penalties})</span>
          )}
        </span>
      )}
    </div>
  )
}

function NodeCard({
  node,
  match,
  lookups,
}: {
  node: BracketNode
  match: Match | undefined
  lookups: BracketLookups
}) {
  const played = match?.status === "Finished"
  const live = match?.status === "Live" || match?.status === "Paused"
  const showScore = played || live
  // Zwycięzca: przy remisie rozstrzygają karne (albo walkower, gdy wynik ustawił organizator).
  const homeAhead = (m: Match) =>
    m.homeScore > m.awayScore ||
    (m.homeScore === m.awayScore && (m.homePenaltyScore ?? 0) > (m.awayPenaltyScore ?? 0))
  const awayAhead = (m: Match) =>
    m.awayScore > m.homeScore ||
    (m.homeScore === m.awayScore && (m.awayPenaltyScore ?? 0) > (m.homePenaltyScore ?? 0))

  return (
    <div
      className={cn("bg-card overflow-hidden rounded-lg border", live && "border-live")}
      data-testid="bracket-node"
    >
      <Slot
        label={
          node.homeTournamentTeamId
            ? lookups.teamName(node.homeTournamentTeamId)
            : placeholder(
                node.homeSourceType,
                node.homeSourceGroupId,
                node.homeSourcePosition,
                lookups,
              )
        }
        known={!!node.homeTournamentTeamId}
        score={showScore ? match!.homeScore : null}
        penalties={showScore ? match!.homePenaltyScore : null}
        winner={played && homeAhead(match!)}
        decided={played}
      />
      <div className="border-t" />
      <Slot
        label={
          node.awayTournamentTeamId
            ? lookups.teamName(node.awayTournamentTeamId)
            : placeholder(
                node.awaySourceType,
                node.awaySourceGroupId,
                node.awaySourcePosition,
                lookups,
              )
        }
        known={!!node.awayTournamentTeamId}
        score={showScore ? match!.awayScore : null}
        penalties={showScore ? match!.awayPenaltyScore : null}
        winner={played && awayAhead(match!)}
        decided={played}
      />
    </div>
  )
}

/** Drabinka w kolumnach rund (CSS grid, przewijana poziomo na telefonie) + mecz o 3. miejsce. */
export function BracketBoard({
  bracket,
  matches,
  lookups,
}: {
  bracket: Bracket
  matches: Match[]
  lookups: BracketLookups
}) {
  const matchById = new Map(matches.map((m) => [m.id, m]))
  const standard = orderRounds(bracket.rounds.filter((r) => r.roundType === "Standard"))
  const thirdPlace = bracket.rounds.filter((r) => r.roundType === "ThirdPlace")
  const matchOf = (node: BracketNode) => (node.matchId ? matchById.get(node.matchId) : undefined)

  return (
    <div className="space-y-6">
      <div className="-mx-4 overflow-x-auto px-4 pb-2">
        <div
          className="grid min-w-max auto-cols-[minmax(10.5rem,1fr)] grid-flow-col gap-4"
          aria-label="Drabinka"
        >
          {standard.map(({ round, nodes }) => (
            <section key={round.id} className="flex flex-col">
              <h3 className="text-muted-foreground mb-3 text-xs font-semibold tracking-wide uppercase">
                {roundLabel(round, standard.length)}
              </h3>
              {/* justify-around: węzły kolejnej rundy wypadają między parami poprzedniej. */}
              <ol className="flex flex-1 flex-col justify-around gap-4">
                {nodes.map((node) => (
                  <li key={node.id}>
                    <NodeCard node={node} match={matchOf(node)} lookups={lookups} />
                  </li>
                ))}
              </ol>
            </section>
          ))}
        </div>
      </div>
      {thirdPlace.map((round) => (
        <section key={round.id} className="max-w-xs">
          <h3 className="text-muted-foreground mb-3 text-xs font-semibold tracking-wide uppercase">
            {roundLabel(round, standard.length)}
          </h3>
          {round.nodes.map((node) => (
            <NodeCard key={node.id} node={node} match={matchOf(node)} lookups={lookups} />
          ))}
        </section>
      ))}
    </div>
  )
}
