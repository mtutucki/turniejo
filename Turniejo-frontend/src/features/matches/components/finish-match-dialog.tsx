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

import {
  type FinishResultKind,
  scoringTeamId,
  useChangeMatchResult,
  useFinishMatch,
  useRecordMatchResult,
} from "../live-api"

const LABELS: Record<FinishResultKind, string> = {
  Regular: "Normalny",
  ExtraTime: "Po dogrywce",
  Penalties: "Karne",
  Walkover: "Walkower",
}

/** Gole z zapisanych zdarzeń. Po walkowerze homeScore/awayScore trzymają wynik z regulaminu. */
function goalScore(panel: MatchLivePanel) {
  const goals = panel.events.filter((e) => isGoal(e.type))
  const home = goals.filter(
    (e) => scoringTeamId(panel, e) === panel.homeTeam.tournamentTeamId,
  ).length
  return { homeScore: home, awayScore: goals.length - home }
}

const parseGoals = (value: string) => (/^\d{1,2}$/.test(value) ? Number(value) : null)

/**
 * Kończenie meczu. Tryb "change": zmiana rozstrzygnięcia meczu już zakończonego (np. na walkower albo
 * karne). Tryb "record": wpisanie wyniku meczu, którego nie prowadzono na żywo (bez zegara).
 * Te same reguły we wszystkich trybach (backend: Match.Finish / ChangeFinishedResult / RecordResult).
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
  mode?: "finish" | "change" | "record"
}) {
  const changing = mode === "change"
  const recording = mode === "record"
  // Przy wpisywaniu wyniku i przy zmianie rozstrzygnięcia wynik jest w polach do edycji.
  const editScore = recording || changing
  const { homeTeam, awayTeam } = panel
  const [homeGoals, setHomeGoals] = useState("")
  const [awayGoals, setAwayGoals] = useState("")
  const scoresFilled = parseGoals(homeGoals) != null && parseGoals(awayGoals) != null
  const { homeScore, awayScore } = editScore
    ? { homeScore: parseGoals(homeGoals) ?? 0, awayScore: parseGoals(awayGoals) ?? 0 }
    : { homeScore: panel.homeScore, awayScore: panel.awayScore }
  // Mecz pucharowy musi wyłonić zwycięzcę — remis rozstrzygają karne (backend: Match.Finish).
  const knockout = panel.stageType !== "Group"
  const draw = homeScore === awayScore
  const kinds: FinishResultKind[] = knockout
    ? draw
      ? ["Penalties", "Walkover"]
      : ["Regular", "ExtraTime", "Walkover"]
    : draw
      ? ["Regular", "Penalties", "Walkover"]
      : ["Regular", "Walkover"]
  // Przy zmianie startujemy od obecnego rozstrzygnięcia (wynik w polach ustawia się dopiero przy otwarciu).
  const current = (Object.keys(LABELS) as FinishResultKind[]).find((k) => k === panel.resultType)
  const defaultKind: FinishResultKind =
    changing && current ? current : knockout && draw ? "Penalties" : "Regular"

  const [open, setOpen] = useState(false)
  const [chosenKind, setKind] = useState<FinishResultKind>(defaultKind)
  // Przy wpisywaniu wyniku lista sposobów zmienia się razem z wynikiem (np. remis w pucharze → karne).
  const kind = kinds.includes(chosenKind) ? chosenKind : kinds[0]!
  // Przy wpisywaniu wyniku walkower to osobna ścieżka (bez goli), a rozstrzygnięcie pokazujemy
  // dopiero po wpisaniu wyniku. Remis w pucharze daje tylko karne, więc wtedy bez przycisków.
  const kindButtons = recording ? kinds.filter((k) => k !== "Walkover") : kinds
  const showKinds = !recording || (scoresFilled && kindButtons.length > 1)
  const showPenalties = kind === "Penalties" && (!editScore || scoresFilled)
  const [walkoverWinner, setWalkoverWinner] = useState<string | null>(null)
  const [homePens, setHomePens] = useState("")
  const [awayPens, setAwayPens] = useState("")
  const [error, setError] = useState<string | null>(null)
  const finishMatch = useFinishMatch(tournamentId, panel.matchId)
  const changeResult = useChangeMatchResult(tournamentId, panel.matchId)
  const recordResult = useRecordMatchResult(tournamentId, panel.matchId)
  const finish = changing ? changeResult : recording ? recordResult : finishMatch

  const scoreWinner =
    homeScore > awayScore
      ? homeTeam.tournamentTeamId
      : awayScore > homeScore
        ? awayTeam.tournamentTeamId
        : null

  function confirm() {
    setError(null)
    const goalsMissing = parseGoals(homeGoals) == null || parseGoals(awayGoals) == null
    if (editScore && kind !== "Walkover" && goalsMissing) {
      setError("Wpisz liczbę goli obu drużyn (0-99)")
      return
    }
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
        ...(editScore && kind !== "Walkover" ? { homeScore, awayScore } : {}),
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
          const goals = goalScore(panel)
          setHomeGoals(changing ? String(goals.homeScore) : "")
          setAwayGoals(changing ? String(goals.awayScore) : "")
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
          <DialogTitle>
            {changing ? "Zmień rozstrzygnięcie" : recording ? "Wpisz wynik" : "Zakończyć mecz?"}
          </DialogTitle>
          <DialogDescription>
            {changing
              ? "Możesz też poprawić wynik. Brakujące gole dopiszemy bez strzelca, a nadmiarowe cofniemy, zaczynając od tych bez strzelca. Tabela i drabinka przeliczą się same."
              : recording
                ? "Mecz od razu będzie zakończony, bez zegara. Strzelców dopiszesz potem na liście zdarzeń."
                : editWindowMinutes != null
                  ? `Po zakończeniu przez ${editWindowMinutes} min możesz jeszcze poprawiać zdarzenia, potem wynik jest zablokowany.`
                  : "Po zakończeniu wynik zostanie zablokowany po czasie na poprawki."}
          </DialogDescription>
        </DialogHeader>

        {editScore && kind !== "Walkover" && (
          <FieldSet>
            <FieldLegend variant="label">Wynik</FieldLegend>
            <div className="grid grid-cols-[1fr_auto_1fr] items-center gap-2 text-center">
              {[
                { team: homeTeam, value: homeGoals, set: setHomeGoals },
                { team: awayTeam, value: awayGoals, set: setAwayGoals },
              ].map(({ team, value, set }, i) => (
                <div key={team.tournamentTeamId} className={i === 1 ? "order-3" : undefined}>
                  <Input
                    aria-label={`Gole: ${team.name}`}
                    inputMode="numeric"
                    type="number"
                    min={0}
                    max={99}
                    className="h-14 text-center font-mono text-2xl tabular-nums"
                    placeholder="-"
                    value={value}
                    onFocus={(e) => e.target.select()}
                    onChange={(e) => set(e.target.value)}
                  />
                  <p className="mt-1 truncate text-sm">{team.name}</p>
                </div>
              ))}
              <span className="text-muted-foreground order-2 pb-6 text-2xl">:</span>
            </div>
            {recording && !scoresFilled && (
              <p className="text-muted-foreground text-center text-xs">
                Były karne? Wpisz remis z boiska, a pojawią się pola na rzuty karne.
              </p>
            )}
            {recording && (
              <Button
                variant="link"
                className="text-muted-foreground h-auto p-0 text-sm"
                onClick={() => setKind("Walkover")}
              >
                Mecz się nie odbył? Wpisz walkower
              </Button>
            )}
          </FieldSet>
        )}

        {showKinds && (
          <FieldSet>
            <FieldLegend variant="label">Rozstrzygnięcie</FieldLegend>
            <div
              className="grid gap-2"
              style={{ gridTemplateColumns: `repeat(${kindButtons.length}, minmax(0, 1fr))` }}
            >
              {kindButtons.map((k) => (
                <Button
                  key={k}
                  variant={kind === k ? "default" : "outline"}
                  aria-pressed={kind === k}
                  className="h-12 px-2"
                  onClick={() => setKind(k)}
                >
                  {k === "Regular" && draw && !knockout ? "Remis" : LABELS[k]}
                </Button>
              ))}
            </div>
            {knockout && draw && !recording && (
              <p className="text-muted-foreground text-xs">
                Mecz pucharowy nie może zakończyć się remisem - rozstrzygnij go karnymi.
              </p>
            )}
          </FieldSet>
        )}

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
            {recording && (
              <Button
                variant="link"
                className="text-muted-foreground h-auto justify-start p-0 text-sm"
                onClick={() => setKind("Regular")}
              >
                Wróć do wpisywania wyniku
              </Button>
            )}
          </FieldSet>
        ) : editScore && !showPenalties ? null : (
          <div className="bg-muted/40 rounded-lg border p-3">
            {/* Przy wpisywaniu wyniku gole są już w polach wyżej. */}
            {!editScore && (
              <div className="grid grid-cols-[1fr_auto_1fr] items-center gap-2 text-center">
                <span className="truncate text-sm">{homeTeam.name}</span>
                <span className="font-mono text-3xl font-semibold tabular-nums">
                  {homeScore} : {awayScore}
                </span>
                <span className="truncate text-sm">{awayTeam.name}</span>
              </div>
            )}
            {editScore && knockout && (
              <p className="mb-2 text-sm">Remis w meczu pucharowym. Wpisz wynik rzutów karnych.</p>
            )}
            {!knockout && kind === "Penalties" && (
              <p className="text-muted-foreground mb-2 text-xs">
                W tabeli grupy mecz i tak liczy się jako remis. Karne zostaną zapisane przy meczu.
              </p>
            )}
            {showPenalties && (
              <div
                className={`grid grid-cols-[1fr_auto_1fr] items-center gap-2 ${editScore ? "" : "mt-3"}`}
              >
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
            {changing || recording ? "Anuluj" : "Wróć do meczu"}
          </Button>
          <Button className="h-12" onClick={confirm} disabled={finish.isPending}>
            {changing || recording
              ? finish.isPending
                ? "Zapisywanie…"
                : recording
                  ? "Zapisz wynik"
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
