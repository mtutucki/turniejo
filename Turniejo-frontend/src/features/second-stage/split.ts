// Format „Dwie fazy grupowe”: podział miejsc z tabel fazy 1 na grupy fazy 2. Czyste funkcje,
// bez React, żeby dało się je sprawdzić testami (split.test.ts).

import type {
  SecondStageRange,
  SecondStageRangeInput,
  SecondStageSeat,
  SecondStageSeatInput,
  TournamentGroup,
} from "@/types/tournaments"

/** Grupa fazy 1 z liczbą drużyn, które mogą w niej zająć miejsce. */
export type FirstStageGroup = { id: string; name: string; order: number; teams: number }

/** „2. miejsce Grupa B”. */
export function placeLabel(position: number, groupName: string | undefined) {
  return `${position}. miejsce ${groupName ?? "w grupie"}`
}

const ROMAN = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X"]

/** „Grupa I”, „Grupa II”… - pierwsza nazwa, której jeszcze nie ma w kategorii (faza 1 ma litery). */
export function suggestSecondStageGroupName(groups: Pick<TournamentGroup, "name">[]) {
  const taken = new Set(groups.map((g) => g.name))
  return ROMAN.map((n) => `Grupa ${n}`).find((name) => !taken.has(name)) ?? ""
}

/**
 * Gotowy podział: miejsca dzielimy na kolejne przedziały, po równo między grupy fazy 2, np. przy
 * 4 drużynach i 2 grupach: miejsca 1-2 do pierwszej, 3-4 do drugiej. W grupie najpierw wszystkie
 * 1. miejsca (A, B, C…), potem 2. miejsca. Mniejsza grupa fazy 1 nie daje miejsc, których nie ma.
 */
export function proposeSplit(
  firstStage: FirstStageGroup[],
  secondStageGroupIds: string[],
): SecondStageSeatInput[] {
  const positions = Math.max(0, ...firstStage.map((g) => g.teams))
  if (positions === 0 || secondStageGroupIds.length === 0) return []

  const sources = [...firstStage].sort((a, b) => a.order - b.order)
  const perGroup = Math.ceil(positions / secondStageGroupIds.length)
  const seats: SecondStageSeatInput[] = []
  for (let position = 1; position <= positions; position++) {
    const groupId =
      secondStageGroupIds[
        Math.min(Math.floor((position - 1) / perGroup), secondStageGroupIds.length - 1)
      ]
    for (const source of sources) {
      if (position <= source.teams)
        seats.push({ groupId, sourceGroupId: source.id, sourcePosition: position })
    }
  }
  return seats
}

/**
 * Przypisanie miejsce → grupa fazy 2 (albo brak, gdy drużyna kończy turniej po fazie 1) zamienione
 * na listę miejsc do zapisu, w kolejności: grupa fazy 2, miejsce w tabeli, grupa fazy 1.
 */
export function seatsFromAssignment(
  assignment: Record<string, string | null>,
  firstStage: FirstStageGroup[],
  secondStageGroupIds: string[],
): SecondStageSeatInput[] {
  const sourceOrder = new Map(
    [...firstStage].sort((a, b) => a.order - b.order).map((g, i) => [g.id, i]),
  )
  return Object.entries(assignment)
    .filter(
      (entry): entry is [string, string] => !!entry[1] && secondStageGroupIds.includes(entry[1]),
    )
    .map(([key, groupId]) => {
      const { sourceGroupId, sourcePosition } = parsePlaceKey(key)
      return { groupId, sourceGroupId, sourcePosition }
    })
    .sort(
      (a, b) =>
        secondStageGroupIds.indexOf(a.groupId) - secondStageGroupIds.indexOf(b.groupId) ||
        a.sourcePosition - b.sourcePosition ||
        (sourceOrder.get(a.sourceGroupId) ?? 0) - (sourceOrder.get(b.sourceGroupId) ?? 0),
    )
}

export const placeKey = (sourceGroupId: string, sourcePosition: number) =>
  `${sourceGroupId}:${sourcePosition}`

function parsePlaceKey(key: string) {
  const at = key.lastIndexOf(":")
  return { sourceGroupId: key.slice(0, at), sourcePosition: Number(key.slice(at + 1)) }
}

/** Zapisane (albo zaproponowane) miejsca jako przypisanie miejsce → grupa fazy 2. */
export function assignmentFromSeats(
  seats: Pick<SecondStageSeatInput, "groupId" | "sourceGroupId" | "sourcePosition">[],
) {
  return Object.fromEntries(
    seats.map((s) => [placeKey(s.sourceGroupId, s.sourcePosition), s.groupId]),
  ) as Record<string, string | null>
}

