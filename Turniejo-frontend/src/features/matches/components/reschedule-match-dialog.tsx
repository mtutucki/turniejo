"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import Link from "next/link"
import { type ReactNode, useState } from "react"
import { Controller, useForm, useWatch } from "react-hook-form"
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
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { useTournament } from "@/features/tournaments/api"
import { isApiError } from "@/lib/api/errors"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { formatDateTime, fromDateTimeLocal, toDateTimeLocal } from "@/lib/format"
import type { Match, Pitch } from "@/types/matches"

import { useUpdateMatchSchedule, useUpdatePlannedMatchSchedule } from "../api"

const NO_PITCH = "none"

const schema = z.object({
  scheduledAt: z.string().min(1, "Podaj datę i godzinę"),
  pitchId: z.string(),
  shiftFollowing: z.boolean(),
})

type RescheduleValues = z.infer<typeof schema>

/** Mecz pucharu, który jeszcze nie powstał (węzeł drabinki z planowanym terminem). */
export type PlannedScheduleTarget = { nodeId: string; scheduledAt: string; pitchId: string | null }

type RescheduleMatchDialogProps = {
  tournamentId: string
  /** Istniejący mecz albo `planned`: mecz, który czeka na wyniki wcześniejszych. */
  match?: Match
  planned?: PlannedScheduleTarget
  /** "Gospodarz – Gość" do nagłówka. */
  title: string
  pitches: Pitch[]
  trigger: ReactNode
}

