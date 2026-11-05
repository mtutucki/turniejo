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
import type { MyOrganization } from "@/types/tournaments"

import { useRenameOrganization } from "../api"

// Limit z RenameOrganizationCommandValidator.
const schema = z.object({
  name: z.string().trim().min(1, "Podaj nazwę organizacji").max(200, "Maksymalnie 200 znaków"),
})
type Values = z.infer<typeof schema>

/** Organizacja zakładana przy rejestracji dostaje nazwę z automatu — tu organizator ją zmienia. */
export function RenameOrganizationDialog({
  organization,
  trigger,
}: {
  organization: MyOrganization
  trigger: ReactNode
}) {
  const [open, setOpen] = useState(false)
  const rename = useRenameOrganization(organization.id)
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { name: organization.name },
  })
  const { errors } = form.formState

  function submit({ name }: Values) {
    rename.mutate(name, {
      onSuccess: () => {
        toast.success("Zmieniono nazwę organizacji")
        setOpen(false)
      },
      onError: (error) => applyFieldErrors(error, form),
    })
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) form.reset({ name: organization.name })
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Nazwa organizacji</DialogTitle>
          <DialogDescription>
            Klub, szkoła albo Ty sam - pod tą nazwą prowadzisz turnieje i bazę drużyn.
          </DialogDescription>
        </DialogHeader>
        <form id="rename-organization-form" onSubmit={form.handleSubmit(submit)} noValidate>
          <FieldGroup>
            <TextField
              id="rename-organization-name"
              label="Nazwa organizacji"
              placeholder="np. UKS Orły Warszawa"
              error={errors.name}
              {...form.register("name")}
            />
            <FieldError errors={[errors.root]} />
          </FieldGroup>
        </form>
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button type="submit" form="rename-organization-form" disabled={rename.isPending}>
            {rename.isPending ? "Zapisywanie…" : "Zapisz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
