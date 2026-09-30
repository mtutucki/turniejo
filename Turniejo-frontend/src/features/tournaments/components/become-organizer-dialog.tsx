"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { useSession } from "next-auth/react"
import { useRouter } from "next/navigation"
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

import { useCreateOrganization } from "../api"

// Limit z CreateOrganizationCommandValidator.
const schema = z.object({
  name: z.string().trim().min(1, "Podaj nazwę organizacji").max(200, "Maksymalnie 200 znaków"),
})
type Values = z.infer<typeof schema>

/**
 * Konto założone jako rodzic/trener/sędzia może w każdej chwili zacząć organizować turnieje —
 * zakłada własną organizację (klub, szkołę, osobę) i dostaje rolę organizatora.
 */
export function BecomeOrganizerDialog({ trigger }: { trigger: ReactNode }) {
  const [open, setOpen] = useState(false)
  const { data: session } = useSession()
  const router = useRouter()
  const create = useCreateOrganization()
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { name: "" } })
  const { errors } = form.formState

  function submit({ name }: Values) {
    create.mutate(name, {
      onSuccess: () => {
        toast.success("Jesteś organizatorem — utwórz pierwszy turniej")
        setOpen(false)
        router.push("/tournaments")
      },
      onError: (error) => applyFieldErrors(error, form),
    })
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) form.reset({ name: session?.user?.name ?? "" })
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Zostań organizatorem</DialogTitle>
          <DialogDescription>
            Turnieje i baza drużyn należą do organizacji — może to być klub, szkoła albo Ty sam.
          </DialogDescription>
        </DialogHeader>
        <form id="organization-form" onSubmit={form.handleSubmit(submit)} noValidate>
          <FieldGroup>
            <TextField
              id="organization-name"
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
          <Button type="submit" form="organization-form" disabled={create.isPending}>
            {create.isPending ? "Tworzenie…" : "Utwórz organizację"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
