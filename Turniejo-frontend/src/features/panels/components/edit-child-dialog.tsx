"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { Pencil } from "lucide-react"
import { useState } from "react"
import { Controller, useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
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
import { applyFieldErrors } from "@/lib/api/form-errors"
import { fromDateInput, toDateInput } from "@/lib/format"
import type { MyChild } from "@/types/roles-panels"
import { PLAYER_POSITION_LABELS, type PlayerPosition } from "@/types/teams"

import { useUpdateChild } from "../api"

const POSITIONS = Object.keys(PLAYER_POSITION_LABELS) as PlayerPosition[]

// Limity z UpdatePlayerCommandValidator.
const schema = z.object({
  firstName: z.string().trim().min(1, "Podaj imię").max(100, "Maksymalnie 100 znaków"),
  lastName: z.string().trim().min(1, "Podaj nazwisko").max(100, "Maksymalnie 100 znaków"),
  birthDate: z
    .string()
    .min(1, "Podaj datę urodzenia")
    .refine((v) => v <= new Date().toISOString().slice(0, 10), "Data nie może być z przyszłości"),
  defaultPosition: z.string(),
  photoUrl: z
    .string()
    .trim()
    .refine((v) => v === "" || /^https?:\/\/\S+$/i.test(v), "Podaj pełny adres http(s) zdjęcia"),
})

type Values = z.infer<typeof schema>

/** Edycja profilu dziecka — pokazywana tylko opiekunom z CanEdit (backend i tak to sprawdza). */
export function EditChildDialog({ child }: { child: MyChild }) {
  const [open, setOpen] = useState(false)
  const update = useUpdateChild(child.playerId)
  const defaults: Values = {
    firstName: child.firstName,
    lastName: child.lastName,
    birthDate: toDateInput(child.birthDate),
    defaultPosition: child.defaultPosition,
    photoUrl: child.photoUrl ?? "",
  }
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: defaults })
  const { errors } = form.formState

  function submit(values: Values) {
    update.mutate(
      {
        firstName: values.firstName,
        lastName: values.lastName,
        birthDate: fromDateInput(values.birthDate)!,
        defaultPosition: values.defaultPosition as PlayerPosition,
        photoUrl: values.photoUrl || null,
      },
      {
        onSuccess: () => {
          toast.success("Profil zapisany")
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
      <DialogTrigger asChild>
        <Button variant="outline" size="sm">
          <Pencil />
          Edytuj profil
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Profil zawodnika</DialogTitle>
        </DialogHeader>
        <form id="child-form" onSubmit={form.handleSubmit(submit)} noValidate>
          <FieldGroup>
            <div className="grid gap-4 sm:grid-cols-2">
              <TextField
                id="child-first"
                label="Imię"
                error={errors.firstName}
                {...form.register("firstName")}
              />
              <TextField
                id="child-last"
                label="Nazwisko"
                error={errors.lastName}
                {...form.register("lastName")}
              />
            </div>
            <TextField
              id="child-birth"
              label="Data urodzenia"
              type="date"
              error={errors.birthDate}
              {...form.register("birthDate")}
            />
            <Controller
              control={form.control}
              name="defaultPosition"
              render={({ field }) => (
                <Field>
                  <FieldLabel htmlFor="child-position">Pozycja</FieldLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="child-position" className="w-full">
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
              id="child-photo"
              label="Zdjęcie — adres (opcjonalnie)"
              placeholder="https://…"
              error={errors.photoUrl}
              {...form.register("photoUrl")}
            />
            <FieldError errors={[errors.root]} />
          </FieldGroup>
        </form>
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button type="submit" form="child-form" disabled={update.isPending}>
            {update.isPending ? "Zapisywanie…" : "Zapisz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
