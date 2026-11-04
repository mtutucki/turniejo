import type { TournamentTeam } from "@/types/teams"
import type { TournamentGroup } from "@/types/tournaments"

import type { BracketSlotInput } from "./api"

export type BracketPair = { home: BracketSlotInput | null; away: BracketSlotInput | null }

/** Backend przyjmuje tylko 1, 2, 4 albo 8 par w 1. rundzie (potęga dwójki). */
const MAX_PAIRS = 8
const nextPowerOfTwo = (value: number) => 2 ** Math.ceil(Math.log2(Math.max(1, value)))

/**
 * Domyślna wielkość drabinki. Z grupami: para na każdą grupę (3 grupy → 4 pary). Bez grup: najmniejsza
 * drabinka, w której mieszczą się wszystkie drużyny (np. 5 drużyn → ćwierćfinały z trzema wolnymi losami).
 */
export function defaultPairCount(groups: TournamentGroup[], teams: TournamentTeam[]) {
  const wanted = groups.length > 0 ? groups.length : Math.max(2, teams.length) / 2
  return Math.min(MAX_PAIRS, nextPowerOfTwo(wanted))
}

/**
 * Propozycja par "na krzyż": przy 2 grupach A1–B2, B1–A2; przy jednej grupie 1–4, 2–3; w ogólności
 * zwycięzcy grup z drugimi miejscami sąsiednich grup. Organizator może każdą parę zmienić.
 */
export function suggestPairs(
  groups: TournamentGroup[],
  teams: TournamentTeam[],
  pairCount: number,
): BracketPair[] {
  // Puchar bez grup: drużyny w kolejności rozstawienia (numer rozstawienia, potem nazwa) —
  // 1. z ostatnią, 2. z przedostatnią. Gdy drużyn jest mniej niż miejsc (np. 5 w ćwierćfinałach),
  // najwyżej rozstawione dostają wolny los. Para bez żadnej drużyny zostaje do wybrania.
  if (groups.length === 0) {
    const seeded = [...teams].sort(
      (a, b) =>
        (a.seedNumber ?? 999) - (b.seedNumber ?? 999) || a.teamName.localeCompare(b.teamName, "pl"),
    )
    const slot = (i: number): BracketSlotInput | null => {
      const team = seeded[i]
      return team ? { kind: "team", tournamentTeamId: team.id } : null
    }
    return Array.from({ length: pairCount }, (_, i) => {
      const home = slot(i)
      return { home, away: slot(pairCount * 2 - 1 - i) ?? (home ? { kind: "bye" } : null) }
    })
  }
  if (groups.length === 1) {
    const group = groups[0]!
    const slot = (position: number): BracketSlotInput => ({
      kind: "group",
      groupId: group.id,
      position,
    })
    return Array.from({ length: pairCount }, (_, i) => ({
      home: slot(i + 1),
      away: slot(pairCount * 2 - i),
    }))
  }
  const pairs: BracketPair[] = []
  for (let i = 0; i < pairCount; i++) {
    const g = groups[i % groups.length]
    const opponent = groups[(i + 1) % groups.length] ?? g
    const round = Math.floor(i / groups.length)
    pairs.push({
      home: g ? { kind: "group", groupId: g.id, position: 1 + round * 2 } : null,
      away: opponent ? { kind: "group", groupId: opponent.id, position: 2 + round * 2 } : null,
    })
  }
  return pairs
}

/**
 * Propozycja, którą można zapisać bez poprawek: każda para pełna, bez podwójnego wolnego losu,
 * miejsca w grupach istnieją, a w pucharze grają wszystkie drużyny. Inaczej null — wtedy pary
 * musi ułożyć organizator.
 */
export function autoBracketPairs(
  groups: TournamentGroup[],
  teams: TournamentTeam[],
): { home: BracketSlotInput; away: BracketSlotInput }[] | null {
  if (groups.length === 0 && teams.length < 2) return null
  const pairs = suggestPairs(groups, teams, defaultPairCount(groups, teams))
  const teamsInGroup = (groupId: string) => teams.filter((t) => t.groupId === groupId).length
  const slotOk = (slot: BracketSlotInput | null) =>
    !!slot && (slot.kind !== "group" || slot.position <= teamsInGroup(slot.groupId))
  if (!pairs.every((p) => slotOk(p.home) && slotOk(p.away))) return null
  if (pairs.some((p) => p.home?.kind === "bye" && p.away?.kind === "bye")) return null
  if (groups.length === 0) {
    const placed = pairs.flatMap((p) => [p.home, p.away]).filter((s) => s?.kind === "team")
    if (placed.length < teams.length) return null
  }
  return pairs.map((p) => ({ home: p.home!, away: p.away! }))
}