/** Miejsca grupy fazy 2, które jeszcze czekają na drużynę: „1. miejsce Grupa A”… */
export function pendingPlaces(
  seats: Pick<
    SecondStageSeat,
    "groupId" | "sourceGroupId" | "sourcePosition" | "tournamentTeamId" | "order"
  >[],
  groupId: string,
  groups: Pick<TournamentGroup, "id" | "name">[],
) {
  const name = new Map(groups.map((g) => [g.id, g.name]))
  return seats
    .filter((s) => s.groupId === groupId && !s.tournamentTeamId)
    .sort((a, b) => a.order - b.order)
    .map((s) => placeLabel(s.sourcePosition, name.get(s.sourceGroupId)))
}

type SeatRef = Pick<SecondStageSeat, "sourceGroupId" | "sourcePosition">

/**
 * Miejsca grupy fazy 2 wierszami po pozycji: „1. miejsce: Grupa B, Grupa C, Grupa D”. Grupy fazy 1
 * w kolejności ich wyświetlania.
 */
export function placesByPosition(
  seats: SeatRef[],
  groups: Pick<TournamentGroup, "id" | "name" | "order">[],
) {
  const order = new Map(groups.map((g) => [g.id, g.order]))
  const name = new Map(groups.map((g) => [g.id, g.name]))
  const positions = [...new Set(seats.map((s) => s.sourcePosition))].sort((a, b) => a - b)
  return positions.map((position) => {
    const sources = seats
      .filter((s) => s.sourcePosition === position)
      .sort((a, b) => (order.get(a.sourceGroupId) ?? 0) - (order.get(b.sourceGroupId) ?? 0))
      .map((s) => name.get(s.sourceGroupId) ?? "grupa")
    return `${position}. miejsce: ${sources.join(", ")}`
  })
}

/**
 * Miejsca z podziału, których nie ma w tabeli: grupa ma już drużyny, ale mniej niż numer miejsca
 * (np. podział zapisany, zanim zgłosiły się drużyny). Grupa bez drużyn jeszcze się nie liczy.
 */
export function missingPlaces(
  seats: SeatRef[],
  teamsInGroup: (groupId: string) => number,
  groups: Pick<TournamentGroup, "id" | "name">[],
) {
  const name = new Map(groups.map((g) => [g.id, g.name]))
  return seats
    .filter((s) => {
      const teams = teamsInGroup(s.sourceGroupId)
      return teams > 0 && s.sourcePosition > teams
    })
    .map((s) => placeLabel(s.sourcePosition, name.get(s.sourceGroupId)))
}

/** Każdy z każdym w grupie fazy 2: n(n-1)/2 meczów. */
export function secondStageMatchCount(seats: Pick<SecondStageSeat, "groupId">[]) {
  const sizes = new Map<string, number>()
  for (const seat of seats) sizes.set(seat.groupId, (sizes.get(seat.groupId) ?? 0) + 1)
  return [...sizes.values()].reduce((sum, n) => sum + (n * (n - 1)) / 2, 0)
}

/**
 * Gotowy podział zakresami (jak SecondStagePlanner.DefaultRanges w backendzie): miejsca 1..N po równo
 * na kolejne przedziały, np. N = 4 i dwie grupy: 1-2 i 3-4.
 */
export function proposeRanges(
  largestGroup: number,
  secondStageGroupIds: string[],
): SecondStageRangeInput[] {
  if (largestGroup < 1 || secondStageGroupIds.length === 0) return []
  const perGroup = Math.ceil(largestGroup / secondStageGroupIds.length)
  return secondStageGroupIds.flatMap((groupId, i) => {
    const fromPosition = i * perGroup + 1
    return fromPosition > largestGroup
      ? []
      : [{ groupId, fromPosition, toPosition: Math.min((i + 1) * perGroup, largestGroup) }]
  })
}

/**
 * Ile miejsc ma największa grupa fazy 1: z drużyn w grupach, a gdy ich tam jeszcze nie ma - drużyny
 * kategorii po równo na grupy (8 drużyn, 4 grupy = 2), a bez żadnych drużyn 4.
 */
export function largestGroupEstimate(firstStage: FirstStageGroup[], categoryTeams: number) {
  return (
    Math.max(0, ...firstStage.map((g) => g.teams)) ||
    (firstStage.length > 0 && categoryTeams > 0
      ? Math.ceil(categoryTeams / firstStage.length)
      : 0) ||
    4
  )
}

/** Zakresy jednej kategorii nie mogą się nakładać - to samo miejsce trafiłoby do dwóch grup. */
export function rangesOverlap(
  ranges: Pick<SecondStageRangeInput, "fromPosition" | "toPosition">[],
) {
  const taken = new Set<number>()
  for (const r of ranges)
    for (let p = r.fromPosition; p <= r.toPosition; p++) {
      if (taken.has(p)) return true
      taken.add(p)
    }
  return false
}

