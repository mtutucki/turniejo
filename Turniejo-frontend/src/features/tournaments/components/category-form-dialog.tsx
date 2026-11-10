"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { type ReactNode, useState } from "react"
import { useForm, useWatch } from "react-hook-form"
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
import { FieldError, FieldGroup } from "@/components/ui/field"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { fromDateInput, toDateInput } from "@/lib/format"
import type { TournamentCategory } from "@/types/tournaments"

import { useSaveCategory } from "../api"

/** Jak DefaultGroupProvisioner.MaxGroupsOnCreate w backendzie. */
const MAX_GROUPS = 8

/** "Grupa A, Grupa B i Grupa C" - podpowiedź pod polem liczby grup. */
function groupNames(count: number) {
  const names = Array.from({ length: count }, (_, i) => `Grupa ${String.fromCharCode(65 + i)}`)
  return names.length > 1 ? `${names.slice(0, -1).join(", ")} i ${names.at(-1)}` : names[0]
}

// Reguły 1:1 z Add/UpdateTournamentCategoryCommandValidator.
const schema = z
  .object({
    name: z.string().trim().min(1, "Podaj nazwę kategorii").max(100, "Maksymalnie 100 znaków"),
    minBirthDate: z.string(),
    maxBirthDate: z.string(),
    order: z.number({ error: "Podaj liczbę" }).int("Podaj liczbę całkowitą"),
    groupCount: z
      .number({ error: "Podaj liczbę" })
      .int("Podaj liczbę całkowitą")
      .min(1, `Liczba grup musi być od 1 do ${MAX_GROUPS}`)
      .max(MAX_GROUPS, `Liczba grup musi być od 1 do ${MAX_GROUPS}`),
  })
  .refine((v) => !v.minBirthDate || !v.maxBirthDate || v.minBirthDate <= v.maxBirthDate, {
    path: ["maxBirthDate"],
    message: "Data „do” nie może być wcześniejsza niż „od”",
  })

type CategoryValues = z.infer<typeof schema>

type CategoryFormDialogProps = {
  tournamentId: string
  category?: TournamentCategory
  /** Kolejność proponowana dla nowej kategorii (na końcu listy). */
  nextOrder: number
  /** Nowa kategoria pyta, ile grup założyć od razu (format z grupami, przed terminarzem). */
  withGroups?: boolean
  trigger: ReactNode
}

export function CategoryFormDialog({
  tournamentId,
  category,
  nextOrder,
  withGroups = false,
  trigger,
}: CategoryFormDialogProps) {
  const askForGroups = !category && withGroups
  const [open, setOpen] = useState(false)
  const saveCategory = useSaveCategory(tournamentId)
  const defaults: CategoryValues = {
    name: category?.name ?? "",
    minBirthDate: toDateInput(category?.minBirthDate ?? null),
    maxBirthDate: toDateInput(category?.maxBirthDate ?? null),
    order: category?.order ?? nextOrder,
    groupCount: 1,
  }
  const form = useForm<CategoryValues>({ resolver: zodResolver(schema), defaultValues: defaults })
  const { errors } = form.formState
  const groupCount = useWatch({ control: form.control, name: "groupCount" })

  function submit(values: CategoryValues) {
    saveCategory.mutate(
      {
        categoryId: category?.id,
        name: values.name,
        minBirthDate: fromDateInput(values.minBirthDate),
        maxBirthDate: fromDateInput(values.maxBirthDate),
        order: values.order,
        ...(askForGroups && { groupCount: values.groupCount }),
      },
      {
        onSuccess: () => {
          toast.success(
            category
              ? "Kategoria zapisana"
              : askForGroups && values.groupCount > 1
                ? `Kategoria dodana z ${values.groupCount} grupami`
                : "Kategoria dodana",
          )
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
          <DialogTitle>{category ? "Edytuj kategorię" : "Nowa kategoria"}</DialogTitle>
          <DialogDescription>
            Kategoria to osobne rozgrywki, np. rocznik albo poziom - drużyny z różnych kategorii nie
            grają ze sobą i mają osobne tabele. Zakres dat urodzenia jest opcjonalny (sprawdza wiek
            zawodników w składach).
          </DialogDescription>
        </DialogHeader>
        <form id="category-form" onSubmit={form.handleSubmit(submit)} noValidate>
          <FieldGroup>
            <TextField
              id="category-name"
              label="Nazwa"
              placeholder="np. U12, Rocznik 2014"
              error={errors.name}
              {...form.register("name")}
            />
            <div className="grid gap-4 sm:grid-cols-2">
              <TextField
                id="category-min"
                label="Urodzeni od"
                type="date"
                error={errors.minBirthDate}
                {...form.register("minBirthDate")}
              />
              <TextField
                id="category-max"
                label="Urodzeni do"
                type="date"
                error={errors.maxBirthDate}
                {...form.register("maxBirthDate")}
              />
            </div>
            {askForGroups && (
              <TextField
                id="category-groups"
                label="Liczba grup"
                type="number"
                inputMode="numeric"
                min={1}
                max={MAX_GROUPS}
                className="font-mono tabular-nums sm:max-w-32"
                description={
                  groupCount >= 1 && groupCount <= MAX_GROUPS
                    ? `Powstanie: ${groupNames(groupCount)}. Drużyny przypiszesz do grup w zakładce Drużyny.`
                    : undefined
                }
                error={errors.groupCount}
                {...form.register("groupCount", { valueAsNumber: true })}
              />
            )}
            <TextField
              id="category-order"
              label="Kolejność wyświetlania"
              type="number"
              inputMode="numeric"
              className="font-mono tabular-nums sm:max-w-32"
              error={errors.order}
              {...form.register("order", { valueAsNumber: true })}
            />
            <FieldError errors={[errors.root]} />
          </FieldGroup>
        </form>
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button type="submit" form="category-form" disabled={saveCategory.isPending}>
            {saveCategory.isPending ? "Zapisywanie…" : "Zapisz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
