"use client"

import { useMemo } from "react"

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useBrackets, useSchedule, useStandings } from "@/features/matches/api"
import {
  bracketMatchStages,
  plannedBracketMatches,
} from "@/features/matches/planned-bracket-matches"
import { useTournamentMatchesRealtime } from "@/features/matches/use-tournament-matches-realtime"
import { useGroups } from "@/features/tournaments/api"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { plural } from "@/lib/format"
import type { TournamentTeam } from "@/types/teams"

import { useTournamentTeams } from "../api"
import { cupPath, teamRecord } from "../team-record"
import { CupPathCard, TeamMatchList, TeamStats } from "./team-stats"

/** Panel: bilans, droga w pucharze i wszystkie mecze drużyny z wynikami (strona drużyny turnieju). */
export function TeamResultsCard({ team }: { team: TournamentTeam }) {
  const { id, tournament } = useTournamentContext()
  useTournamentMatchesRealtime([id])
  const schedule = useSchedule(id)
  const teams = useTournamentTeams(id)
  const groups = useGroups(id)
  const format = tournament.data?.format
  const hasGroups = format !== "Knockout"
  const hasKnockout = format === "Knockout" || format === "GroupAndKnockout"
  const standings = useStandings(id, hasGroups ? (team.groupId ?? "") : "")
  const bracket = useBrackets(id, [team.categoryId], hasKnockout)[0]?.data

  const teamName = useMemo(() => {
    const byId = new Map((teams.data ?? []).map((t) => [t.id, t.teamName]))
    return (teamId: string) => byId.get(teamId) ?? "Drużyna"
  }, [teams.data])
  const record = useMemo(() => teamRecord(schedule.data ?? [], team.id), [schedule.data, team.id])
  const stages = useMemo(
    () => (bracket ? bracketMatchStages(bracket) : new Map<string, string>()),
    [bracket],
  )
  const cup = useMemo(() => {
    if (!bracket) return null
    const planned = plannedBracketMatches(bracket, team.categoryId, teamName)
    return cupPath(schedule.data ?? [], team.id, stages, planned)
  }, [bracket, schedule.data, stages, team.categoryId, team.id, teamName])

  const groupName = groups.data?.find((g) => g.id === team.groupId)?.name
  const row = team.groupId ? standings.data?.find((r) => r.tournamentTeamId === team.id) : undefined
  const links = {
    teamName,
    teamHref: (teamId: string) =>
      teamId === team.id ? undefined : `/tournaments/${id}/teams/${teamId}`,
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Mecze i wyniki</CardTitle>
        <CardDescription>
          {schedule.isPending
            ? "Wczytywanie…"
            : record.matches.length
              ? plural(record.matches.length, ["mecz", "mecze", "meczów"])
              : "Drużyna nie ma jeszcze meczów. Pojawią się po wygenerowaniu terminarza."}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-6">
        {schedule.isPending ? (
          <Skeleton className="h-24 w-full" />
        ) : (
          record.matches.length > 0 && (
            <>
              <TeamStats
                record={record}
                standing={
                  row && groupName
                    ? { position: row.position, points: row.points, groupName }
                    : null
                }
              />
              {cup && <CupPathCard cup={cup} teamId={team.id} links={links} />}
              <TeamMatchList
                record={record}
                teamId={team.id}
                links={links}
                stageLabel={(m) =>
                  stages.get(m.id) ??
                  (m.groupId ? (groups.data?.find((g) => g.id === m.groupId)?.name ?? null) : null)
                }
              />
            </>
          )
        )}
      </CardContent>
    </Card>
  )
}
