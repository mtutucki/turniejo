"use client"

import { type ReactNode, useState } from "react"
import { toast } from "sonner"

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
import { Field, FieldLabel, FieldLegend, FieldSet } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { cn } from "@/lib/utils"
import {
  type CardType,
  GOAL_TYPE_LABELS,
  GOAL_TYPES,
  type GoalType,
  type MatchLivePanel,
  type MatchPlayer,
  type MatchTeam,
} from "@/types/matches"

import { cardState, effectiveCardType, useAddCard, useAddGoal, useSelectMvp } from "../live-api"

const NONE = "none"

const playerName = (p: MatchPlayer) =>
  `${p.shirtNumber != null ? `${p.shirtNumber}. ` : ""}${p.firstName} ${p.lastName}`

// Usuniętych ze składu nie da się wybrać do nowego gola czy kartki (ich stare wydarzenia zostają).
const byShirt = (players: MatchPlayer[]) =>
  players
    .filter((p) => p.status !== "Removed")
    .sort((a, b) => (a.shirtNumber ?? 999) - (b.shirtNumber ?? 999))

/** Duże przyciski wyboru — pod kciuk, zamiast listy rozwijanej. */
function Segmented<T extends string>({
  legend,
  value,
  onChange,
  options,
}: {
  legend: string
  value: T
  onChange: (value: T) => void
  options: { value: T; label: ReactNode }[]
}) {
  return (
    <FieldSet>
      <FieldLegend variant="label">{legend}</FieldLegend>
      <div
        className="grid gap-2"
        style={{ gridTemplateColumns: `repeat(${options.length}, minmax(0, 1fr))` }}
      >
        {options.map((o) => (
          <Button
            key={o.value}
            type="button"
            variant={value === o.value ? "default" : "outline"}
            aria-pressed={value === o.value}
            className="h-12 min-w-0 px-2"
            onClick={() => onChange(o.value)}
          >
            <span className="truncate">{o.label}</span>
          </Button>
        ))}
      </div>
    </FieldSet>
  )
}

