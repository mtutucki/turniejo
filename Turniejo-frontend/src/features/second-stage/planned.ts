import type { PlannedBracketMatch, PlannedSide } from "@/features/matches/planned-bracket-matches"
import type { SecondStage } from "@/types/tournaments"

import { placeLabel } from "./split"

type GroupRef = { id: string; name: string }

/**
 * Mecze grup drugiej fazy, które jeszcze nie powstały (czekają na koniec fazy 1), jako zapowiedzi
 * w terminarzu - w tym samym kształcie co zapowiedzi pucharu. Miejsce z już znaną drużyną pokazuje
 * jej nazwę, inaczej „1. miejsce Grupa A”.
 */
export function plannedSecondStageMatches(
  secondStage: SecondStage,
  groups: GroupRef[],
  teamName: (tournamentTeamId: string) => string,
): PlannedBracketMatch[] {
  const groupName = new Map(groups.map((g) => [g.id, g.name]))
  const seatById = new Map(secondStage.seats.map((s) => [s.id, s]))

  const side = (seatId: string): PlannedSide => {
    const seat = seatById.get(seatId)
    if (!seat) return { label: "Drużyna z fazy 1", known: false }
    return seat.tournamentTeamId
      ? {
          label: teamName(seat.tournamentTeamId),
          known: true,
          tournamentTeamId: seat.tournamentTeamId,
        }
      : { label: placeLabel(seat.sourcePosition, groupName.get(seat.sourceGroupId)), known: false }
  }

  return secondStage.fixtures
    .filter((f) => !f.matchId && f.plannedAt)
    .map((f) => ({
      nodeId: f.id,
      roundId: f.groupId,
      categoryId: f.categoryId,
      scheduledAt: f.plannedAt!,
      pitchId: f.plannedPitchId,
      refereeId: null,
      stage: groupName.get(f.groupId) ?? "Faza 2",
      home: side(f.homeSeatId),
      away: side(f.awaySeatId),
      secondStageGroupId: f.groupId,
    }))
    .sort((a, b) => a.scheduledAt.localeCompare(b.scheduledAt))
}
