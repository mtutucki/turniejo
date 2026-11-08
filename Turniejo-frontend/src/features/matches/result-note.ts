/** Pola meczu, z których wynika sposób rozstrzygnięcia. */
type DecidedMatch = {
  status: string
  resultType?: string | null
  homePenaltyScore: number | null
  awayPenaltyScore: number | null
}

/**
 * Krótki dopisek do wyniku zakończonego meczu: „karne 4:3”, „po dogrywce”, „walkower”.
 * Bez niego walkower 3:0 czy wynik po dogrywce wyglądały jak zwykły wynik z boiska.
 */
export function resultNote(match: DecidedMatch): string | null {
  if (match.status !== "Finished") return null
  if (match.homePenaltyScore != null && match.awayPenaltyScore != null)
    return `karne ${match.homePenaltyScore}:${match.awayPenaltyScore}`
  if (match.resultType === "ExtraTime") return "po dogrywce"
  if (match.resultType === "Walkover") return "walkower"
  return null
}
