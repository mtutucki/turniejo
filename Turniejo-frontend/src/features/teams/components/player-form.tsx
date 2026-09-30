"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { Controller, useForm } from "react-hook-form"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
import { Button } from "@/components/ui/button"
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { formatBirthDate, fromDateInput, toDateInput } from "@/lib/format"
import {
  type NewPlayerInput,
  PLAYER_POSITION_LABELS,
  type PlayerPosition,
  type TournamentPlayer,
} from "@/types/teams"
import type { TournamentCategory } from "@/types/tournaments"

const POSITIONS = Object.keys(PLAYER_POSITION_LABELS) as PlayerPosition[]

const baseSchema = z.object({
  firstName: z.string().trim().min(1, "Podaj imię").max(100, "Maksymalnie 100 znaków"),
  lastName: z.string().trim().min(1, "Podaj nazwisko").max(100, "Maksymalnie 100 znaków"),
  birthDate: z.string().min(1, "Podaj datę urodzenia"),
  position: z.enum(POSITIONS as [PlayerPosition, ...PlayerPosition[]]),
  shirtNumber: z
    .string()
    .trim()
    .refine((v) => v === "" || (/^\d+$/.test(v) && Number(v) > 0), {
      message: "Numer musi być liczbą większą od 0",
    }),
  photoUrl: z
    .string()
    .trim()
    .refine((v) => v === "" || /^https?:\/\/\S+$/i.test(v), "Podaj pełny adres http(s) zdjęcia"),
})

type PlayerValues = z.infer<typeof baseSchema>

/**
 * Reguły z backendu (CreatePlayerInTournamentTeamCommandValidator + handler) sprawdzane już
 * w formularzu, żeby błąd pojawił się od razu przy polu. Backend sprawdza je ponownie.
 */
function buildSchema(category: TournamentCategory | undefined, squad: TournamentPlayer[]) {
  const min = toDateInput(category?.minBirthDate ?? null)
  const max = toDateInput(category?.maxBirthDate ?? null)
  const today = new Date().toISOString().slice(0, 10)

  return baseSchema.superRefine((v, ctx) => {
    if (v.birthDate > today) {
      ctx.addIssue({
        code: "custom",
        path: ["birthDate"],
        message: "Data urodzenia nie może być z przyszłości",
      })
    } else if (min && v.birthDate < min) {
      ctx.addIssue({
        code: "custom",
        path: ["birthDate"],
        message: `Za stary na kategorię ${category!.name}`,
      })
    } else if (max && v.birthDate > max) {
      ctx.addIssue({
        code: "custom",
        path: ["birthDate"],
        message: `Za młody na kategorię ${category!.name}`,
      })
    }

    const duplicate = squad.some(
      (p) =>
        p.firstName.localeCompare(v.firstName, "pl", { sensitivity: "base" }) === 0 &&
        p.lastName.localeCompare(v.lastName, "pl", { sensitivity: "base" }) === 0 &&
        toDateInput(p.birthDate) === v.birthDate,
    )
    if (duplicate) {
      ctx.addIssue({
        code: "custom",
        path: ["lastName"],
        message: "Ten zawodnik jest już w składzie",
      })
    }

    if (v.shirtNumber && squad.some((p) => p.shirtNumber === Number(v.shirtNumber))) {
      ctx.addIssue({
        code: "custom",
        path: ["shirtNumber"],
        message: "Ten numer jest już zajęty w drużynie",
      })
    }
  })
}

type PlayerFormProps = {
  category: TournamentCategory | undefined
  squad: TournamentPlayer[]
  submitLabel: string
  onSubmit: (input: NewPlayerInput) => Promise<unknown>
  /** Przycisk obok "Zapisz" (np. Anuluj w dialogu). */
  secondaryAction?: React.ReactNode
}

export function PlayerForm({
  category,
  squad,
  submitLabel,
  onSubmit,
  secondaryAction,
}: PlayerFormProps) {
  const form = useForm<PlayerValues>({
    resolver: zodResolver(buildSchema(category, squad)),
    defaultValues: {
      firstName: "",
      lastName: "",
      birthDate: "",
      position: "Unknown",
      shirtNumber: "",
      photoUrl: "",
    },
  })
  const { errors, isSubmitting } = form.formState

  async function submit(values: PlayerValues) {
    try {
      await onSubmit({
        firstName: values.firstName,
        lastName: values.lastName,
        birthDate: fromDateInput(values.birthDate)!,
        position: values.position,
        shirtNumber: values.shirtNumber ? Number(values.shirtNumber) : null,
        photoUrl: values.photoUrl || null,
      })
    } catch (error) {
      applyFieldErrors(error, form)
    }
  }

  const range =
    category?.minBirthDate || category?.maxBirthDate
      ? `Kategoria ${category.name}: ${category.minBirthDate ? formatBirthDate(category.minBirthDate) : "…"} – ${category.maxBirthDate ? formatBirthDate(category.maxBirthDate) : "…"}`
      : undefined

  return (
    <form onSubmit={form.handleSubmit(submit)} noValidate>
      <FieldGroup>
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            id="player-first"
            label="Imię"
            error={errors.firstName}
            {...form.register("firstName")}
          />
          <TextField
            id="player-last"
            label="Nazwisko"
            error={errors.lastName}
            {...form.register("lastName")}
          />
        </div>
        <div>
          <TextField
            id="player-birth"
            label="Data urodzenia"
            type="date"
            error={errors.birthDate}
            {...form.register("birthDate")}
          />
          {range && <FieldDescription className="mt-1">{range}</FieldDescription>}
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <Controller
            control={form.control}
            name="position"
            render={({ field }) => (
              <Field>
                <FieldLabel htmlFor="player-position">Pozycja</FieldLabel>
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger id="player-position" className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {POSITIONS.map((p) => (
                      <SelectItem key={p} value={p}>
                        {PLAYER_POSITION_LABELS[p]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
            )}
          />
          <TextField
            id="player-number"
            label="Numer koszulki (opcjonalnie)"
            inputMode="numeric"
            className="font-mono tabular-nums"
            error={errors.shirtNumber}
            {...form.register("shirtNumber")}
          />
        </div>
        <TextField
          id="player-photo"
          label="Adres zdjęcia (opcjonalnie)"
          placeholder="https://…"
          error={errors.photoUrl}
          {...form.register("photoUrl")}
        />
        <FieldError errors={[errors.root]} />
        <div className="flex justify-end gap-2">
          {secondaryAction}
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Zapisywanie…" : submitLabel}
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}
