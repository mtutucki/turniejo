"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import Link from "next/link"
import type { ComponentProps } from "react"
import type { FieldError as RhfFieldError } from "react-hook-form"
import { useForm, useWatch } from "react-hook-form"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
import { Button } from "@/components/ui/button"
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { formatDateTime, plural, toDateTimeLocal, zonedToIso } from "@/lib/format"
import type { Tournament, TournamentDetailsInput } from "@/types/tournaments"

// Limity 1:1 z CreateTournamentCommandValidator / UpdateTournamentDetailsValidator.
const schema = z
  .object({
    name: z.string().trim().min(1, "Podaj nazwę turnieju").max(200, "Maksymalnie 200 znaków"),
    description: z.string().trim().max(500, "Maksymalnie 500 znaków"),
    startDate: z.string().min(1, "Podaj datę rozpoczęcia"),
    startTime: z.string().min(1, "Podaj godzinę rozpoczęcia"),
    endDate: z.string().min(1, "Podaj datę zakończenia"),
    endTime: z.string().min(1, "Podaj godzinę zakończenia"),
    teamLimit: z
      .number({ error: "Podaj liczbę" })
      .int("Podaj liczbę całkowitą")
      // Backend porównuje liczbę zaakceptowanych drużyn z limitem przy każdym zgłoszeniu —
      // limit 0 oznaczałby turniej, do którego nie da się zgłosić żadnej drużyny.
      .min(1, "Limit musi wynosić co najmniej 1"),
  })
  // "RRRR-MM-DD" i "GG:MM" porównują się leksykograficznie tak samo jak chronologicznie.
  .refine((v) => !v.startDate || !v.endDate || v.endDate >= v.startDate, {
    path: ["endDate"],
    message: "Zakończenie nie może być przed rozpoczęciem",
  })
  .refine(
    (v) => v.startDate !== v.endDate || !v.startTime || !v.endTime || v.endTime > v.startTime,
    { path: ["endTime"], message: "Godzina zakończenia musi być po rozpoczęciu" },
  )

type DetailsValues = z.infer<typeof schema>

type DateTimeFieldProps = {
  id: string
  label: string
  dateProps: ComponentProps<typeof Input>
  timeProps: ComponentProps<typeof Input>
  dateError?: RhfFieldError
  timeError?: RhfFieldError
}

/** Dzień i godzina jako osobne pola — natywny kalendarz i zegar zamiast jednego datetime-local. */
function DateTimeField({
  id,
  label,
  dateProps,
  timeProps,
  dateError,
  timeError,
}: DateTimeFieldProps) {
  return (
    <Field data-invalid={!!dateError || !!timeError}>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <div className="flex gap-2">
        <Input id={id} type="date" aria-invalid={!!dateError} {...dateProps} />
        <Input
          type="time"
          aria-label={`${label} - godzina`}
          aria-invalid={!!timeError}
          className="w-28 shrink-0 font-mono tabular-nums"
          {...timeProps}
        />
      </div>
      <FieldError errors={[dateError ?? timeError]} />
    </Field>
  )
}

type TournamentDetailsFormProps = {
  tournament?: Tournament
  submitLabel: string
  pendingLabel: string
  disabled?: boolean
  /** Zaplanowane mecze (początek i koniec) — ostrzegamy, gdy nowy termin ich nie obejmuje. */
  scheduledMatches?: { startAt: number; endAt: number }[]
  onSubmit: (input: TournamentDetailsInput) => Promise<unknown>
}

