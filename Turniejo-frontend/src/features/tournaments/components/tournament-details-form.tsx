"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { useForm } from "react-hook-form"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
import { Button } from "@/components/ui/button"
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Textarea } from "@/components/ui/textarea"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { fromDateTimeLocal, toDateTimeLocal } from "@/lib/format"
import type { Tournament, TournamentDetailsInput } from "@/types/tournaments"

// Limity 1:1 z CreateTournamentCommandValidator / UpdateTournamentDetailsValidator.
const schema = z
  .object({
    name: z.string().trim().min(1, "Podaj nazwę turnieju").max(200, "Maksymalnie 200 znaków"),
    description: z.string().trim().max(500, "Maksymalnie 500 znaków"),
    startAt: z.string().min(1, "Podaj datę rozpoczęcia"),
    endAt: z.string().min(1, "Podaj datę zakończenia"),
    teamLimit: z
      .number({ error: "Podaj liczbę" })
      .int("Podaj liczbę całkowitą")
      // Backend porównuje liczbę zaakceptowanych drużyn z limitem przy każdym zgłoszeniu —
      // limit 0 oznaczałby turniej, do którego nie da się zgłosić żadnej drużyny.
      .min(1, "Limit musi wynosić co najmniej 1"),
  })
  .refine((v) => !v.startAt || !v.endAt || new Date(v.endAt) > new Date(v.startAt), {
    path: ["endAt"],
    message: "Zakończenie musi być po rozpoczęciu",
  })

type DetailsValues = z.infer<typeof schema>

type TournamentDetailsFormProps = {
  tournament?: Tournament
  submitLabel: string
  pendingLabel: string
  disabled?: boolean
  onSubmit: (input: TournamentDetailsInput) => Promise<unknown>
}

export function TournamentDetailsForm({
  tournament,
  submitLabel,
  pendingLabel,
  disabled = false,
  onSubmit,
}: TournamentDetailsFormProps) {
  const form = useForm<DetailsValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: tournament?.name ?? "",
      description: tournament?.description ?? "",
      startAt: tournament ? toDateTimeLocal(tournament.startAt) : "",
      endAt: tournament ? toDateTimeLocal(tournament.endAt) : "",
      teamLimit: tournament?.teamLimit ?? 16,
    },
  })
  const { errors, isSubmitting } = form.formState

  async function submit(values: DetailsValues) {
    try {
      await onSubmit({
        name: values.name,
        description: values.description || null,
        startAt: fromDateTimeLocal(values.startAt),
        endAt: fromDateTimeLocal(values.endAt),
        teamLimit: values.teamLimit,
      })
      form.reset(values)
    } catch (error) {
      applyFieldErrors(error, form)
    }
  }

  return (
    <form onSubmit={form.handleSubmit(submit)} noValidate>
      <fieldset disabled={disabled || isSubmitting} className="contents">
        <FieldGroup>
          <TextField id="name" label="Nazwa" error={errors.name} {...form.register("name")} />
          <Field data-invalid={!!errors.description}>
            <FieldLabel htmlFor="description">Opis (opcjonalnie)</FieldLabel>
            <Textarea
              id="description"
              rows={3}
              aria-invalid={!!errors.description}
              {...form.register("description")}
            />
            <FieldError errors={[errors.description]} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              id="startAt"
              label="Rozpoczęcie"
              type="datetime-local"
              error={errors.startAt}
              {...form.register("startAt")}
            />
            <TextField
              id="endAt"
              label="Zakończenie"
              type="datetime-local"
              error={errors.endAt}
              {...form.register("endAt")}
            />
          </div>
          <TextField
            id="teamLimit"
            label="Maksymalna liczba drużyn"
            type="number"
            min={1}
            inputMode="numeric"
            className="font-mono tabular-nums sm:max-w-40"
            error={errors.teamLimit}
            {...form.register("teamLimit", { valueAsNumber: true })}
          />
          <FieldError errors={[errors.root]} />
          <div>
            <Button
              type="submit"
              disabled={disabled || isSubmitting || (!!tournament && !form.formState.isDirty)}
            >
              {isSubmitting ? pendingLabel : submitLabel}
            </Button>
          </div>
        </FieldGroup>
      </fieldset>
    </form>
  )
}
