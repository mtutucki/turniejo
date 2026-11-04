"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import Link from "next/link"
import { type ReactNode, useState } from "react"
import { Controller, useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
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
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { useTournament } from "@/features/tournaments/api"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { formatDateTime, fromDateTimeLocal, toDateTimeLocal } from "@/lib/format"
import type { Match, Pitch } from "@/types/matches"

import { useUpdateMatchSchedule } from "../api"

const NO_PITCH = "none"

const schema = z.object({
  scheduledAt: z.string().min(1, "Podaj datę i godzinę"),
  pitchId: z.string(),
})

type RescheduleValues = z.infer<typeof schema>

type RescheduleMatchDialogProps = {
  tournamentId: string
  match: Match
  /** "Gospodarz – Gość" do nagłówka. */
  title: string
  pitches: Pitch[]
  trigger: ReactNode
}

export function RescheduleMatchDialog({
  tournamentId,
  match,
  title,
  pitches,
  trigger,
}: RescheduleMatchDialogProps) {
  const [open, setOpen] = useState(false)
  const update = useUpdateMatchSchedule(tournamentId)
  // Turniej jest już w cache (layout turnieju) — bierzemy z niego daty do podpowiedzi.
  const tournament = useTournament(tournamentId).data
  const defaults: RescheduleValues = {
    scheduledAt: toDateTimeLocal(match.scheduledAt),
    pitchId: match.pitchId ?? NO_PITCH,
  }
  const form = useForm<RescheduleValues>({ resolver: zodResolver(schema), defaultValues: defaults })
  const { errors } = form.formState

  function submit(values: RescheduleValues) {
    update.mutate(
      {
        matchId: match.id,
        scheduledAt: fromDateTimeLocal(values.scheduledAt),
        pitchId: values.pitchId === NO_PITCH ? null : values.pitchId,
      },
      {
        onSuccess: () => {
          toast.success("Termin meczu zapisany")
          setOpen(false)
        },
        onError: (error) => applyFieldErrors(error, form),
      },
    )
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) form.reset(defaults)
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Zmień termin</DialogTitle>
          <DialogDescription>{title}</DialogDescription>
        </DialogHeader>
        <form id="reschedule-form" onSubmit={form.handleSubmit(submit)} noValidate>
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
                      <SelectItem value={NO_PITCH}>Bez boiska</SelectItem>
                      {/* Nieaktywnych boisk nie da się wybrać — poza obecnym boiskiem meczu. */}
                      {pitches
                        .filter((pitch) => pitch.isActive || pitch.id === match.pitchId)
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
            <FieldError errors={[errors.root]} />
          </FieldGroup>
        </form>
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button type="submit" form="reschedule-form" disabled={update.isPending}>
            {update.isPending ? "Zapisywanie…" : "Zapisz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
