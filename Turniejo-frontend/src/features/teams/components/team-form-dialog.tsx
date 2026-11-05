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
import type { Team } from "@/types/teams"

import { useSaveTeam } from "../api"
import { TeamLogo } from "./team-logo"

// Limity 1:1 z CreateTeamCommandValidator.
const schema = z.object({
  name: z.string().trim().min(1, "Podaj nazwę drużyny").max(200, "Maksymalnie 200 znaków"),
  shortName: z.string().trim().max(20, "Maksymalnie 20 znaków"),
  logoUrl: z
    .string()
    .trim()
    .refine((v) => v === "" || /^https?:\/\/\S+$/i.test(v), "Podaj pełny adres http(s) obrazka"),
})

type TeamValues = z.infer<typeof schema>

type TeamFormDialogProps = {
  organizationId: string
  team?: Team
  trigger: ReactNode
}

export function TeamFormDialog({ organizationId, team, trigger }: TeamFormDialogProps) {
  const [open, setOpen] = useState(false)
  const saveTeam = useSaveTeam(organizationId)
  const defaults: TeamValues = {
    name: team?.name ?? "",
    shortName: team?.shortName ?? "",
    logoUrl: team?.logoUrl ?? "",
  }
  const form = useForm<TeamValues>({ resolver: zodResolver(schema), defaultValues: defaults })
  const { errors } = form.formState
  const previewName = useWatch({ control: form.control, name: "name" }) || "?"
  const previewLogo = useWatch({ control: form.control, name: "logoUrl" })

  function submit(values: TeamValues) {
    saveTeam.mutate(
      {
        teamId: team?.id,
        name: values.name,
        shortName: values.shortName || null,
        logoUrl: values.logoUrl || null,
      },
      {
        onSuccess: () => {
          toast.success(team ? "Drużyna zapisana" : "Drużyna dodana")
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
          <DialogTitle>{team ? "Edytuj drużynę" : "Nowa drużyna"}</DialogTitle>
          <DialogDescription>
            Drużyna trafia do bazy organizacji - potem zgłosisz ją do dowolnego turnieju.
          </DialogDescription>
        </DialogHeader>
        <form id="team-form" onSubmit={form.handleSubmit(submit)} noValidate>
          <FieldGroup>
            <TextField
              id="team-name"
              label="Nazwa"
              error={errors.name}
              {...form.register("name")}
            />
            <TextField
              id="team-short"
              label="Skrót (opcjonalnie)"
              placeholder="np. ORŁ"
              error={errors.shortName}
              {...form.register("shortName")}
            />
            <div className="flex items-end gap-3">
              <TextField
                id="team-logo"
                label="Adres herbu (opcjonalnie)"
                placeholder="https://…"
                error={errors.logoUrl}
                {...form.register("logoUrl")}
              />
              <TeamLogo
                name={previewName}
                logoUrl={!errors.logoUrl && /^https?:\/\//i.test(previewLogo) ? previewLogo : null}
                size={40}
                className="mb-0.5"
              />
            </div>
            <FieldError errors={[errors.root]} />
          </FieldGroup>
        </form>
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button type="submit" form="team-form" disabled={saveTeam.isPending}>
            {saveTeam.isPending ? "Zapisywanie…" : "Zapisz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
