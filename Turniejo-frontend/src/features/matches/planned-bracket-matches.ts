import type { Bracket, BracketNode, BracketRound } from "@/types/public"

/**
 * Mecz kolejnej rundy drabinki, który jeszcze nie istnieje (czeka na zwycięzców wcześniejszych
 * meczów), ale ma już termin zaplanowany przez generator terminarza. Do pokazania w terminarzu
 * jako zapowiedź — z opisem, skąd przyjdą drużyny.
 */
export type PlannedBracketMatch = {
  nodeId: string
  roundId: string
  categoryId: string
  scheduledAt: string
  pitchId: string | null
  /** "Finał", "Półfinał", "Mecz o 3. miejsce"… */
  stage: string
  home: PlannedSide
  away: PlannedSide
}

/** known = konkretna drużyna (już awansowała); inaczej opis typu "Zwycięzca meczu A – B". */
export type PlannedSide = { label: string; known: boolean }

const STAGES: Record<number, [name: string, genitive: string]> = {
  1: ["Finał", "finału"],
  2: ["Półfinał", "półfinału"],
  3: ["Ćwierćfinał", "ćwierćfinału"],
}

function stageOf(round: BracketRound, standardRounds: number): [name: string, genitive: string] {
  if (round.roundType === "ThirdPlace") return ["Mecz o 3. miejsce", "meczu o 3. miejsce"]
  const fromEnd = standardRounds - round.roundNumber + 1
  const fraction = `1/${2 ** (fromEnd - 1)} finału`
  return STAGES[fromEnd] ?? [fraction, fraction]
}

/** Runda drabinki jako sekcja terminarza: "Ćwierćfinały", "Półfinały", "Finał"… */
export type BracketStage = {
  roundId: string
  /** Kolejność rozgrywania — mecz o 3. miejsce tuż przed finałem. */
  order: number
  title: string
  matchIds: string[]
}

const STAGE_TITLES: Record<string, string> = {
  Półfinał: "Półfinały",
  Ćwierćfinał: "Ćwierćfinały",
}

export function bracketStages(bracket: Bracket): BracketStage[] {
  const standardRounds = bracket.rounds.filter((r) => r.roundType === "Standard").length
  return bracket.rounds
    .map((round) => {
      const name = stageOf(round, standardRounds)[0]
      return {
        roundId: round.id,
        order: round.roundType === "ThirdPlace" ? standardRounds - 0.5 : round.roundNumber,
        title: STAGE_TITLES[name] ?? name,
        matchIds: round.nodes.flatMap((node) => (node.matchId ? [node.matchId] : [])),
      }
    })
    .sort((a, b) => a.order - b.order)
}

export function plannedBracketMatches(
  bracket: Bracket,
  categoryId: string,
  teamName: (tournamentTeamId: string) => string,
): PlannedBracketMatch[] {
  const standardRounds = bracket.rounds.filter((r) => r.roundType === "Standard").length
  const nodes = bracket.rounds.flatMap((round) => round.nodes.map((node) => ({ node, round })))

  // Węzeł nie zna swoich źródeł wprost — szukamy węzłów, których zwycięzca/przegrany do niego trafia.
  function source(target: BracketNode, slot: "Home" | "Away", loser: boolean) {
    if (!loser) {
      return nodes.find(
        ({ node }) => node.winnerGoesToNodeId === target.id && node.winnerGoesToSlot === slot,
      )
    }
    const losers = nodes
      .filter(({ node }) => node.loserGoesToNodeId === target.id)
      .sort((a, b) => a.node.id.localeCompare(b.node.id))
    return losers[slot === "Home" ? 0 : 1]
  }

  function side(target: BracketNode, slot: "Home" | "Away"): PlannedSide {
    const teamId = slot === "Home" ? target.homeTournamentTeamId : target.awayTournamentTeamId
    if (teamId) return { label: teamName(teamId), known: true }

    const sourceType = slot === "Home" ? target.homeSourceType : target.awaySourceType
    if (sourceType !== "MatchWinner" && sourceType !== "MatchLoser") {
      return { label: "Do ustalenia", known: false }
    }
    const loser = sourceType === "MatchLoser"
    const prefix = loser ? "Przegrany" : "Zwycięzca"
    const from = source(target, slot, loser)
    if (!from) return { label: `${prefix} wcześniejszego meczu`, known: false }

    const { homeTournamentTeamId, awayTournamentTeamId } = from.node
    if (homeTournamentTeamId && awayTournamentTeamId) {
      return {
        label: `${prefix} meczu ${teamName(homeTournamentTeamId)} – ${teamName(awayTournamentTeamId)}`,
        known: false,
      }
    }
    return { label: `${prefix} ${stageOf(from.round, standardRounds)[1]}`, known: false }
  }

  return nodes
    .filter(({ node }) => !node.matchId && !!node.plannedAt)
    .map(({ node, round }) => ({
      nodeId: node.id,
      roundId: round.id,
      categoryId,
      scheduledAt: node.plannedAt!,
      pitchId: node.plannedPitchId,
      stage: stageOf(round, standardRounds)[0],
      home: side(node, "Home"),
      away: side(node, "Away"),
    }))
    .sort((a, b) => a.scheduledAt.localeCompare(b.scheduledAt))
}
