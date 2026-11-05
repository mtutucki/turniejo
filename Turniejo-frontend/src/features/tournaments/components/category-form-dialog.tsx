"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { type ReactNode, useState } from "react"
import { useForm } from "react-hook-form"
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

// Reguły 1:1 z Add/UpdateTournamentCategoryCommandValidator.
const schema = z
  .object({
    name: z.string().trim().min(1, "Podaj nazwę kategorii").max(100, "Maksymalnie 100 znaków"),
    minBirthDate: z.string(),
    maxBirthDate: z.string(),
    order: z.number({ error: "Podaj liczbę" }).int("Podaj liczbę całkowitą"),
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
  trigger: ReactNode
}

export function CategoryFormDialog({
  tournamentId,
  category,
  nextOrder,
  trigger,
}: CategoryFormDialogProps) {
  const [open, setOpen] = useState(false)
  const saveCategory = useSaveCategory(tournamentId)
  const defaults: CategoryValues = {
    name: category?.name ?? "",
    minBirthDate: toDateInput(category?.minBirthDate ?? null),
    maxBirthDate: toDateInput(category?.maxBirthDate ?? null),
    order: category?.order ?? nextOrder,
  }
  const form = useForm<CategoryValues>({ resolver: zodResolver(schema), defaultValues: defaults })
  const { errors } = form.formState

  function submit(values: CategoryValues) {
    saveCategory.mutate(
      {
        categoryId: category?.id,
        name: values.name,
        minBirthDate: fromDateInput(values.minBirthDate),
        maxBirthDate: fromDateInput(values.maxBirthDate),
        order: values.order,
      },
      {
        onSuccess: () => {
          toast.success(category ? "Kategoria zapisana" : "Kategoria dodana")
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
