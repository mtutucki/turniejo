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

type Pair = { home: BracketSlotInput | null; away: BracketSlotInput | null }

const SIZES = [
  { pairs: 1, label: "Finał (2 drużyny)" },
  { pairs: 2, label: "Półfinały (4 drużyny)" },
  { pairs: 4, label: "Ćwierćfinały (8 drużyn)" },
  { pairs: 8, label: "1/8 finału (16 drużyn)" },
]

// Wartość w Select: "g:<groupId>:<pozycja>" albo "t:<tournamentTeamId>".
const encode = (slot: BracketSlotInput | null) =>
  !slot
    ? ""
    : slot.kind === "group"
      ? `g:${slot.groupId}:${slot.position}`
      : `t:${slot.tournamentTeamId}`
const decode = (value: string): BracketSlotInput | null => {
  const [kind, id, position] = value.split(":")
  if (kind === "g" && id && position)
    return { kind: "group", groupId: id, position: Number(position) }
  if (kind === "t" && id) return { kind: "team", tournamentTeamId: id }
  return null
}

/**
 * Propozycja par "na krzyż": przy 2 grupach A1–B2, B1–A2; przy jednej grupie 1–4, 2–3; w ogólności
 * zwycięzcy grup z drugimi miejscami sąsiednich grup. Organizator może każdą parę zmienić.
 */
function suggestPairs(
  groups: TournamentGroup[],
  teams: TournamentTeam[],
  pairCount: number,
): Pair[] {
  // Puchar bez grup: drużyny w kolejności rozstawienia (numer rozstawienia, potem nazwa) —
  // 1. z ostatnią, 2. z przedostatnią. Brakujące miejsca zostają do wybrania.
  if (groups.length === 0) {
    const seeded = [...teams].sort(
      (a, b) =>
        (a.seedNumber ?? 999) - (b.seedNumber ?? 999) || a.teamName.localeCompare(b.teamName, "pl"),
    )
    const slot = (i: number): BracketSlotInput | null => {
      const team = seeded[i]
      return team ? { kind: "team", tournamentTeamId: team.id } : null
    }
    return Array.from({ length: pairCount }, (_, i) => ({
      home: slot(i),
      away: slot(pairCount * 2 - 1 - i),
    }))
  }
  const slots: BracketSlotInput[] = []
  if (groups.length === 1) {
    for (let i = 1; i <= pairCount * 2; i++)
      slots.push({ kind: "group", groupId: groups[0]!.id, position: i })
    return Array.from({ length: pairCount }, (_, i) => ({
      home: slots[i]!,
      away: slots[pairCount * 2 - 1 - i]!,
    }))
  }
  const pairs: Pair[] = []
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
  // Z grupami: tyle par, ile grup (max ćwierćfinały). Bez grup: największa drabinka, którą da się
  // w pełni obsadzić drużynami (np. 6 drużyn → półfinały).
  const defaultSize =
    groups.length > 0
      ? Math.min(4, Math.max(1, groups.length))
      : Math.max(1, Math.min(8, 2 ** Math.floor(Math.log2(Math.max(2, teams.length))) / 2))
  const [pairCount, setPairCount] = useState(defaultSize)
  const [pairs, setPairs] = useState<Pair[]>(() => suggestPairs(groups, teams, defaultSize))
  const [error, setError] = useState<string | null>(null)
  const generate = useGenerateBracket(tournamentId)

  const filled = pairs.every((p) => p.home && p.away)
  const keys = pairs.flatMap((p) => [encode(p.home), encode(p.away)]).filter(Boolean)
  const duplicates = keys.length !== new Set(keys).size

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
      </FieldDescription>

      {duplicates && (
        <p className="text-destructive text-sm">To samo miejsce albo drużyna występuje dwa razy.</p>
      )}
      {error && <p className="text-destructive text-sm">{error}</p>}

      <Button onClick={submit} disabled={!filled || duplicates || generate.isPending}>
        <GitBranch />
        {generate.isPending ? "Tworzenie…" : "Utwórz drabinkę"}
      </Button>
    </div>
  )
}
