"use client"

import { type ReactNode, useState } from "react"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
import { FieldLegend, FieldSet } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { isApiError } from "@/lib/api/errors"
import { type MatchLivePanel, isGoal } from "@/types/matches"

import { type FinishResultKind, scoringTeamId, useChangeMatchResult, useFinishMatch } from "../live-api"

const LABELS: Record<FinishResultKind, string> = {
  Regular: "Normalny",
  ExtraTime: "Po dogrywce",
  Penalties: "Karne",
  Walkover: "Walkower",
}

/** Gole z zapisanych zdarzeń. Po walkowerze homeScore/awayScore trzymają wynik z regulaminu. */
function goalScore(panel: MatchLivePanel) {
  const goals = panel.events.filter((e) => isGoal(e.type))
  const home = goals.filter((e) => scoringTeamId(panel, e) === panel.homeTeam.tournamentTeamId).length
  return { homeScore: home, awayScore: goals.length - home }
}

/**
 * Kończenie meczu, a w trybie "change" zmiana rozstrzygnięcia meczu już zakończonego (np. na walkower
 * albo karne). Te same reguły w obu przypadkach (backend: Match.Finish / ChangeFinishedResult).
 */
export function FinishMatchDialog({
  tournamentId,
  panel,
  editWindowMinutes,
  trigger,
  mode = "finish",
}: {
  tournamentId: string
  panel: MatchLivePanel
  editWindowMinutes?: number
  trigger: ReactNode
  mode?: "finish" | "change"
}) {
  const changing = mode === "change"
  const { homeTeam, awayTeam } = panel
  const { homeScore, awayScore } = changing
    ? goalScore(panel)
    : { homeScore: panel.homeScore, awayScore: panel.awayScore }
  // Mecz pucharowy musi wyłonić zwycięzcę — remis rozstrzygają karne (backend: Match.Finish).
  const knockout = panel.stageType !== "Group"
  const draw = homeScore === awayScore
  const kinds: FinishResultKind[] = knockout
    ? draw
      ? ["Penalties", "Walkover"]
      : ["Regular", "ExtraTime", "Walkover"]
    : ["Regular", "Walkover"]
  const current = kinds.find((k) => k === panel.resultType)
  const defaultKind: FinishResultKind =
    changing && current ? current : knockout && draw ? "Penalties" : "Regular"

  const [open, setOpen] = useState(false)
  const [kind, setKind] = useState<FinishResultKind>(defaultKind)
  const [walkoverWinner, setWalkoverWinner] = useState<string | null>(null)
  const [homePens, setHomePens] = useState("")
  const [awayPens, setAwayPens] = useState("")
  const [error, setError] = useState<string | null>(null)
  const finishMatch = useFinishMatch(tournamentId, panel.matchId)
  const changeResult = useChangeMatchResult(tournamentId, panel.matchId)
  const finish = changing ? changeResult : finishMatch

  const scoreWinner =
    homeScore > awayScore
      ? homeTeam.tournamentTeamId
      : awayScore > homeScore
        ? awayTeam.tournamentTeamId
        : null

  function confirm() {
    setError(null)
    if (kind === "Walkover" && !walkoverWinner) {
      setError("Wybierz drużynę, która wygrywa walkowerem")
      return
    }
    const home = Number(homePens)
    const away = Number(awayPens)
    if (kind === "Penalties") {
      if (homePens === "" || awayPens === "" || home < 0 || away < 0) {
        setError("Wpisz wynik rzutów karnych")
        return
      }
      if (home === away) {
        setError("Karne muszą wyłonić zwycięzcę")
        return
      }
    }
    finish.mutate(
      {
        resultType: kind,
        winnerTournamentTeamId:
          kind === "Walkover"
            ? walkoverWinner
            : kind === "Penalties"
              ? home > away
                ? homeTeam.tournamentTeamId
                : awayTeam.tournamentTeamId
              : scoreWinner,
        homePenaltyScore: kind === "Penalties" ? home : null,
        awayPenaltyScore: kind === "Penalties" ? away : null,
      },
      {
        // Bez toastu — status "Zakończony" widać na tablicy (toast zasłaniałby nagłówek na telefonie).
        onSuccess: () => setOpen(false),
        onError: (e) => {
          if (isApiError(e) && e.fieldErrors.length) setError(e.fieldErrors[0]!.message)
        },
      },
    )
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setKind(defaultKind)
          setWalkoverWinner(
            changing && panel.resultType === "Walkover" ? panel.winnerTournamentTeamId : null,
          )
          setHomePens(changing ? (panel.homePenaltyScore?.toString() ?? "") : "")
          setAwayPens(changing ? (panel.awayPenaltyScore?.toString() ?? "") : "")
          setError(null)
        }
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{changing ? "Zmień rozstrzygnięcie" : "Zakończyć mecz?"}</DialogTitle>
          <DialogDescription>
            {changing
              ? "Tabela i drabinka przeliczą się same."
              : editWindowMinutes != null
                ? `Po zakończeniu przez ${editWindowMinutes} min możesz jeszcze poprawiać zdarzenia, potem wynik jest zablokowany.`
                : "Po zakończeniu wynik zostanie zablokowany po czasie na poprawki."}
          </DialogDescription>
        </DialogHeader>

        <FieldSet>
          <FieldLegend variant="label">Rozstrzygnięcie</FieldLegend>
          <div
            className="grid gap-2"
            style={{ gridTemplateColumns: `repeat(${kinds.length}, minmax(0, 1fr))` }}
          >
            {kinds.map((k) => (
              <Button
                key={k}
                variant={kind === k ? "default" : "outline"}
                aria-pressed={kind === k}
                className="h-12 px-2"
                onClick={() => setKind(k)}
              >
                {LABELS[k]}
              </Button>
            ))}
          </div>
          {knockout && draw && (
            <p className="text-muted-foreground text-xs">
              Mecz pucharowy nie może zakończyć się remisem - rozstrzygnij go karnymi.
            </p>
          )}
        </FieldSet>

        {kind === "Walkover" ? (
          <FieldSet>
            <FieldLegend variant="label">Walkower wygrywa</FieldLegend>
            <div className="grid grid-cols-2 gap-2">
              {[homeTeam, awayTeam].map((team) => (
                <Button
                  key={team.tournamentTeamId}
                  variant={walkoverWinner === team.tournamentTeamId ? "default" : "outline"}
                  aria-pressed={walkoverWinner === team.tournamentTeamId}
                  className="h-12 min-w-0"
                  onClick={() => setWalkoverWinner(team.tournamentTeamId)}
                >
                  <span className="truncate">{team.shortName || team.name}</span>
                </Button>
              ))}
            </div>
            <p className="text-muted-foreground text-xs">
              Wynik walkowera ustawia się z ustawień turnieju.
            </p>
          </FieldSet>
        ) : (
          <div className="bg-muted/40 rounded-lg border p-3">
            <div className="grid grid-cols-[1fr_auto_1fr] items-center gap-2 text-center">
              <span className="truncate text-sm">{homeTeam.name}</span>
              <span className="font-mono text-3xl font-semibold tabular-nums">
                {homeScore} : {awayScore}
              </span>
              <span className="truncate text-sm">{awayTeam.name}</span>
            </div>
            {kind === "Penalties" && (
              <div className="mt-3 grid grid-cols-[1fr_auto_1fr] items-center gap-2">
                <Input
                  aria-label={`Karne: ${homeTeam.name}`}
                  inputMode="numeric"
                  type="number"
                  min={0}
                  className="h-12 text-center font-mono text-lg tabular-nums"
                  value={homePens}
                  onChange={(e) => setHomePens(e.target.value)}
                />
                <span className="text-muted-foreground text-xs">karne</span>
                <Input
                  aria-label={`Karne: ${awayTeam.name}`}
                  inputMode="numeric"
                  type="number"
                  min={0}
                  className="h-12 text-center font-mono text-lg tabular-nums"
                  value={awayPens}
                  onChange={(e) => setAwayPens(e.target.value)}
                />
              </div>
            )}
          </div>
        )}

        {error && <p className="text-destructive text-sm">{error}</p>}

        <DialogFooter>
          <Button variant="ghost" className="h-12" onClick={() => setOpen(false)}>
            {changing ? "Anuluj" : "Wróć do meczu"}
          </Button>
          <Button className="h-12" onClick={confirm} disabled={finish.isPending}>
            {changing
              ? finish.isPending
                ? "Zapisywanie…"
                : "Zapisz"
              : finish.isPending
                ? "Kończenie…"
                : "Zakończ mecz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