export function TournamentDetailsForm({
  tournament,
  submitLabel,
  pendingLabel,
  disabled = false,
  scheduledMatches = [],
  onSubmit,
}: TournamentDetailsFormProps) {
  const start = tournament && toDateTimeLocal(tournament.startAt)
  const end = tournament && toDateTimeLocal(tournament.endAt)
  const form = useForm<DetailsValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: tournament?.name ?? "",
      description: tournament?.description ?? "",
      startDate: start?.slice(0, 10) ?? "",
      startTime: start?.slice(11) ?? "09:00",
      endDate: end?.slice(0, 10) ?? "",
      endTime: end?.slice(11) ?? "17:00",
      teamLimit: tournament?.teamLimit ?? 16,
    },
  })
  const { errors, isSubmitting } = form.formState
  const startDate = useWatch({ control: form.control, name: "startDate" })
  const [startTime, endDate, endTime] = useWatch({
    control: form.control,
    name: ["startTime", "endDate", "endTime"],
  })
  const outsideWarning = outsideTermWarning(
    scheduledMatches,
    startDate && startTime ? zonedToIso(startDate, startTime) : null,
    endDate && endTime ? zonedToIso(endDate, endTime) : null,
  )

  // Zakończenie podąża za rozpoczęciem: puste albo wcześniejsze ustawiamy na ten sam dzień.
  function syncEndDate(nextStart: string) {
    const endDate = form.getValues("endDate")
    if (nextStart && (!endDate || endDate < nextStart))
      form.setValue("endDate", nextStart, { shouldDirty: true, shouldValidate: !!endDate })
  }

  async function submit(values: DetailsValues) {
    try {
      await onSubmit({
        name: values.name,
        description: values.description || null,
        startAt: zonedToIso(values.startDate, values.startTime),
        endAt: zonedToIso(values.endDate, values.endTime),
        teamLimit: values.teamLimit,
        expectedUpdatedAt: tournament?.updatedAt ?? null,
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
            <DateTimeField
              id="startDate"
              label="Rozpoczęcie"
              dateError={errors.startDate}
              timeError={errors.startTime}
              dateProps={form.register("startDate", {
                onChange: (event) => syncEndDate(event.target.value),
              })}
              timeProps={form.register("startTime", { deps: ["endTime"] })}
            />
            <DateTimeField
              id="endDate"
              label="Zakończenie"
              dateError={errors.endDate}
              timeError={errors.endTime}
              dateProps={{ min: startDate || undefined, ...form.register("endDate") }}
              timeProps={form.register("endTime")}
            />
          </div>
          {outsideWarning && (
            <p className="border-destructive/50 bg-destructive/10 rounded-lg border px-3 py-2 text-sm">
              {outsideWarning}{" "}
              {tournament && (
                <Link href={`/tournaments/${tournament.id}/schedule`} className="text-primary underline">
                  Przejdź do terminarza
                </Link>
              )}
            </p>
          )}
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
              disabled={
                disabled || isSubmitting || !!outsideWarning || (!!tournament && !form.formState.isDirty)
              }
            >
              {isSubmitting ? pendingLabel : submitLabel}
            </Button>
          </div>
        </FieldGroup>
      </fieldset>
    </form>
  )
}

/**
 * Mecze, które nie mieszczą się w nowym terminie. Zapis jest wtedy zablokowany (decyzja QA v3, UST-004):
 * najpierw trzeba przesunąć te mecze w terminarzu, inaczej zostałyby poza turniejem.
 */
function outsideTermWarning(
  matches: { startAt: number; endAt: number }[],
  startIso: string | null,
  endIso: string | null,
) {
  if (matches.length === 0) return null
  const start = startIso ? new Date(startIso).getTime() : -Infinity
  const end = endIso ? new Date(endIso).getTime() : Infinity
  const before = matches.filter((m) => m.startAt < start)
  const after = matches.filter((m) => m.endAt > end)
  const parts: string[] = []
  if (before.length)
    parts.push(
      `${plural(before.length, ["mecz zaczyna się", "mecze zaczynają się", "meczów zaczyna się"])} przed rozpoczęciem`,
    )
  if (after.length) {
    const lastEnd = Math.max(...after.map((m) => m.endAt))
    parts.push(
      `${plural(after.length, ["mecz kończy się", "mecze kończą się", "meczów kończy się"])} po zakończeniu (${after.length > 1 ? "ostatni " : ""}ok. ${formatDateTime(new Date(lastEnd))})`,
    )
  }
  if (parts.length === 0) return null
  return `${parts.join(", a ")}. Najpierw przesuń te mecze w terminarzu.`
}
