"use client"

import Link from "next/link"

import { formatDateTime } from "@/lib/format"
import { TeamLogo } from "@/features/teams/components/team-logo"
import { cn } from "@/lib/utils"
import type { Match } from "@/types/matches"

import { resultNote } from "../result-note"
import type {
  Bracket,
  BracketNode,
  BracketRound,
  BracketSlotRef,
  BracketSourceType,
} from "@/types/public"

export type BracketSwap = {
  selected: BracketSlotRef | null
  disabled: boolean
  onSelect: (slot: BracketSlotRef) => void
}

export type BracketLookups = {
  teamName: (tournamentTeamId: string) => string
  /** Herb drużyny. Bez tej funkcji drabinka nie pokazuje herbów (portal bez „Pokazuj drużyny”). */
  teamLogo?: (tournamentTeamId: string) => string | null
  groupName: (groupId: string | null) => string | null
  pitchName?: (pitchId: string | null) => string | null
  /** Strona drużyny. Bez niej nazwa jest zwykłym tekstem (np. w trybie zamiany par). */
  teamHref?: (tournamentTeamId: string) => string | undefined
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
  logo,
  known,
  href,
  score,
  penalties,
  winner,
  decided,
  swap,
}: {
  label: string
  /** undefined = bez herbu (miejsce jeszcze bez drużyny albo herby wyłączone). */
  logo?: string | null
  known: boolean
  href?: string
  score: number | null
  penalties: number | null
  winner: boolean
  decided: boolean
  /** Tryb zamiany par: slot jest przyciskiem. */
  swap?: { selected: boolean; disabled: boolean; onSelect: () => void }
}) {
  const className = cn(
    "flex items-center justify-between gap-2 px-3 py-2 text-sm",
    winner && "bg-primary/10 font-semibold",
    decided && !winner && "text-muted-foreground",
  )
  const text =
    href && !swap ? (
      <Link href={href} className="min-w-0 truncate hover:underline">
        {label}
      </Link>
    ) : (
      <span className={cn("min-w-0 truncate", !known && "text-muted-foreground italic")}>
        {label}
      </span>
    )
  const name =
    logo !== undefined ? (
      <span className="flex min-w-0 items-center gap-2">
        <TeamLogo name={label} logoUrl={logo} size={18} />
        {text}
      </span>
    ) : (
      text
    )
  if (swap) {
    return (
      <button
        type="button"
        aria-pressed={swap.selected}
        disabled={swap.disabled}
        onClick={swap.onSelect}
        className={cn(
          className,
          "hover:bg-accent focus-visible:ring-ring w-full cursor-pointer text-left outline-none focus-visible:ring-2 focus-visible:ring-inset disabled:cursor-wait",
          swap.selected && "bg-primary text-primary-foreground hover:bg-primary",
        )}
      >
        {name}
      </button>
    )
  }
  return (
    <div className={className} data-winner={winner || undefined}>
      {name}
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
  showTimes,
  swap,
}: {
  node: BracketNode
  match: Match | undefined
  lookups: BracketLookups
  showTimes: boolean
  swap?: BracketSwap
}) {
  const swapOf = (slot: BracketSlotRef["slot"]) =>
    swap && {
      selected: swap.selected?.nodeId === node.id && swap.selected.slot === slot,
      disabled: swap.disabled,
      onSelect: () => swap.onSelect({ nodeId: node.id, slot }),
    }
  const played = match?.status === "Finished"
  const live = match?.status === "Live" || match?.status === "Paused"
  const showScore = played || live
  // Termin: istniejącego meczu albo zaplanowany z góry dla meczu, który czeka na drużyny.
  const scheduledAt = match ? match.scheduledAt : node.plannedAt
  const pitch = lookups.pitchName?.(match ? match.pitchId : node.plannedPitchId) ?? null
  const showTime = showTimes && !showScore && match?.status !== "Cancelled" && !!scheduledAt
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
        logo={
          node.homeTournamentTeamId && lookups.teamLogo
            ? lookups.teamLogo(node.homeTournamentTeamId)
            : undefined
        }
        known={!!node.homeTournamentTeamId}
        href={node.homeTournamentTeamId ? lookups.teamHref?.(node.homeTournamentTeamId) : undefined}
        score={showScore ? match!.homeScore : null}
        penalties={showScore ? match!.homePenaltyScore : null}
        winner={played && homeAhead(match!)}
        decided={played}
        swap={swapOf("Home")}
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
        logo={
          node.awayTournamentTeamId && lookups.teamLogo
            ? lookups.teamLogo(node.awayTournamentTeamId)
            : undefined
        }
        known={!!node.awayTournamentTeamId}
        href={node.awayTournamentTeamId ? lookups.teamHref?.(node.awayTournamentTeamId) : undefined}
        score={showScore ? match!.awayScore : null}
        penalties={showScore ? match!.awayPenaltyScore : null}
        winner={played && awayAhead(match!)}
        decided={played}
        swap={swapOf("Away")}
      />
      {/* Karne są przy wynikach drużyn - tu dogrywka i walkower, żeby nie wyglądały jak zwykły wynik. */}
      {played && match && (match.resultType === "ExtraTime" || match.resultType === "Walkover") && (
        <p
          className="text-muted-foreground border-t px-3 py-1 text-xs"
          data-testid="bracket-node-note"
        >
          {resultNote({ ...match, homePenaltyScore: null, awayPenaltyScore: null })}
        </p>
      )}
      {showTime && (
        <p
          className="text-muted-foreground border-t px-3 py-1 text-xs"
          data-testid="bracket-node-time"
        >
          {formatDateTime(scheduledAt)}
          {pitch && ` · ${pitch}`}
        </p>
      )}
    </div>
  )
}

/** Drabinka w kolumnach rund (CSS grid, przewijana poziomo na telefonie) + mecz o 3. miejsce. */
export function BracketBoard({
  bracket,
  matches,
  lookups,
  showTimes = true,
  swap,
}: {
  bracket: Bracket
  matches: Match[]
  lookups: BracketLookups
  /** false, gdy terminy w bazie to jeszcze zaślepki (puchar przed wygenerowaniem terminarza). */
  showTimes?: boolean
  /** Tryb zamiany par: sloty pierwszej rundy da się kliknąć. */
  swap?: BracketSwap
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
                    <NodeCard
                      node={node}
                      match={matchOf(node)}
                      lookups={lookups}
                      showTimes={showTimes}
                      swap={round.roundNumber === 1 ? swap : undefined}
                    />
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
            <NodeCard
              key={node.id}
              node={node}
              match={matchOf(node)}
              lookups={lookups}
              showTimes={showTimes}
            />
          ))}
        </section>
      ))}
    </div>
  )
}
