"use client"

import { CircleAlert } from "lucide-react"
import Link from "next/link"
import { type ReactNode, useState } from "react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { isApiError } from "@/lib/api/errors"
import { plural } from "@/lib/format"
import type { TournamentTeam } from "@/types/teams"
import type { TournamentCategory } from "@/types/tournaments"

import { useAddTeamToTournament, useOrganizationTeams } from "../api"

type AddTeamDialogProps = {
  tournamentId: string
  organizationId: string
  categories: TournamentCategory[]
  registered: TournamentTeam[]
  /** Miejsca w limicie po odjęciu zaakceptowanych i oczekujących. Tyle można zaznaczyć. */
  freeSlots: number
  teamLimit: number
  trigger: ReactNode
}

/**
 * Zgłoszenie drużyn z bazy organizacji do turnieju — można zaznaczyć kilka naraz, każda trafia jako
 * "Oczekuje". Odrzucone/wycofane wcześniej drużyny da się zgłosić ponownie (backend wznawia wpis).
 */
export function AddTeamDialog({
  tournamentId,
  organizationId,
  categories,
  registered,
  freeSlots,
  teamLimit,
  trigger,
}: AddTeamDialogProps) {
  const [open, setOpen] = useState(false)
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [categoryId, setCategoryId] = useState<string>()
  const [error, setError] = useState<string>()
  const [pending, setPending] = useState(false)
  const teams = useOrganizationTeams(organizationId)
  const addTeam = useAddTeamToTournament(tournamentId)

  // Aktywne zgłoszenie (oczekuje/zaakceptowana) blokuje ponowne — odrzucone i wycofane nie.
  const registrationOf = (teamId: string) =>
    registered.find((r) => r.teamId === teamId && r.categoryId === categoryId)
  const available = (teams.data ?? []).filter((team) => {
    const status = registrationOf(team.id)?.registrationStatus
    return !status || status === "Rejected" || status === "Withdrawn"
  })
  const [query, setQuery] = useState("")
  // Przy dużej bazie — filtr po nazwie (zaznaczenia spoza filtra zostają).
  const visible = query.trim()
    ? available.filter((t) => t.name.toLowerCase().includes(query.trim().toLowerCase()))
    : available
  const allSelected = visible.length > 0 && visible.every((t) => selected.has(t.id))
  const limitReached = freeSlots === 0
  // Nie da się zaznaczyć więcej drużyn, niż jest wolnych miejsc.
  const canSelectMore = selected.size < freeSlots
  const fitsAll = selected.size + visible.filter((t) => !selected.has(t.id)).length <= freeSlots

  function reset() {
    setSelected(new Set())
    setCategoryId(categories.length === 1 ? categories[0]!.id : undefined)
    setError(undefined)
    setQuery("")
  }

  function toggle(teamId: string, checked: boolean) {
    setSelected((current) => {
      const next = new Set(current)
      if (checked) next.add(teamId)
      else next.delete(teamId)
      return next
    })
  }

  async function submit() {
    if (!categoryId || selected.size === 0) return
    setPending(true)
    setError(undefined)
    const failed: string[] = []
    const failedIds = new Set<string>()
    let added = 0
    // Po kolei, nie równolegle — limit drużyn w turnieju backend sprawdza przy każdym zgłoszeniu.
    for (const team of available.filter((t) => selected.has(t.id))) {
      try {
        await addTeam.mutateAsync({ teamId: team.id, categoryId })
        added++
      } catch (e) {
        const reason = isApiError(e) ? (e.fieldErrors[0]?.message ?? e.detail) : undefined
        failed.push(reason ? `${team.name}: ${reason}` : team.name)
        failedIds.add(team.id)
      }
    }
    setPending(false)

    if (added > 0) {
      toast.success(
        `Zgłoszono ${plural(added, ["drużynę", "drużyny", "drużyn"])} — czekają na akceptację`,
      )
    }
    if (failed.length === 0) {
      setOpen(false)
      return
    }
    // Zostają zaznaczone tylko te, których nie udało się zgłosić.
    setSelected(failedIds)
    setError(`Nie udało się zgłosić: ${failed.join("; ")}`)
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
          <DialogTitle>Zgłoś drużyny</DialogTitle>
          <DialogDescription>
            Wybierz kategorię i zaznacz drużyny z bazy organizacji — możesz kilka naraz.
          </DialogDescription>
        </DialogHeader>
        <p className={limitReached ? "text-destructive text-sm" : "text-muted-foreground text-sm"}>
          {limitReached
            ? `Wszystkie miejsca w limicie (${teamLimit}) są zajęte przez zaakceptowane lub oczekujące drużyny. Zwiększ limit albo odrzuć któreś zgłoszenie.`
            : `Wolne miejsca: ${freeSlots} z ${teamLimit}. Zaznaczono ${selected.size}.`}
        </p>
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="add-team-category">Kategoria</FieldLabel>
            <Select
              value={categoryId}
              onValueChange={(value) => {
                setCategoryId(value)
                setSelected(new Set())
              }}
            >
              <SelectTrigger id="add-team-category" className="w-full">
                <SelectValue placeholder="Wybierz kategorię" />
              </SelectTrigger>
              <SelectContent>
                {categories.map((c) => (
                  <SelectItem key={c.id} value={c.id}>
                    {c.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>

          {categoryId && (
            <fieldset className="space-y-2">
              <div className="flex items-center justify-between gap-2">
                <legend className="text-sm font-medium">Drużyny</legend>
                {visible.length > 1 && !limitReached && (
                  <Button
                    type="button"
                    variant="link"
                    size="sm"
                    className="h-auto px-0"
                    onClick={() =>
                      setSelected((current) => {
                        const next = new Set(current)
                        if (allSelected || !canSelectMore) {
                          for (const t of visible) next.delete(t.id)
                          return next
                        }
                        // Dobieramy tylko tyle, ile zmieści się w limicie.
                        for (const t of visible) {
                          if (next.size >= freeSlots) break
                          next.add(t.id)
                        }
                        return next
                      })
                    }
                  >
                    {allSelected || !canSelectMore
                      ? "Odznacz wszystkie"
                      : fitsAll
                        ? "Zaznacz wszystkie"
                        : `Zaznacz ${freeSlots - selected.size}`}
                  </Button>
                )}
              </div>
              {available.length > 8 && (
                <Input
                  type="search"
                  placeholder="Szukaj drużyny…"
                  aria-label="Szukaj drużyny"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                />
              )}
              {!limitReached && !canSelectMore && (
                <p
                  role="status"
                  className="border-live/40 bg-live/10 flex items-start gap-2 rounded-md border px-3 py-2 text-sm"
                >
                  <CircleAlert className="text-live mt-0.5 size-4 shrink-0" aria-hidden />
                  <span>
                    Wybrano maksymalną liczbę drużyn ({freeSlots}). Żeby zaznaczyć inną, najpierw
                    odznacz którąś.
                  </span>
                </p>
              )}
              {available.length > 0 ? (
                <ul className="max-h-72 divide-y overflow-y-auto rounded-md border">
                  {visible.length === 0 && (
                    <li className="text-muted-foreground px-3 py-2 text-sm">
                      Brak drużyn pasujących do „{query}”.
                    </li>
                  )}
                  {visible.map((team) => {
                    const previous = registrationOf(team.id)?.registrationStatus
                    const inputId = `add-team-${team.id}`
                    const locked = !selected.has(team.id) && !canSelectMore
                    return (
                      <li key={team.id} className="flex items-center gap-3 px-3 py-2">
                        <Checkbox
                          id={inputId}
                          checked={selected.has(team.id)}
                          disabled={locked}
                          onCheckedChange={(checked) => toggle(team.id, checked === true)}
                        />
                        <label
                          htmlFor={inputId}
                          className={
                            locked
                              ? "text-muted-foreground min-w-0 flex-1 cursor-not-allowed text-sm opacity-60"
                              : "min-w-0 flex-1 cursor-pointer text-sm"
                          }
                        >
                          <span className="block truncate">{team.name}</span>
                          {previous && (
                            <span className="text-muted-foreground text-xs">
                              {previous === "Rejected" ? "Odrzucona" : "Wycofana"} — zgłoś ponownie
                            </span>
                          )}
                        </label>
                      </li>
                    )
                  })}
                </ul>
              ) : (
                teams.data && (
                  <p className="text-muted-foreground text-sm">
                    Wszystkie drużyny z bazy są już zgłoszone do tej kategorii.{" "}
                    <Link
                      href={`/organizations/${organizationId}/teams`}
                      className="text-primary hover:underline"
                    >
                      Dodaj drużynę do bazy
                    </Link>
                  </p>
                )
              )}
            </fieldset>
          )}
          {error && <FieldError>{error}</FieldError>}
        </FieldGroup>
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button
            onClick={submit}
            disabled={!categoryId || selected.size === 0 || pending || limitReached}
          >
            {pending
              ? "Zgłaszanie…"
              : selected.size > 1
                ? `Zgłoś ${plural(selected.size, ["drużynę", "drużyny", "drużyn"])}`
                : "Zgłoś drużynę"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
