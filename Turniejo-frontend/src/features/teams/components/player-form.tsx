"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { CircleAlert } from "lucide-react"
import { useState } from "react"
import { Controller, useForm, useWatch } from "react-hook-form"
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
  PLAYER_POSITION_LABELS,
  PLAYER_STATUS_LABELS,
  type PlayerPosition,
  type SquadPlayerInput,
  type TournamentPlayer,
  type TournamentPlayerStatus,
} from "@/types/teams"
import type { TournamentCategory } from "@/types/tournaments"

const POSITIONS = Object.keys(PLAYER_POSITION_LABELS) as PlayerPosition[]
// "Usunięty" ustawia przycisk "Usuń ze składu", nie lista statusów.
const STATUSES = (Object.keys(PLAYER_STATUS_LABELS) as TournamentPlayerStatus[]).filter(
  (s) => s !== "Removed",
)

const baseSchema = z.object({
  firstName: z.string().trim().min(1, "Podaj imię").max(100, "Maksymalnie 100 znaków"),
  lastName: z.string().trim().min(1, "Podaj nazwisko").max(100, "Maksymalnie 100 znaków"),
  birthDate: z.string().min(1, "Podaj datę urodzenia"),
  position: z.enum(POSITIONS as [PlayerPosition, ...PlayerPosition[]]),
  status: z.enum(STATUSES as [TournamentPlayerStatus, ...TournamentPlayerStatus[]]),
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

/** Ktoś ze składu z tym samym imieniem, nazwiskiem i datą urodzenia (pomijając edytowanego). */
function findNamesake(
  squad: TournamentPlayer[],
  editing: TournamentPlayer | undefined,
  v: Pick<PlayerValues, "firstName" | "lastName" | "birthDate">,
) {
  return squad.find(
    (p) =>
      p.id !== editing?.id &&
      p.firstName.trim().localeCompare(v.firstName.trim(), "pl", { sensitivity: "base" }) === 0 &&
      p.lastName.trim().localeCompare(v.lastName.trim(), "pl", { sensitivity: "base" }) === 0 &&
      toDateInput(p.birthDate) === v.birthDate,
  )
}

const namesakeKey = (v: Pick<PlayerValues, "firstName" | "lastName" | "birthDate">) =>
  `${v.firstName.trim().toLowerCase()}|${v.lastName.trim().toLowerCase()}|${v.birthDate}`

/**
 * Reguły z backendu (Create/UpdatePlayerInTournamentTeam: walidator + handler) sprawdzane już
 * w formularzu, żeby błąd pojawił się od razu przy polu. Backend sprawdza je ponownie.
 * `editing` — edytowany zawodnik nie zajmuje sobie numeru. Dubel (te same dane) nie jest błędem
 * schematu: formularz pyta, czy to inna osoba, i pozwala zapisać.
 */
function buildSchema(
  category: TournamentCategory | undefined,
  squad: TournamentPlayer[],
  editing: TournamentPlayer | undefined,
) {
  const others = editing ? squad.filter((p) => p.id !== editing.id) : squad
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
    } else if (editing && v.birthDate === toDateInput(editing.birthDate)) {
      // Jak na backendzie: limit rocznika sprawdzamy tylko przy zmianie daty urodzenia.
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

    if (v.shirtNumber && others.some((p) => p.shirtNumber === Number(v.shirtNumber))) {
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
  /** Zawodnik ze składu do edycji — bez niego formularz zakłada nowy profil. */
  player?: TournamentPlayer
  submitLabel: string
  onSubmit: (input: SquadPlayerInput) => Promise<unknown>
  /** Przycisk obok "Zapisz" (np. Anuluj w dialogu). */
  secondaryAction?: React.ReactNode
}

export function PlayerForm({
  category,
  squad,
  player,
  submitLabel,
  onSubmit,
  secondaryAction,
}: PlayerFormProps) {
  const form = useForm<PlayerValues>({
    resolver: zodResolver(buildSchema(category, squad, player)),
    defaultValues: {
      firstName: player?.firstName ?? "",
      lastName: player?.lastName ?? "",
      birthDate: toDateInput(player?.birthDate ?? null),
      position: player?.position ?? "Unknown",
      status: player?.status ?? "Active",
      shirtNumber: player?.shirtNumber?.toString() ?? "",
      photoUrl: player?.photoUrl ?? "",
    },
  })
  const { errors, isSubmitting } = form.formState
  // Dane, dla których zapytaliśmy "czy to inna osoba?". Zmiana imienia, nazwiska lub daty chowa pytanie.
  const [askedFor, setAskedFor] = useState<string | null>(null)
  const [firstName, lastName, birthDate] = useWatch({
    control: form.control,
    name: ["firstName", "lastName", "birthDate"],
  })
  const current = { firstName, lastName, birthDate }
  const namesake =
    askedFor === namesakeKey(current) ? findNamesake(squad, player, current) : undefined

  async function submit(values: PlayerValues, allowDuplicate = false) {
    if (!allowDuplicate && findNamesake(squad, player, values)) {
      setAskedFor(namesakeKey(values))
      return
    }
    setAskedFor(null)
    try {
      await onSubmit({
        firstName: values.firstName,
        lastName: values.lastName,
        birthDate: fromDateInput(values.birthDate)!,
        position: values.position,
        status: values.status,
        shirtNumber: values.shirtNumber ? Number(values.shirtNumber) : null,
        photoUrl: values.photoUrl || null,
        allowDuplicate,
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
    <form onSubmit={form.handleSubmit((values) => submit(values))} noValidate>
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
        {player && (
          <Controller
            control={form.control}
            name="status"
            render={({ field }) => (
              <Field>
                <FieldLabel htmlFor="player-status">Status</FieldLabel>
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger id="player-status" className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {STATUSES.map((s) => (
                      <SelectItem key={s} value={s}>
                        {PLAYER_STATUS_LABELS[s]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
            )}
          />
        )}
        <TextField
          id="player-photo"
          label="Adres zdjęcia (opcjonalnie)"
          placeholder="https://…"
          error={errors.photoUrl}
          {...form.register("photoUrl")}
        />
        <FieldError errors={[errors.root]} />
        {namesake ? (
          <div
            role="alert"
            className="border-live/40 bg-live/10 space-y-3 rounded-md border px-3 py-3 text-sm"
          >
            <p className="flex items-start gap-2">
              <CircleAlert className="text-live mt-0.5 size-4 shrink-0" aria-hidden />
              <span>
                W składzie jest już {namesake.firstName} {namesake.lastName}
                {namesake.birthDate && `, ur. ${formatBirthDate(namesake.birthDate)}`}. Na pewno to
                inna osoba?
              </span>
            </p>
            <div className="flex flex-wrap justify-end gap-2">
              <Button
                type="button"
                variant="ghost"
                onClick={() => {
                  setAskedFor(null)
                  form.setFocus("firstName")
                }}
              >
                Popraw dane
              </Button>
              <Button
                type="button"
                disabled={isSubmitting}
                onClick={form.handleSubmit((values) => submit(values, true))}
              >
                {isSubmitting ? "Zapisywanie…" : player ? "Tak, zapisz" : "Tak, dodaj"}
              </Button>
            </div>
          </div>
        ) : (
          <div className="flex justify-end gap-2">
            {secondaryAction}
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Zapisywanie…" : submitLabel}
            </Button>
          </div>
        )}
      </FieldGroup>
    </form>
  )
}