function PlayerSelect({
  id,
  label,
  players,
  value,
  onChange,
  allowNone,
  cards,
}: {
  id: string
  label: string
  players: MatchPlayer[]
  value: string
  onChange: (value: string) => void
  allowNone?: string
  /** Kartki w meczu: zawodnik z czerwoną jest wykluczony, żółta — adnotacja. */
  cards: ReturnType<typeof cardState>
}) {
  return (
    <Field>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger id={id} className="h-12 w-full">
          <SelectValue placeholder="Wybierz zawodnika" />
        </SelectTrigger>
        <SelectContent>
          {allowNone && <SelectItem value={NONE}>{allowNone}</SelectItem>}
          {byShirt(players).map((p) => (
            <SelectItem key={p.playerId} value={p.playerId} disabled={cards.red.has(p.playerId)}>
              {playerName(p)}
              {cards.red.has(p.playerId)
                ? " · czerwona kartka"
                : cards.yellow.has(p.playerId)
                  ? " · żółta kartka"
                  : ""}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </Field>
  )
}

function MinuteField({ value, onChange }: { value: number; onChange: (value: number) => void }) {
  return (
    <Field>
      <FieldLabel htmlFor="event-minute">Minuta</FieldLabel>
      <Input
        id="event-minute"
        type="number"
        inputMode="numeric"
        min={0}
        max={300}
        className="h-12 font-mono text-lg tabular-nums sm:max-w-28"
        value={Number.isNaN(value) ? "" : value}
        onChange={(e) => onChange(e.target.valueAsNumber)}
      />
    </Field>
  )
}

const teamOptions = (panel: MatchLivePanel) => [
  {
    value: panel.homeTeam.tournamentTeamId,
    label: panel.homeTeam.shortName || panel.homeTeam.name,
  },
  {
    value: panel.awayTeam.tournamentTeamId,
    label: panel.awayTeam.shortName || panel.awayTeam.name,
  },
]

const teamById = (panel: MatchLivePanel, id: string): MatchTeam =>
  id === panel.homeTeam.tournamentTeamId ? panel.homeTeam : panel.awayTeam
const opponentOf = (panel: MatchLivePanel, id: string): MatchTeam =>
  id === panel.homeTeam.tournamentTeamId ? panel.awayTeam : panel.homeTeam

// --- Gol -----------------------------------------------------------------------------------

export function AddGoalDialog({
  tournamentId,
  panel,
  minute,
  trigger,
}: {
  tournamentId: string
  panel: MatchLivePanel
  /** Bieżąca minuta meczu — domyślna wartość pola. */
  minute: number
  trigger: ReactNode
}) {
  const [open, setOpen] = useState(false)
  const [scoringTeam, setScoringTeam] = useState(panel.homeTeam.tournamentTeamId)
  const [goalType, setGoalType] = useState<GoalType>("Goal")
  const [playerId, setPlayerId] = useState(NONE)
  const [assistId, setAssistId] = useState(NONE)
  const [eventMinute, setEventMinute] = useState(minute)
  const addGoal = useAddGoal(tournamentId, panel.matchId)
  const cards = cardState(panel)

  // Samobój: strzelcem jest zawodnik drużyny przeciwnej, a backend oczekuje drużyny strzelca
  // (RecalculateMatchScore zalicza samobója przeciwnikowi).
  const shooterTeam =
    goalType === "OwnGoal" ? opponentOf(panel, scoringTeam) : teamById(panel, scoringTeam)

  function reset() {
    setScoringTeam(panel.homeTeam.tournamentTeamId)
    setGoalType("Goal")
    setPlayerId(NONE)
    setAssistId(NONE)
    setEventMinute(minute)
  }

  function save() {
    if (Number.isNaN(eventMinute) || eventMinute < 0 || eventMinute > 300) {
      toast.error("Minuta musi być od 0 do 300")
      return
    }
    // Optimistic update — dialog zamykamy od razu, wynik już się zmienił.
    addGoal.mutate(
      {
        tournamentTeamId: shooterTeam.tournamentTeamId,
        playerId: playerId === NONE ? null : playerId,
        assistPlayerId: goalType === "OwnGoal" || assistId === NONE ? null : assistId,
        goalType,
        minute: eventMinute,
      },
      // Błąd pokazuje globalny handler (MutationCache), a onError mutacji cofa wynik.
    )
    setOpen(false)
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) reset()
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Gol</DialogTitle>
          <DialogDescription>Wynik zmieni się od razu po zapisaniu.</DialogDescription>
        </DialogHeader>
        <div className="space-y-5">
          <Segmented
            legend="Gol dla"
            value={scoringTeam}
            onChange={(value) => {
              setScoringTeam(value)
              setPlayerId(NONE)
              setAssistId(NONE)
            }}
            options={teamOptions(panel)}
          />
          <Segmented
            legend="Rodzaj"
            value={goalType}
            onChange={(value) => {
              setGoalType(value)
              setPlayerId(NONE)
              setAssistId(NONE)
            }}
            options={GOAL_TYPES.map((t) => ({ value: t, label: GOAL_TYPE_LABELS[t] }))}
          />
          <PlayerSelect
            id="goal-player"
            label={goalType === "OwnGoal" ? `Strzelec samobója (${shooterTeam.name})` : "Strzelec"}
            players={shooterTeam.players}
            value={playerId}
            onChange={setPlayerId}
            allowNone="Nie wiadomo / bez zawodnika"
            cards={cards}
          />
          {goalType !== "OwnGoal" && (
            <PlayerSelect
              id="goal-assist"
              label="Asysta (opcjonalnie)"
              players={shooterTeam.players.filter((p) => p.playerId !== playerId)}
              value={assistId}
              onChange={setAssistId}
              allowNone="Bez asysty"
              cards={cards}
            />
          )}
          <MinuteField value={eventMinute} onChange={setEventMinute} />
        </div>
        <DialogFooter>
          <Button variant="ghost" className="h-12" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button className="h-12" onClick={save}>
            Zapisz gola
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// --- Kartka --------------------------------------------------------------------------------

export function AddCardDialog({
  tournamentId,
  panel,
  minute,
  trigger,
}: {
  tournamentId: string
  panel: MatchLivePanel
  minute: number
  trigger: ReactNode
}) {
  const [open, setOpen] = useState(false)
  const [teamId, setTeamId] = useState(panel.homeTeam.tournamentTeamId)
  const [cardType, setCardType] = useState<CardType>("YellowCard")
  const [playerId, setPlayerId] = useState("")
  const [eventMinute, setEventMinute] = useState(minute)
  const addCard = useAddCard(tournamentId, panel.matchId)
  const team = teamById(panel, teamId)
  const cards = cardState(panel)
  const secondYellow = !!playerId && effectiveCardType(panel, playerId, cardType) !== cardType

  function save() {
    if (!playerId) {
      toast.error("Wybierz zawodnika")
      return
    }
    if (Number.isNaN(eventMinute) || eventMinute < 0 || eventMinute > 300) {
      toast.error("Minuta musi być od 0 do 300")
      return
    }
    addCard.mutate(
      { tournamentTeamId: teamId, playerId, cardType, minute: eventMinute },
      {
        onSuccess: (response) => {
          if (response.cardType === "RedCard" && cardType === "YellowCard")
            toast.warning("Druga żółta kartka — zawodnik dostał czerwoną")
        },
      },
    )
    setOpen(false)
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setTeamId(panel.homeTeam.tournamentTeamId)
          setCardType("YellowCard")
          setPlayerId("")
          setEventMinute(minute)
        }
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Kartka</DialogTitle>
        </DialogHeader>
        <div className="space-y-5">
          <Segmented
            legend="Kolor"
            value={cardType}
            onChange={setCardType}
            options={[
              {
                value: "YellowCard",
                label: (
                  <span className="flex items-center gap-2">
                    <span className="h-4 w-3 rounded-xs bg-yellow-400" aria-hidden /> Żółta
                  </span>
                ),
              },
              {
                value: "RedCard",
                label: (
                  <span className="flex items-center gap-2">
                    <span className="h-4 w-3 rounded-xs bg-red-500" aria-hidden /> Czerwona
                  </span>
                ),
              },
            ]}
          />
          <Segmented
            legend="Drużyna"
            value={teamId}
            onChange={(value) => {
              setTeamId(value)
              setPlayerId("")
            }}
            options={teamOptions(panel)}
          />
          <PlayerSelect
            id="card-player"
            label="Zawodnik"
            players={team.players}
            value={playerId}
            onChange={setPlayerId}
            cards={cards}
          />
          {secondYellow && (
            <p className="rounded-lg border border-red-500/40 bg-red-500/10 p-3 text-sm">
              Druga żółta kartka tego zawodnika — zostanie zapisana jako czerwona.
            </p>
          )}
          <MinuteField value={eventMinute} onChange={setEventMinute} />
        </div>
        <DialogFooter>
          <Button variant="ghost" className="h-12" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button
            className={cn(
              "h-12",
              cardType === "RedCard" && "bg-red-600 text-white hover:bg-red-600/90",
            )}
            onClick={save}
          >
            Zapisz kartkę
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// --- MVP -----------------------------------------------------------------------------------

const NO_CARDS: ReturnType<typeof cardState> = { yellow: new Set(), red: new Set() }

export function SelectMvpDialog({
  tournamentId,
  panel,
  current,
  trigger,
}: {
  tournamentId: string
  panel: MatchLivePanel
  /** Obecne MVP — dialog startuje od jego drużyny i zawodnika. */
  current: { tournamentTeamId: string | null; playerId: string | null } | undefined
  trigger: ReactNode
}) {
  const [open, setOpen] = useState(false)
  const [teamId, setTeamId] = useState(panel.homeTeam.tournamentTeamId)
  const [playerId, setPlayerId] = useState("")
  const selectMvp = useSelectMvp(tournamentId, panel.matchId)
  const team = teamById(panel, teamId)

  function save() {
    if (!playerId) {
      toast.error("Wybierz zawodnika")
      return
    }
    selectMvp.mutate({ tournamentTeamId: teamId, playerId }, { onSuccess: () => setOpen(false) })
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setTeamId(current?.tournamentTeamId ?? panel.homeTeam.tournamentTeamId)
          setPlayerId(current?.playerId ?? "")
        }
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>MVP meczu</DialogTitle>
          <DialogDescription>
            Najlepszy zawodnik meczu — trafi do rankingu MVP turnieju.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-5">
          <Segmented
            legend="Drużyna"
            value={teamId}
            onChange={(value) => {
              setTeamId(value)
              setPlayerId("")
            }}
            options={teamOptions(panel)}
          />
          <PlayerSelect
            id="mvp-player"
            label="Zawodnik"
            players={team.players}
            value={playerId}
            onChange={setPlayerId}
            cards={NO_CARDS}
          />
        </div>
        <DialogFooter>
          <Button variant="ghost" className="h-12" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button className="h-12" onClick={save} disabled={selectMvp.isPending}>
            Zapisz MVP
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
