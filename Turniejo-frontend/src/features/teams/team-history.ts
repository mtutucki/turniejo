import type { TeamHistoryTournament, TeamRecordSummary } from "@/types/teams"

/** Nazwa rundy, w której drużyna odpadła: 2 → „półfinale”, 3 → „ćwierćfinale”, 4 → „1/8 finału”. */
function roundName(fromEnd: number) {
  if (fromEnd === 2) return "półfinale"
  if (fromEnd === 3) return "ćwierćfinale"
  return `1/${2 ** (fromEnd - 1)} finału`
}

/** Wynik drużyny w turnieju: „Zwycięzca”, „2. miejsce”, „Odpadła w półfinale”, „1. miejsce w Grupie A”… */
export function tournamentResult(
  t: Pick<
    TeamHistoryTournament,
    "resultKind" | "eliminatedRoundFromEnd" | "groupPosition" | "groupName"
  >,
) {
  switch (t.resultKind) {
    case "Champion":
      return "Zwycięzca"
    case "RunnerUp":
      return "2. miejsce"
    case "Third":
      return "3. miejsce"
    case "Fourth":
      return "4. miejsce"
    case "Eliminated":
      return t.eliminatedRoundFromEnd
        ? `Odpadła w ${roundName(t.eliminatedRoundFromEnd)}`
        : "Odpadła"
    case "InCup":
      return "Gra w pucharze"
    case "Group":
      if (!t.groupName) return `${t.groupPosition}. miejsce w grupie`
      return /^Grupa\s/.test(t.groupName)
        ? `${t.groupPosition}. miejsce w ${t.groupName.replace(/^Grupa/, "Grupie")}`
        : `${t.groupPosition}. miejsce w grupie „${t.groupName}”`
    default:
      return "Bez wyniku"
  }
}

/** Procent zwycięstw z rozegranych meczów, zaokrąglony; null bez meczów. */
export function winRate(r: TeamRecordSummary) {
  return r.played ? Math.round((r.wins / r.played) * 100) : null
}
