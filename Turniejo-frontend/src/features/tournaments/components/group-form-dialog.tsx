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
import type { TournamentGroup } from "@/types/tournaments"

import { useSaveGroup } from "../api"

const schema = z.object({
  name: z.string().trim().min(1, "Podaj nazwę grupy").max(100, "Maksymalnie 100 znaków"),
  order: z.number({ error: "Podaj liczbę" }).int("Podaj liczbę całkowitą"),
})

type GroupValues = z.infer<typeof schema>

type GroupFormDialogProps = {
  tournamentId: string
  categoryId: string
  categoryName: string
  group?: TournamentGroup
  nextOrder: number
  /** Proponowana nazwa nowej grupy ("Grupa A", "Grupa B", …). */
  suggestedName?: string
  /** 2: grupa drugiej fazy, drużyny przychodzą z miejsc w tabelach fazy 1. */
  stage?: 1 | 2
  trigger: ReactNode
}

export function GroupFormDialog({
  tournamentId,
  categoryId,
  categoryName,
  group,
  nextOrder,
  suggestedName = "",
  stage = group?.stage ?? 1,
  trigger,
}: GroupFormDialogProps) {
  const [open, setOpen] = useState(false)
  const saveGroup = useSaveGroup(tournamentId)
  const defaults: GroupValues = {
    name: group?.name ?? suggestedName,
    order: group?.order ?? nextOrder,
  }
  const form = useForm<GroupValues>({ resolver: zodResolver(schema), defaultValues: defaults })
  const { errors } = form.formState

  function submit(values: GroupValues) {
    saveGroup.mutate(
      { groupId: group?.id, categoryId, stage, ...values },
      {
        onSuccess: () => {
          toast.success(group ? "Grupa zapisana" : "Grupa dodana")
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
          <DialogTitle>
            {group ? "Edytuj grupę" : stage === 2 ? "Nowa grupa drugiej fazy" : "Nowa grupa"} ·{" "}
            {categoryName}
          </DialogTitle>
          <DialogDescription>
            {stage === 2
              ? "Grupa drugiej fazy: trafiają do niej drużyny z wybranych miejsc w tabelach pierwszej fazy i znów grają każdy z każdym."
              : `Grupa to pula w kategorii ${categoryName}: drużyny z jednej grupy grają każdy z każdym i mają wspólną tabelę. Druga grupa ma sens, gdy drużyn jest tyle, że wszystkie mecze by się nie zmieściły.`}
          </DialogDescription>
        </DialogHeader>
        <form id="group-form" onSubmit={form.handleSubmit(submit)} noValidate>
          <FieldGroup>
            <TextField
              id="group-name"
              label="Nazwa"
              error={errors.name}
              {...form.register("name")}
            />
            <TextField
              id="group-order"
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
          <Button type="submit" form="group-form" disabled={saveGroup.isPending}>
            {saveGroup.isPending ? "Zapisywanie…" : "Zapisz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
