"use client"

import { Check, Search } from "lucide-react"
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
import { Field, FieldDescription, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { useTournamentSettings } from "@/features/tournaments/api"
import { isApiError } from "@/lib/api/errors"
import { formatTime, plural } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { Match } from "@/types/matches"
import type { TournamentMember } from "@/types/teams"

import { useAssignPlannedReferee, useAssignReferee, useSchedule } from "../api"

const NO_REFEREE = "none"
/** Od tylu sędziów pokazujemy wyszukiwarkę, mniejszą listę widać od razu w całości. */
const SEARCH_FROM = 6

export const memberName = (m: TournamentMember) => `${m.firstName} ${m.lastName}`.trim() || m.email

const collator = new Intl.Collator("pl", { sensitivity: "base" })
// Po nazwisku, jak na liście obecności. Bez imienia i nazwiska zostaje e-mail.
const sortKey = (m: TournamentMember) => `${m.lastName} ${m.firstName}`.trim() || m.email

// Wyszukiwanie bez polskich znaków: "lukasz" znajduje "Łukasz".
const normalize = (text: string) =>
  text
    .toLowerCase()
    .replace(/ł/g, "l")
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")

const matchCountLabel = (count: number) =>
  count > 0 ? plural(count, ["mecz", "mecze", "meczów"]) : "Bez meczów"

type RefereeOption = {
  member: TournamentMember
  /** Mecze tego sędziego w turnieju (bez odwołanych i bez tego meczu). */
  matchCount: number
  /** Inny mecz tego sędziego nachodzi na ten, więc backend odrzuci przypisanie. */
  busyWith: Match | null
}

/** Mecz pucharu, który jeszcze nie powstał (węzeł drabinki z planowanym terminem). */
export type PlannedRefereeTarget = { nodeId: string; scheduledAt: string; refereeId: string | null }

type AssignRefereeDialogProps = {
  tournamentId: string
  /** Istniejący mecz albo `planned`: mecz, który czeka na wyniki wcześniejszych. */
  match?: Match
  planned?: PlannedRefereeTarget
  title: string
  /** Członkowie turnieju z rolą Referee (dołączyli kodem sędziego). */
  referees: TournamentMember[]
  refereeCode: string | null
  trigger: ReactNode
}

export function AssignRefereeDialog({
  tournamentId,
  match,
  planned,
  title,
  referees,
  refereeCode,
  trigger,
}: AssignRefereeDialogProps) {
  const currentRefereeId = match?.refereeId ?? planned?.refereeId ?? null
  const scheduledAt = match?.scheduledAt ?? planned?.scheduledAt ?? ""
  const [open, setOpen] = useState(false)
  const [refereeId, setRefereeId] = useState(currentRefereeId ?? NO_REFEREE)
  const [query, setQuery] = useState("")
  const [error, setError] = useState<string | null>(null)
  const assignMatch = useAssignReferee(tournamentId)
  const assignPlanned = useAssignPlannedReferee(tournamentId)
  const isPending = assignMatch.isPending || assignPlanned.isPending
  // Te same zapytania co terminarz i ustawienia, więc dane są już w cache.
  const schedule = useSchedule(tournamentId)
  const settings = useTournamentSettings(tournamentId)

  const durationMs = Math.max(settings.data?.matchDurationMinutes ?? 1, 1) * 60_000
  const start = Date.parse(scheduledAt)
  // Ta sama reguła co ScheduleConflictChecker w backendzie: mecze nachodzą na siebie w czasie.
  const overlaps = (other: Match) => {
    const otherStart = Date.parse(other.scheduledAt)
    return otherStart < start + durationMs && start < otherStart + durationMs
  }
  const others = (schedule.data ?? []).filter(
    (m) => m.id !== match?.id && m.status !== "Cancelled" && m.refereeId,
  )

  const options: RefereeOption[] = referees
    .map((member) => {
      const own = others.filter((m) => m.refereeId === member.userId)
      return { member, matchCount: own.length, busyWith: own.find(overlaps) ?? null }
    })
    .sort((a, b) => collator.compare(sortKey(a.member), sortKey(b.member)))

  const needle = normalize(query.trim())
  const matches = (o: RefereeOption) =>
    !needle ||
    normalize(`${o.member.firstName} ${o.member.lastName} ${o.member.email}`).includes(needle)
  // Obecny sędzia stoi na górze, zaraz pod "Dowolny sędzia", żeby nie szukać go wśród 50 osób.
  const current = needle ? undefined : options.find((o) => o.member.userId === currentRefereeId)
  const listed = options.filter((o) => o !== current && matches(o))
  // Wolni najpierw, zajęci o tej godzinie na końcu: i tak nie da się ich przypisać.
  const free = listed.filter((o) => !o.busyWith)
  const busy = listed.filter((o) => o.busyWith)

  function save() {
    setError(null)
    const chosen = refereeId === NO_REFEREE ? null : refereeId
    const callbacks = {
      onSuccess: () => {
        toast.success(
          refereeId === NO_REFEREE ? "Mecz może wziąć dowolny sędzia" : "Sędzia przypisany",
        )
        setOpen(false)
      },
      onError: (e: unknown) => {
        if (isApiError(e) && e.fieldErrors.length) setError(e.fieldErrors[0]!.message)
      },
    }
    if (planned) assignPlanned.mutate({ nodeId: planned.nodeId, refereeId: chosen }, callbacks)
    else if (match) assignMatch.mutate({ matchId: match.id, refereeId: chosen }, callbacks)
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setRefereeId(currentRefereeId ?? NO_REFEREE)
          setQuery("")
          setError(null)
        }
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Sędzia meczu</DialogTitle>
          <DialogDescription>{title}</DialogDescription>
        </DialogHeader>
        <Field data-invalid={!!error}>
          <FieldLabel id="match-referee-label">Sędzia</FieldLabel>
          {referees.length >= SEARCH_FROM && (
            <div className="relative">
              <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" />
              <Input
                type="search"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Szukaj po imieniu, nazwisku albo e-mailu"
                aria-label="Szukaj sędziego"
                className="pl-9"
              />
            </div>
          )}
          <div
            role="radiogroup"
            aria-labelledby="match-referee-label"
            className="max-h-72 space-y-0.5 overflow-y-auto rounded-md border p-1"
          >
            {!needle && (
              <RefereeRow
                selected={refereeId === NO_REFEREE}
                onSelect={() => setRefereeId(NO_REFEREE)}
                name="Dowolny sędzia"
                detail="Mecz może wziąć każdy sędzia turnieju."
              />
            )}
            {current && (
              <RefereeRow
                selected={refereeId === current.member.userId}
                onSelect={() => setRefereeId(current.member.userId)}
                name={memberName(current.member)}
                detail={current.member.email}
                meta="Obecny sędzia"
              />
            )}
            {free.map((o) => (
              <RefereeRow
                key={o.member.userId}
                selected={refereeId === o.member.userId}
                onSelect={() => setRefereeId(o.member.userId)}
                name={memberName(o.member)}
                detail={o.member.email}
                meta={matchCountLabel(o.matchCount)}
              />
            ))}
            {busy.length > 0 && (
              <p className="text-muted-foreground px-2 pt-2 pb-1 text-xs font-medium">
                Zajęci o tej godzinie
              </p>
            )}
            {busy.map((o) => (
              <RefereeRow
                key={o.member.userId}
                selected={refereeId === o.member.userId}
                disabled
                name={memberName(o.member)}
                detail={o.member.email}
                meta={`Ma mecz o ${formatTime(o.busyWith!.scheduledAt)}`}
              />
            ))}
            {needle && free.length === 0 && busy.length === 0 && (
              <p className="text-muted-foreground px-2 py-3 text-center text-sm">
                Nikt nie pasuje do „{query.trim()}”.
              </p>
            )}
          </div>
          <FieldDescription>
            Przypisany mecz widzi tylko wybrany sędzia.{" "}
            {referees.length === 0
              ? "Nikt jeszcze nie dołączył jako sędzia. "
              : "Na liście są osoby, które dołączyły do turnieju jako sędziowie. "}
            {refereeCode && (
              <>
                Kod sędziego: <span className="text-foreground font-mono">{refereeCode}</span>
              </>
            )}
          </FieldDescription>
          {error && <p className="text-destructive text-sm">{error}</p>}
        </Field>
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button onClick={save} disabled={isPending}>
            {isPending ? "Zapisywanie…" : "Zapisz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function RefereeRow({
  selected,
  onSelect,
  disabled = false,
  name,
  detail,
  meta,
}: {
  selected: boolean
  onSelect?: () => void
  disabled?: boolean
  name: string
  detail: string
  meta?: string
}) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={selected}
      disabled={disabled}
      onClick={onSelect}
      className={cn(
        "flex w-full items-center gap-3 rounded-sm px-2 py-2 text-left text-sm transition-colors",
        selected ? "bg-primary/15" : "hover:bg-accent",
        "disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:bg-transparent",
      )}
    >
      <span
        className={cn(
          "flex size-4 shrink-0 items-center justify-center rounded-full border",
          selected ? "border-primary bg-primary text-primary-foreground" : "border-input",
        )}
        aria-hidden
      >
        {selected && <Check className="size-3" />}
      </span>
      <span className="min-w-0 flex-1">
        <span className="block truncate font-medium">{name}</span>
        <span className="text-muted-foreground block truncate text-xs">{detail}</span>
      </span>
      {meta && <span className="text-muted-foreground shrink-0 text-xs">{meta}</span>}
    </button>
  )
}
