"use client"

import { useEffect } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { Controller, useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

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
import { Input } from "@/components/ui/input"
import { Switch } from "@/components/ui/switch"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { cn } from "@/lib/utils"
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
  /** Tryb kreatora: przycisk prowadzi do kolejnego kroku, także gdy nic się nie zmieniło. */
  onSaved?: () => void
  /** Kreator pyta przed wyjściem z kroku z niezapisanymi zmianami (QA v3: UST-017). */
  onDirtyChange?: (dirty: boolean) => void
}

export function SportSettingsForm({
  tournamentId,
  settings,
  disabled,
  onSaved,
  onDirtyChange,
}: SportSettingsFormProps) {
  const updateSettings = useUpdateTournamentSettings(tournamentId)
  const form = useForm<TournamentSettings>({ resolver: zodResolver(schema), values: settings })
  const { errors, isDirty } = form.formState
  useEffect(() => onDirtyChange?.(isDirty), [isDirty, onDirtyChange])

  const numberField = (name: NumberField, label: string, suffix?: string) => (
    <Field data-invalid={!!errors[name]}>
      <FieldLabel htmlFor={name}>{label}</FieldLabel>
      <div className="relative">
        <Input
          id={name}
          type="number"
          inputMode="numeric"
          aria-invalid={!!errors[name]}
          className={cn("font-mono tabular-nums", suffix && "pr-12")}
          {...form.register(name, { valueAsNumber: true })}
        />
        {suffix && (
          <span className="text-muted-foreground pointer-events-none absolute inset-y-0 right-3 flex items-center text-sm">
            {suffix}
          </span>
        )}
      </div>
      <FieldError errors={[errors[name]]} />
    </Field>
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
        <Field orientation="horizontal" className="rounded-lg border p-3">
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
    if (!isDirty) return onSaved?.()
    updateSettings.mutate({ ...values, expected: settings }, {
      onSuccess: () => {
        toast.success("Zasady gry zapisane")
        onSaved?.()
      },
      onError: (error) => applyFieldErrors(error, form),
    })
  }

  return (
    <form onSubmit={form.handleSubmit(submit)} noValidate>
      <fieldset disabled={disabled || updateSettings.isPending} className="contents">
        <FieldGroup>
          <FieldSet>
            <FieldLegend>Punktacja</FieldLegend>
            <FieldDescription>Punkty do tabeli za wynik meczu.</FieldDescription>
            <div className="grid gap-4 sm:grid-cols-3">
              {numberField("pointsForWin", "Wygrana", "pkt")}
              {numberField("pointsForDraw", "Remis", "pkt")}
              {numberField("pointsForLoss", "Porażka", "pkt")}
            </div>
          </FieldSet>
          <FieldSet>
            <FieldLegend>Czas</FieldLegend>
            <FieldDescription>Na tej podstawie generator układa terminarz.</FieldDescription>
            <div className="grid gap-4 sm:grid-cols-2">
              {numberField("matchDurationMinutes", "Długość meczu", "min")}
              {numberField("breakDurationMinutes", "Przerwa między meczami", "min")}
            </div>
          </FieldSet>
          <FieldSet>
            <FieldLegend>Walkower</FieldLegend>
            <FieldDescription>Wynik wpisywany, gdy mecz kończy się walkowerem.</FieldDescription>
            <div className="grid gap-4 sm:grid-cols-2">
              {numberField("walkoverHomeScore", "Gole gospodarzy")}
              {numberField("walkoverAwayScore", "Gole gości")}
            </div>
          </FieldSet>
          <FieldSet>
            <FieldLegend>Sędziowie i składy</FieldLegend>
            <FieldGroup>
              <div className="space-y-2 sm:max-w-xs">
                {numberField("refereeEditWindowMinutes", "Okno poprawek sędziego", "min")}
                <FieldDescription>
                  Tyle minut po meczu sędzia może jeszcze poprawić wynik.
                </FieldDescription>
              </div>
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
            <Button
              type="submit"
              disabled={disabled || updateSettings.isPending || (!isDirty && !onSaved)}
            >
              {updateSettings.isPending
                ? "Zapisywanie…"
                : onSaved
                  ? "Zapisz i dalej"
                  : "Zapisz zasady gry"}
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
