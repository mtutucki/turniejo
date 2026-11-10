import type { PlannedBracketMatch } from "@/features/matches/planned-bracket-matches"
import type { Match } from "@/types/matches"

// Bilans, forma i droga w pucharze jednej drużyny, liczone z meczów turnieju. Wspólne dla strony
// drużyny w portalu i w panelu. Czyste funkcje, bez pobierania danych.

/** Wynik meczu z perspektywy drużyny: zwycięstwo, remis, porażka. */
export type Outcome = "W" | "D" | "L"

type TeamMatch = Pick<
  Match,
  | "id"
  | "status"
  | "scheduledAt"
  | "stageType"
  | "homeTournamentTeamId"
  | "awayTournamentTeamId"
  | "homeScore"
  | "awayScore"
  | "homePenaltyScore"
  | "awayPenaltyScore"
>

export const plays = (m: TeamMatch, teamId: string) =>
  m.homeTournamentTeamId === teamId || m.awayTournamentTeamId === teamId

const isLive = (m: TeamMatch) => m.status === "Live" || m.status === "Paused"
const UPCOMING = new Set<Match["status"]>(["Scheduled", "Ready", "Postponed"])

/** Tylko zakończony mecz ma wynik. Remis rozstrzygnięty karnymi to zwycięstwo albo porażka. */
export function outcomeFor(m: TeamMatch, teamId: string): Outcome | null {
  if (m.status !== "Finished" || !plays(m, teamId)) return null
  const home = m.homeTournamentTeamId === teamId
  const [own, other] = home ? [m.homeScore, m.awayScore] : [m.awayScore, m.homeScore]
  if (own !== other) return own > other ? "W" : "L"
  if (m.homePenaltyScore != null && m.awayPenaltyScore != null) {
    const [ownPen, otherPen] = home
      ? [m.homePenaltyScore, m.awayPenaltyScore]
      : [m.awayPenaltyScore, m.homePenaltyScore]
    if (ownPen !== otherPen) return ownPen > otherPen ? "W" : "L"
  }
  return "D"
}

export type TeamRecord<M extends TeamMatch> = {
  /** Mecze drużyny w kolejności rozgrywania. */
  matches: M[]
  played: number
  wins: number
  draws: number
  losses: number
  goalsFor: number
  goalsAgainst: number
  /** Ostatnie wyniki, najstarszy pierwszy (do 5). */
  form: Outcome[]
  live: M[]
  next: M | undefined
}

export function teamRecord<M extends TeamMatch>(all: M[], teamId: string): TeamRecord<M> {
  const matches = all
    .filter((m) => plays(m, teamId))
    .sort((a, b) => a.scheduledAt.localeCompare(b.scheduledAt))
  const record: TeamRecord<M> = {
    matches,
    played: 0,
    wins: 0,
    draws: 0,
    losses: 0,
    goalsFor: 0,
    goalsAgainst: 0,
    form: [],
    live: matches.filter(isLive),
    next: matches.find((m) => UPCOMING.has(m.status)),
  }
  const outcomes: Outcome[] = []
  for (const m of matches) {
    const outcome = outcomeFor(m, teamId)
    if (!outcome) continue
    const home = m.homeTournamentTeamId === teamId
    record.played++
    record.goalsFor += home ? m.homeScore : m.awayScore
    record.goalsAgainst += home ? m.awayScore : m.homeScore
    if (outcome === "W") record.wins++
    else if (outcome === "D") record.draws++
    else record.losses++
    outcomes.push(outcome)
  }
  record.form = outcomes.slice(-5)
  return record
}

export type CupStep =
  | { kind: "match"; stage: string; match: TeamMatch; outcome: Outcome | null }
  | { kind: "planned"; stage: string; planned: PlannedBracketMatch }

export type CupPath = {
  steps: CupStep[]
  /** „Zwycięzca turnieju”, „2. miejsce”, „Odpadła w półfinale”, „W grze”… */
  status: string
  champion: boolean
}

const ELIMINATED_IN: Record<string, string> = {
  Finał: "2. miejsce",
  "Mecz o 3. miejsce": "4. miejsce",
  Półfinał: "Odpadła w półfinale",
  Ćwierćfinał: "Odpadła w ćwierćfinale",
}

/**
 * Droga drużyny w pucharze jej kategorii: rozegrane i zaplanowane mecze drabinki po kolei i krótki
 * status. `stages` to nazwy rund meczów (bracketMatchStages), `planned` to mecze kolejnych rund,
 * które jeszcze nie istnieją (plannedBracketMatches), z drużyną już znaną.
 */
export function cupPath(
  matches: TeamMatch[],
  teamId: string,
  stages: Map<string, string>,
  planned: PlannedBracketMatch[],
): CupPath | null {
  const played: CupStep[] = matches
    .filter((m) => plays(m, teamId) && stages.has(m.id) && m.status !== "Cancelled")
    .sort((a, b) => a.scheduledAt.localeCompare(b.scheduledAt))
    .map((m) => ({
      kind: "match",
      stage: stages.get(m.id)!,
      match: m,
      outcome: outcomeFor(m, teamId),
    }))
  const ahead: CupStep[] = planned
    .filter((p) => p.home.tournamentTeamId === teamId || p.away.tournamentTeamId === teamId)
    .map((p) => ({ kind: "planned", stage: p.stage, planned: p }))
  const steps = [...played, ...ahead]
  if (steps.length === 0) return null

  const last = played.at(-1)
  const decided = last?.kind === "match" ? last : undefined
  let status = "W grze"
  let champion = false
  if (decided && ahead.length === 0 && decided.outcome) {
    if (decided.outcome === "W" && decided.stage === "Finał") {
      status = "Zwycięzca turnieju"
      champion = true
    } else if (decided.outcome === "W" && decided.stage === "Mecz o 3. miejsce")
      status = "3. miejsce"
    else if (decided.outcome === "L")
      status = ELIMINATED_IN[decided.stage] ?? `Odpadła: ${decided.stage.toLowerCase()}`
  } else if (ahead.some((s) => s.stage === "Mecz o 3. miejsce")) status = "Zagra o 3. miejsce"
  else if (ahead.some((s) => s.stage === "Finał")) status = "W finale"
  return { steps, status, champion }
}

export const OUTCOME_LABELS: Record<Outcome, string> = {
  W: "Zwycięstwo",
  D: "Remis",
  L: "Porażka",
}
/** Litera w pasku formy: Z, R, P. */
export const OUTCOME_LETTERS: Record<Outcome, string> = { W: "Z", D: "R", L: "P" }