/** „miejsca 1-2”, „miejsce 3”. */
export function rangeLabel(range: Pick<SecondStageRangeInput, "fromPosition" | "toPosition">) {
  return range.fromPosition === range.toPosition
    ? `miejsce ${range.fromPosition}`
    : `miejsca ${range.fromPosition}-${range.toPosition}`
}

/**
 * Miejsca, które powstaną przy generowaniu terminarza (jak PrepareSecondStageSeatsAsync): z zakresów,
 * a bez żadnego podziału z gotowego podziału po równo; pojedynczo ustawione miejsca bez zmian.
 */
export function effectiveSeats({
  groups,
  seats,
  ranges,
  teamsInGroup,
}: {
  groups: TournamentGroup[]
  seats: Pick<SecondStageSeat, "groupId" | "sourceGroupId" | "sourcePosition" | "categoryId">[]
  ranges: Pick<SecondStageRange, "groupId" | "fromPosition" | "toPosition" | "categoryId">[]
  teamsInGroup: (groupId: string) => number
}) {
  const categories = [...new Set(groups.map((g) => g.categoryId))]
  return categories.flatMap((categoryId) => {
    const inCategory = groups.filter((g) => g.categoryId === categoryId)
    const secondStage = inCategory.filter((g) => g.stage === 2).sort((a, b) => a.order - b.order)
    const firstStage = inCategory
      .filter((g) => g.stage !== 2)
      .map((g) => ({ id: g.id, name: g.name, order: g.order, teams: teamsInGroup(g.id) }))
    const own = ranges.filter((r) => r.categoryId === categoryId)
    const ownSeats = seats.filter((s) => s.categoryId === categoryId)
    if (own.length === 0 && ownSeats.length > 0) return ownSeats
    const used =
      own.length > 0
        ? own
        : proposeRanges(
            Math.max(0, ...firstStage.map((g) => g.teams)),
            secondStage.map((g) => g.id),
          )
    return seatsFromRanges(
      used,
      firstStage,
      secondStage.map((g) => g.id),
    ).map((s) => ({
      ...s,
      categoryId,
    }))
  })
}

/** Zakresy zamienione na miejsca z prawdziwej liczby drużyn: grupa z 3 drużynami nie ma 4. miejsca. */
export function seatsFromRanges(
  ranges: Pick<SecondStageRangeInput, "groupId" | "fromPosition" | "toPosition">[],
  firstStage: FirstStageGroup[],
  secondStageGroupIds: string[],
): SecondStageSeatInput[] {
  const sources = [...firstStage].sort((a, b) => a.order - b.order)
  return [...ranges]
    .sort((a, b) => secondStageGroupIds.indexOf(a.groupId) - secondStageGroupIds.indexOf(b.groupId))
    .flatMap((r) =>
      Array.from(
        { length: r.toPosition - r.fromPosition + 1 },
        (_, i) => r.fromPosition + i,
      ).flatMap((position) =>
        sources
          .filter((g) => position <= g.teams)
          .map((g) => ({ groupId: r.groupId, sourceGroupId: g.id, sourcePosition: position })),
      ),
    )
}

/**
 * Te same reguły co generator terminarza (BuildSecondStageAsync): każda grająca kategoria ma grupę
 * fazy 2 z miejscami, grupa fazy 2 ma co najmniej 2 miejsca, a miejsce istnieje w tabeli grupy.
 * Zwraca pierwszy problem albo null.
 */
export function secondStageProblem({
  playingCategoryIds,
  groups,
  seats,
  teamsInGroup,
}: {
  playingCategoryIds: string[]
  groups: TournamentGroup[]
  seats: Pick<SecondStageSeat, "groupId" | "sourceGroupId" | "sourcePosition">[]
  teamsInGroup: (groupId: string) => number
}): string | null {
  const withSeats = new Set(seats.map((s) => s.groupId))
  const secondStage = groups.filter((g) => g.stage === 2)
  if (
    playingCategoryIds.some(
      (c) => !secondStage.some((g) => g.categoryId === c && withSeats.has(g.id)),
    )
  )
    return "Każda kategoria potrzebuje grup drugiej fazy."
  const tooSmall = secondStage.find((g) => seats.filter((s) => s.groupId === g.id).length === 1)
  if (tooSmall) return `${tooSmall.name} ma tylko jedno miejsce, a potrzebuje co najmniej dwóch.`
  const missing = seats.find((s) => s.sourcePosition > teamsInGroup(s.sourceGroupId))
  if (missing) {
    const name = groups.find((g) => g.id === missing.sourceGroupId)?.name
    return `W podziale jest ${placeLabel(missing.sourcePosition, name)}, a ta grupa ma mniej drużyn.`
  }
  return null
}
