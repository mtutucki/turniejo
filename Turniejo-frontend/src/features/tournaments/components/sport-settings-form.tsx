"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { Controller, useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
import { Button } from "@/components/ui/button"
import {
  Field,
  FieldContent,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
  FieldLegend,
  FieldSet,
} from "@/components/ui/field"
import { Switch } from "@/components/ui/switch"
import { applyFieldErrors } from "@/lib/api/form-errors"
import type { TournamentSettings } from "@/types/tournaments"

import { useUpdateTournamentSettings } from "../api"

const int = (min: number, message: string) =>
  z.number({ error: "Podaj liczbę" }).int("Podaj liczbę całkowitą").min(min, message)

// Reguły 1:1 z UpdateTournamentSettingsCommandValidator i TournamentSettings.Update.
const schema = z
  .object({
    pointsForWin: int(0, "Nie może być ujemne"),
    pointsForDraw: int(0, "Nie może być ujemne"),
    pointsForLoss: int(0, "Nie może być ujemne"),
    matchDurationMinutes: int(1, "Mecz musi trwać co najmniej minutę"),
    breakDurationMinutes: int(0, "Nie może być ujemne"),
    walkoverHomeScore: int(0, "Nie może być ujemne"),
    walkoverAwayScore: int(0, "Nie może być ujemne"),
    refereeEditWindowMinutes: int(0, "Nie może być ujemne"),
    isThirdPlaceMatchEnabled: z.boolean(),
    canCoachEditSquadAfterStart: z.boolean(),
  })
  .refine((v) => v.pointsForDraw >= v.pointsForLoss, {
    path: ["pointsForDraw"],
    message: "Remis nie może dawać mniej punktów niż porażka",
  })
  .refine((v) => v.pointsForWin >= v.pointsForDraw, {
    path: ["pointsForWin"],
    message: "Wygrana nie może dawać mniej punktów niż remis",
  })
  .refine((v) => v.walkoverHomeScore !== v.walkoverAwayScore, {
    path: ["walkoverHomeScore"],
    message: "Walkower nie może kończyć się remisem",
  })

type NumberField = Exclude<
  keyof TournamentSettings,
  "isThirdPlaceMatchEnabled" | "canCoachEditSquadAfterStart"
>

type SportSettingsFormProps = {
  tournamentId: string
  settings: TournamentSettings
  disabled: boolean
}

export function SportSettingsForm({ tournamentId, settings, disabled }: SportSettingsFormProps) {
  const updateSettings = useUpdateTournamentSettings(tournamentId)
  const form = useForm<TournamentSettings>({ resolver: zodResolver(schema), values: settings })
  const { errors, isDirty } = form.formState

  const numberField = (name: NumberField, label: string, suffix?: string) => (
    <TextField
      id={name}
      label={suffix ? `${label} (${suffix})` : label}
      type="number"
      inputMode="numeric"
      className="font-mono tabular-nums"
      error={errors[name]}
      {...form.register(name, { valueAsNumber: true })}
    />
  )

  const switchField = (
    name: "isThirdPlaceMatchEnabled" | "canCoachEditSquadAfterStart",
    label: string,
    description: string,
  ) => (
    <Controller
      control={form.control}
      name={name}
      render={({ field }) => (
        <Field orientation="horizontal">
          <FieldContent>
            <FieldLabel htmlFor={name}>{label}</FieldLabel>
            <FieldDescription>{description}</FieldDescription>
          </FieldContent>
          <Switch id={name} checked={field.value} onCheckedChange={field.onChange} />
        </Field>
      )}
    />
  )

  function submit(values: TournamentSettings) {
    updateSettings.mutate(values, {
      onSuccess: () => toast.success("Ustawienia sportowe zapisane"),
      onError: (error) => applyFieldErrors(error, form),
    })
  }

  return (
    <form onSubmit={form.handleSubmit(submit)} noValidate>
      <fieldset disabled={disabled || updateSettings.isPending} className="contents">
        <FieldGroup>
          <FieldSet>
            <FieldLegend>Punktacja</FieldLegend>
            <div className="grid gap-4 sm:grid-cols-3">
              {numberField("pointsForWin", "Wygrana")}
              {numberField("pointsForDraw", "Remis")}
              {numberField("pointsForLoss", "Porażka")}
            </div>
          </FieldSet>
          <FieldSet>
            <FieldLegend>Czas</FieldLegend>
            <div className="grid gap-4 sm:grid-cols-2">
              {numberField("matchDurationMinutes", "Długość meczu", "min")}
              {numberField("breakDurationMinutes", "Przerwa między meczami", "min")}
            </div>
          </FieldSet>
          <FieldSet>
            <FieldLegend>Walkower</FieldLegend>
            <div className="grid gap-4 sm:grid-cols-2">
              {numberField("walkoverHomeScore", "Gole gospodarzy")}
              {numberField("walkoverAwayScore", "Gole gości")}
            </div>
          </FieldSet>
          <FieldSet>
            <FieldLegend>Opcje</FieldLegend>
            <FieldGroup>
              {numberField("refereeEditWindowMinutes", "Okno edycji wyniku przez sędziego", "min")}
              {switchField(
                "isThirdPlaceMatchEnabled",
                "Mecz o 3. miejsce",
                "Dodaje mecz przegranych półfinalistów do drabinki.",
              )}
              {switchField(
                "canCoachEditSquadAfterStart",
                "Trener edytuje skład po starcie",
                "Pozwala trenerom zmieniać skład, gdy turniej już trwa.",
              )}
            </FieldGroup>
          </FieldSet>
          <FieldError errors={[errors.root]} />
          <div className="flex gap-2">
            <Button type="submit" disabled={disabled || !isDirty || updateSettings.isPending}>
              {updateSettings.isPending ? "Zapisywanie…" : "Zapisz ustawienia"}
            </Button>
            {isDirty && !disabled && (
              <Button type="button" variant="ghost" onClick={() => form.reset(settings)}>
                Odrzuć zmiany
              </Button>
            )}
          </div>
        </FieldGroup>
      </fieldset>
    </form>
  )
}