export function RescheduleMatchDialog({
  tournamentId,
  match,
  planned,
  title,
  pitches,
  trigger,
}: RescheduleMatchDialogProps) {
  const [open, setOpen] = useState(false)
  // Backend odpowiedział, że przesunięcie rusza też mecze na innych boiskach - czekamy na potwierdzenie.
  const [askOtherPitches, setAskOtherPitches] = useState(false)
  const updateMatch = useUpdateMatchSchedule(tournamentId)
  const updatePlanned = useUpdatePlannedMatchSchedule(tournamentId)
  const isPending = updateMatch.isPending || updatePlanned.isPending
  // Turniej jest już w cache (layout turnieju) — bierzemy z niego daty do podpowiedzi.
  const tournament = useTournament(tournamentId).data
  const current = match ?? planned
  const defaults: RescheduleValues = {
    scheduledAt: current ? toDateTimeLocal(current.scheduledAt) : "",
    pitchId: current?.pitchId ?? NO_PITCH,
    shiftFollowing: true,
  }
  const form = useForm<RescheduleValues>({ resolver: zodResolver(schema), defaultValues: defaults })
  const { errors } = form.formState
  const selectedPitch = useWatch({ control: form.control, name: "pitchId" })
  // Opóźnienie na boisku przesuwa kolejne mecze na tym samym boisku, więc opcja ma sens tylko bez zmiany boiska.
  const canShiftFollowing = !!match?.pitchId && selectedPitch === match.pitchId

  function submit(values: RescheduleValues, shiftOtherPitches = false) {
    const scheduledAt = fromDateTimeLocal(values.scheduledAt)
    const pitchId = values.pitchId === NO_PITCH ? null : values.pitchId
    const callbacks = {
      onSuccess: () => {
        toast.success("Termin meczu zapisany")
        setOpen(false)
      },
      onError: (error: unknown) => {
        if (isApiError(error) && error.status === 409) {
          if (error.code === "Conflict.OtherPitches") setAskOtherPitches(true)
          else form.setError("root", { type: "server", message: error.message })
          return
        }
        applyFieldErrors(error, form)
      },
    }
    if (planned) {
      // Planowany mecz musi mieć boisko — generator i przesuwanie terminów liczą na nim kolizje.
      if (!pitchId) return form.setError("pitchId", { message: "Wybierz boisko" })
      updatePlanned.mutate({ nodeId: planned.nodeId, scheduledAt, pitchId }, callbacks)
    } else if (match) {
      const shiftFollowing = canShiftFollowing && values.shiftFollowing
      updateMatch.mutate(
        { matchId: match.id, scheduledAt, pitchId, shiftFollowing, shiftOtherPitches },
        callbacks,
      )
    }
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        setAskOtherPitches(false)
        if (next) form.reset(defaults)
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Zmień termin</DialogTitle>
          <DialogDescription>{title}</DialogDescription>
        </DialogHeader>
        <form
          id="reschedule-form"
          onSubmit={form.handleSubmit((values) => submit(values))}
          onChange={() => setAskOtherPitches(false)}
          noValidate
        >
          <FieldGroup>
            <TextField
              id="match-scheduled-at"
              label="Data i godzina (czas polski)"
              type="datetime-local"
              className="font-mono tabular-nums"
              error={errors.scheduledAt}
              {...form.register("scheduledAt")}
            />
            {tournament && (
              <p className="text-muted-foreground -mt-5 text-xs">
                Turniej trwa {formatDateTime(tournament.startAt)} –{" "}
                {formatDateTime(tournament.endAt)}.{" "}
                <Link
                  href={`/tournaments/${tournamentId}/settings#details`}
                  className="text-foreground underline"
                >
                  Zmień daty turnieju
                </Link>
              </p>
            )}
            <Controller
              control={form.control}
              name="pitchId"
              render={({ field }) => (
                <Field data-invalid={!!errors.pitchId}>
                  <FieldLabel htmlFor="match-pitch">Boisko</FieldLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="match-pitch" className="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {!planned && <SelectItem value={NO_PITCH}>Bez boiska</SelectItem>}
                      {/* Nieaktywnych boisk nie da się wybrać — poza obecnym boiskiem meczu. */}
                      {pitches
                        .filter((pitch) => pitch.isActive || pitch.id === current?.pitchId)
                        .map((pitch) => (
                          <SelectItem key={pitch.id} value={pitch.id}>
                            {pitch.name}
                            {!pitch.isActive && " (nieaktywne)"}
                          </SelectItem>
                        ))}
                    </SelectContent>
                  </Select>
                  <FieldError errors={[errors.pitchId]} />
                </Field>
              )}
            />
            {canShiftFollowing && (
              <Controller
                control={form.control}
                name="shiftFollowing"
                render={({ field }) => (
                  <Field orientation="horizontal">
                    <Checkbox
                      id="match-shift-following"
                      checked={field.value}
                      onCheckedChange={(checked) => field.onChange(checked === true)}
                    />
                    <FieldLabel htmlFor="match-shift-following" className="font-normal">
                      Przesuń też kolejne mecze na tym boisku
                    </FieldLabel>
                  </Field>
                )}
              />
            )}
            <FieldError errors={[errors.root]} />
          </FieldGroup>
        </form>
        {askOtherPitches ? (
          <div role="alert" className="flex flex-col gap-3">
            <p className="text-sm">
              Przesunięcie zmieni też godziny meczów na innych boiskach, bo grają w nich te same
              drużyny albo sędziowie. Przesunąć mimo to?
            </p>
            <DialogFooter>
              <Button variant="ghost" onClick={() => setAskOtherPitches(false)}>
                Wróć
              </Button>
              <Button
                disabled={isPending}
                onClick={form.handleSubmit((values) => submit(values, true))}
              >
                {isPending ? "Zapisywanie…" : "Przesuń mimo to"}
              </Button>
            </DialogFooter>
          </div>
        ) : (
          <DialogFooter>
            <Button variant="ghost" onClick={() => setOpen(false)}>
              Anuluj
            </Button>
            <Button type="submit" form="reschedule-form" disabled={isPending}>
              {isPending ? "Zapisywanie…" : "Zapisz"}
            </Button>
          </DialogFooter>
        )}
      </DialogContent>
    </Dialog>
  )
}
