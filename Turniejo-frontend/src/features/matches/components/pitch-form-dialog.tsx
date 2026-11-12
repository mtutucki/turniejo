"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { Trash2 } from "lucide-react"
import { type ReactNode, useState } from "react"
import { Controller, useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
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
import { Textarea } from "@/components/ui/textarea"
import { useCategories, useGroups } from "@/features/tournaments/api"
import { applyFieldErrors } from "@/lib/api/form-errors"
import type { Pitch } from "@/types/matches"

import { useDeletePitch, useSavePitch } from "../api"
import { pitchGroupName, pitchGroupSections } from "../pitch-groups"

// Limity z AddPitchCommandValidator.
const schema = z.object({
  name: z.string().trim().min(1, "Podaj nazwę boiska").max(100, "Maksymalnie 100 znaków"),
  description: z.string().trim().max(500, "Maksymalnie 500 znaków"),
  isActive: z.boolean(),
  groupIds: z.array(z.string()),
})

type PitchValues = z.infer<typeof schema>

type PitchFormDialogProps = {
  tournamentId: string
  pitch?: Pitch
  /** Proponowana nazwa nowego boiska ("Boisko 1", "Boisko 2", …). */
  suggestedName?: string
  trigger: ReactNode
}

export function PitchFormDialog({
  tournamentId,
  pitch,
  suggestedName = "",
  trigger,
}: PitchFormDialogProps) {
  const [open, setOpen] = useState(false)
  const savePitch = useSavePitch(tournamentId)
  const deletePitch = useDeletePitch(tournamentId)
  const categories = useCategories(tournamentId)
  const groups = useGroups(tournamentId)
  const sections = pitchGroupSections(categories.data ?? [], groups.data ?? [])
  const knownGroups = new Set(sections.flatMap((section) => section.groups.map((g) => g.id)))
  const defaults: PitchValues = {
    name: pitch?.name ?? suggestedName,
    description: pitch?.description ?? "",
    isActive: pitch?.isActive ?? true,
    groupIds: pitch?.groupIds ?? [],
  }
  const form = useForm<PitchValues>({ resolver: zodResolver(schema), defaultValues: defaults })
  const { errors } = form.formState

  function submit(values: PitchValues) {
    savePitch.mutate(
      {
        pitchId: pitch?.id,
        ...values,
        description: values.description || null,
        // Grupa usunięta w innej karcie nie wraca do boiska.
        groupIds: values.groupIds.filter((id) => knownGroups.has(id)),
      },
      {
        onSuccess: () => {
          toast.success(pitch ? "Boisko zapisane" : "Boisko dodane")
          setOpen(false)
        },
        onError: (error) => applyFieldErrors(error, form),
      },
    )
  }

  function remove() {
    if (!pitch) return
    deletePitch.mutate(pitch.id, {
      onSuccess: () => {
        toast.success(`Usunięto: ${pitch.name}`)
        setOpen(false)
      },
    })
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
          <DialogTitle>{pitch ? "Edytuj boisko" : "Nowe boisko"}</DialogTitle>
        </DialogHeader>
        <form id="pitch-form" onSubmit={form.handleSubmit(submit)} noValidate>
          <FieldGroup>
            <TextField
              id="pitch-name"
              label="Nazwa"
              error={errors.name}
              {...form.register("name")}
            />
            <Field data-invalid={!!errors.description}>
              <FieldLabel htmlFor="pitch-description">Opis (opcjonalnie)</FieldLabel>
              <Textarea
                id="pitch-description"
                placeholder="np. hala główna, sztuczna trawa"
                rows={2}
                aria-invalid={!!errors.description}
                {...form.register("description")}
              />
              <FieldError errors={[errors.description]} />
            </Field>
            {sections.length > 0 && (
              <Controller
                control={form.control}
                name="groupIds"
                render={({ field }) => (
                  <FieldSet>
                    <FieldLegend variant="label">Grupy na tym boisku</FieldLegend>
                    <FieldDescription>
                      Generator ustawi tu tylko mecze zaznaczonych grup. Bez zaznaczenia boisko
                      przyjmuje wszystkie grupy, a mecze pucharu grają na każdym boisku.
                    </FieldDescription>
                    <div className="max-h-60 space-y-3 overflow-y-auto">
                      {sections.map(({ category, groups: categoryGroups }) => (
                        <div key={category.id} className="space-y-2">
                          {sections.length > 1 && (
                            <p className="text-muted-foreground text-xs font-medium">
                              {category.name}
                            </p>
                          )}
                          <div className="flex flex-wrap gap-x-6 gap-y-2">
                            {categoryGroups.map((group) => (
                              <Field key={group.id} orientation="horizontal" className="w-auto">
                                <Checkbox
                                  id={`pitch-group-${group.id}`}
                                  checked={field.value.includes(group.id)}
                                  onCheckedChange={(next) =>
                                    field.onChange(
                                      next
                                        ? [...field.value, group.id]
                                        : field.value.filter((id) => id !== group.id),
                                    )
                                  }
                                />
                                <FieldLabel
                                  htmlFor={`pitch-group-${group.id}`}
                                  className="font-normal"
                                >
                                  {pitchGroupName(group, [])}
                                </FieldLabel>
                              </Field>
                            ))}
                          </div>
                        </div>
                      ))}
                    </div>
                    <FieldError errors={[errors.groupIds]} />
                  </FieldSet>
                )}
              />
            )}
            {/* Nowe boisko jest zawsze aktywne (AddPitch nie przyjmuje IsActive). */}
            {pitch && (
              <Controller
                control={form.control}
                name="isActive"
                render={({ field }) => (
                  <Field orientation="horizontal">
                    <FieldContent>
                      <FieldLabel htmlFor="pitch-active">Aktywne</FieldLabel>
                      <FieldDescription>
                        Nieaktywne boisko nie jest brane pod uwagę przy generowaniu terminarza.
                      </FieldDescription>
                    </FieldContent>
                    <Switch
                      id="pitch-active"
                      checked={field.value}
                      onCheckedChange={field.onChange}
                    />
                  </Field>
                )}
              />
            )}
            <FieldError errors={[errors.root]} />
          </FieldGroup>
        </form>
        <DialogFooter>
          {pitch && (
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button
                  variant="ghost"
                  className="text-destructive hover:text-destructive sm:mr-auto"
                >
                  <Trash2 />
                  Usuń boisko
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Usunąć boisko „{pitch.name}”?</AlertDialogTitle>
                  <AlertDialogDescription>
                    Boiska z zaplanowanymi meczami nie da się usunąć - możesz je wtedy oznaczyć jako
                    nieaktywne.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Anuluj</AlertDialogCancel>
                  <AlertDialogAction variant="destructive" onClick={remove}>
                    Usuń
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          )}
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button type="submit" form="pitch-form" disabled={savePitch.isPending}>
            {savePitch.isPending ? "Zapisywanie…" : "Zapisz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
