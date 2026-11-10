"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { type ReactNode, useState } from "react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
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
import { FieldError, FieldGroup } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { isApiError } from "@/lib/api/errors"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { plural } from "@/lib/format"
import type { Team, TeamLabel } from "@/types/teams"

import { useSaveTeamLabel, useSetTeamLabelMembers } from "../api"
import { matchesQuery } from "../team-labels"

// Limity 1:1 z CreateTeamLabelCommandValidator / RenameTeamLabelCommandValidator.
const schema = z.object({
  name: z.string().trim().min(1, "Podaj nazwę etykiety").max(60, "Maksymalnie 60 znaków"),
})

type LabelValues = z.infer<typeof schema>

/** Nowa etykieta albo zmiana nazwy istniejącej. */
export function TeamLabelFormDialog({
  organizationId,
  label,
  trigger,
}: {
  organizationId: string
  label?: TeamLabel
  trigger: ReactNode
}) {
  const [open, setOpen] = useState(false)
  const save = useSaveTeamLabel(organizationId)
  const form = useForm<LabelValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: label?.name ?? "" },
  })

  function submit(values: LabelValues) {
    save.mutate(
      { labelId: label?.id, name: values.name },
      {
        onSuccess: () => {
          toast.success(label ? "Etykieta zapisana" : "Etykieta dodana")
          setOpen(false)
        },
        onError: (error) => {
          if (isApiError(error) && error.status === 409) {
            form.setError("name", { type: "server", message: error.message })
            return
          }
          applyFieldErrors(error, form)
        },
      },
    )
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) form.reset({ name: label?.name ?? "" })
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{label ? "Zmień nazwę etykiety" : "Nowa etykieta"}</DialogTitle>
          <DialogDescription>Na przykład rocznik, klub albo poziom drużyn.</DialogDescription>
        </DialogHeader>
        <form id="team-label-form" onSubmit={form.handleSubmit(submit)} noValidate>
          <FieldGroup>
            <TextField
              id="team-label-name"
              label="Nazwa"
              placeholder="np. Rocznik 2015"
              error={form.formState.errors.name}
              {...form.register("name")}
            />
            <FieldError errors={[form.formState.errors.root]} />
          </FieldGroup>
        </form>
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button type="submit" form="team-label-form" disabled={save.isPending}>
            {save.isPending ? "Zapisywanie…" : "Zapisz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** Zaznaczanie drużyn z etykietą: wiele naraz, z wyszukiwarką. */
export function TeamLabelMembersDialog({
  organizationId,
  label,
  teams,
  trigger,
}: {
  organizationId: string
  label: TeamLabel
  teams: Team[]
  trigger: ReactNode
}) {
  const [open, setOpen] = useState(false)
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [query, setQuery] = useState("")
  const setMembers = useSetTeamLabelMembers(organizationId)
  const visible = teams.filter((t) => matchesQuery(t, query))
  const allVisibleSelected = visible.length > 0 && visible.every((t) => selected.has(t.id))

  function toggle(teamId: string, checked: boolean) {
    setSelected((current) => {
      const next = new Set(current)
      if (checked) next.add(teamId)
      else next.delete(teamId)
      return next
    })
  }

  function submit() {
    setMembers.mutate(
      { labelId: label.id, teamIds: [...selected] },
      {
        onSuccess: () => {
          toast.success("Etykieta zapisana")
          setOpen(false)
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
          setSelected(new Set(teams.filter((t) => t.labelIds.includes(label.id)).map((t) => t.id)))
          setQuery("")
        }
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Drużyny z etykietą {label.name}</DialogTitle>
          <DialogDescription>
            Zaznacz drużyny. Zaznaczono {plural(selected.size, ["drużynę", "drużyny", "drużyn"])}.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <Input
              type="search"
              placeholder="Szukaj drużyny…"
              aria-label="Szukaj drużyny"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
            {visible.length > 1 && (
              <Button
                type="button"
                variant="link"
                size="sm"
                className="h-auto shrink-0 px-0"
                onClick={() =>
                  setSelected((current) => {
                    const next = new Set(current)
                    for (const t of visible) {
                      if (allVisibleSelected) next.delete(t.id)
                      else next.add(t.id)
                    }
                    return next
                  })
                }
              >
                {allVisibleSelected ? "Odznacz wszystkie" : "Zaznacz wszystkie"}
              </Button>
            )}
          </div>
          <ul className="max-h-72 divide-y overflow-y-auto rounded-md border">
            {visible.length === 0 && (
              <li className="text-muted-foreground px-3 py-2 text-sm">
                {teams.length === 0
                  ? "W bazie nie ma jeszcze drużyn."
                  : `Brak drużyn pasujących do „${query}”.`}
              </li>
            )}
            {visible.map((team) => {
              const inputId = `label-member-${team.id}`
              return (
                <li key={team.id} className="flex items-center gap-3 px-3 py-2">
                  <Checkbox
                    id={inputId}
                    checked={selected.has(team.id)}
                    onCheckedChange={(checked) => toggle(team.id, checked === true)}
                  />
                  <label
                    htmlFor={inputId}
                    className="min-w-0 flex-1 cursor-pointer truncate text-sm"
                  >
                    {team.name}
                  </label>
                </li>
              )
            })}
          </ul>
        </div>
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button onClick={submit} disabled={setMembers.isPending}>
            {setMembers.isPending ? "Zapisywanie…" : "Zapisz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
