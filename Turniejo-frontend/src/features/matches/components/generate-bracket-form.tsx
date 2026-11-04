"use client"

import { GitBranch } from "lucide-react"
import { useState } from "react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { FieldDescription } from "@/components/ui/field"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { isApiError } from "@/lib/api/errors"
import type { TournamentTeam } from "@/types/teams"
import type { TournamentGroup } from "@/types/tournaments"

import { type BracketSlotInput, useGenerateBracket } from "../api"
import { type BracketPair, defaultPairCount, suggestPairs } from "../suggest-bracket"

type Pair = BracketPair

const SIZES = [
  { pairs: 1, label: "Finał (2 drużyny)" },
  { pairs: 2, label: "Półfinały (3–4 drużyny)" },
  { pairs: 4, label: "Ćwierćfinały (5–8 drużyn)" },
  { pairs: 8, label: "1/8 finału (9–16 drużyn)" },
]

const BYE = "bye"

// Wartość w Select: "g:<groupId>:<pozycja>", "t:<tournamentTeamId>" albo "bye" (wolny los).
const encode = (slot: BracketSlotInput | null) =>
  !slot
    ? ""
    : slot.kind === "bye"
      ? BYE
      : slot.kind === "group"
        ? `g:${slot.groupId}:${slot.position}`
        : `t:${slot.tournamentTeamId}`
const decode = (value: string): BracketSlotInput | null => {
  if (value === BYE) return { kind: "bye" }
  const [kind, id, position] = value.split(":")
  if (kind === "g" && id && position)
    return { kind: "group", groupId: id, position: Number(position) }
  if (kind === "t" && id) return { kind: "team", tournamentTeamId: id }
  return null
}

function SlotSelect({
  label,
  value,
  onChange,
  groups,
  teams,
}: {
  label: string
  value: BracketSlotInput | null
  onChange: (slot: BracketSlotInput | null) => void
  groups: TournamentGroup[]
  teams: TournamentTeam[]
}) {
  const maxPosition = Math.max(
    2,
    ...groups.map((g) => teams.filter((t) => t.groupId === g.id).length),
  )
  return (
    <Select value={encode(value)} onValueChange={(v) => onChange(decode(v))}>
      <SelectTrigger className="h-10 w-full min-w-0" aria-label={label}>
        <SelectValue placeholder="Wybierz" />
      </SelectTrigger>
      <SelectContent>
        {groups.flatMap((g) =>
          Array.from({ length: maxPosition }, (_, i) => (
            <SelectItem key={`${g.id}-${i + 1}`} value={`g:${g.id}:${i + 1}`}>
              {i + 1}. miejsce · {g.name}
            </SelectItem>
          )),
        )}
        {teams.map((t) => (
          <SelectItem key={t.id} value={`t:${t.id}`}>
            {t.teamName}
          </SelectItem>
        ))}
        <SelectItem value={BYE}>Wolny los</SelectItem>
      </SelectContent>
    </Select>
  )
}

export function GenerateBracketForm({
  tournamentId,
  categoryId,
  groups,
  teams,
  thirdPlaceEnabled,
}: {
  tournamentId: string
  categoryId: string
  groups: TournamentGroup[]
  /** Zaakceptowane drużyny kategorii — do par ze stałą drużyną. */
  teams: TournamentTeam[]
  thirdPlaceEnabled: boolean
}) {
  const defaultSize = defaultPairCount(groups, teams)
  const [pairCount, setPairCount] = useState(defaultSize)
  const [pairs, setPairs] = useState<Pair[]>(() => suggestPairs(groups, teams, defaultSize))
  const [error, setError] = useState<string | null>(null)
  const generate = useGenerateBracket(tournamentId)

  const filled = pairs.every((p) => p.home && p.away)
  // Wolnych losów może być kilka, ale nie po obu stronach jednej pary — nie byłoby komu awansować.
  const keys = pairs
    .flatMap((p) => [encode(p.home), encode(p.away)])
    .filter((key) => key && key !== BYE)
  const duplicates = keys.length !== new Set(keys).size
  const doubleBye = pairs.some((p) => p.home?.kind === "bye" && p.away?.kind === "bye")
  const hasBye = pairs.some((p) => p.home?.kind === "bye" || p.away?.kind === "bye")

  function setSlot(index: number, side: "home" | "away", slot: BracketSlotInput | null) {
    setPairs((current) => current.map((p, i) => (i === index ? { ...p, [side]: slot } : p)))
  }

  function submit() {
    setError(null)
    generate.mutate(
      { categoryId, pairs: pairs.map((p) => ({ home: p.home!, away: p.away! })) },
      {
        onSuccess: () => toast.success("Drabinka utworzona"),
        onError: (e) => {
          if (isApiError(e)) setError(e.fieldErrors[0]?.message ?? e.detail ?? null)
        },
      },
    )
  }

  return (
    <div className="space-y-5">
      <div className="space-y-2">
        <p className="text-sm font-medium">Pierwsza runda</p>
        <div className="flex flex-wrap gap-2" role="group" aria-label="Wielkość drabinki">
          {SIZES.map((size) => (
            <Button
              key={size.pairs}
              type="button"
              size="sm"
              variant={pairCount === size.pairs ? "default" : "outline"}
              aria-pressed={pairCount === size.pairs}
              onClick={() => {
                setPairCount(size.pairs)
                setPairs(suggestPairs(groups, teams, size.pairs))
              }}
            >
              {size.label}
            </Button>
          ))}
        </div>
      </div>

      <ol className="space-y-3">
        {pairs.map((pair, i) => (
          <li key={i} className="grid grid-cols-[1.5rem_1fr_auto_1fr] items-center gap-2">
            <span className="text-muted-foreground font-mono text-sm tabular-nums">{i + 1}.</span>
            <SlotSelect
              label={`Para ${i + 1}: gospodarz`}
              value={pair.home}
              onChange={(slot) => setSlot(i, "home", slot)}
              groups={groups}
              teams={teams}
            />
            <span className="text-muted-foreground text-sm">vs</span>
            <SlotSelect
              label={`Para ${i + 1}: gość`}
              value={pair.away}
              onChange={(slot) => setSlot(i, "away", slot)}
              groups={groups}
              teams={teams}
            />
          </li>
        ))}
      </ol>

      <FieldDescription>
        Miejsca z grup wypełnią się same, gdy grupa rozegra wszystkie mecze. Mecze pucharowe dostaną
        propozycję terminu po fazie grupowej — możesz je przesunąć w terminarzu.
        {thirdPlaceEnabled && " Mecz o 3. miejsce zostanie dodany (ustawienia sportowe)."}
        {hasBye && " Drużyna z wolnym losem przechodzi do kolejnej rundy bez meczu."}
      </FieldDescription>

      {duplicates && (
        <p className="text-destructive text-sm">To samo miejsce albo drużyna występuje dwa razy.</p>
      )}
      {doubleBye && (
        <p className="text-destructive text-sm">Para nie może mieć wolnego losu po obu stronach.</p>
      )}
      {error && <p className="text-destructive text-sm">{error}</p>}

      <Button onClick={submit} disabled={!filled || duplicates || doubleBye || generate.isPending}>
        <GitBranch />
        {generate.isPending ? "Tworzenie…" : "Utwórz drabinkę"}
      </Button>
    </div>
  )
}
