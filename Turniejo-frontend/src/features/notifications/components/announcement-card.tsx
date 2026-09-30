"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { Megaphone } from "lucide-react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Textarea } from "@/components/ui/textarea"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { plural } from "@/lib/format"

import { useCreateAnnouncement } from "../notification-sync"

// Limity z CreateOrganizerAnnouncementCommandValidator.
const schema = z.object({
  title: z.string().trim().min(1, "Podaj tytuł").max(200, "Maksymalnie 200 znaków"),
  message: z.string().trim().min(1, "Podaj treść").max(2000, "Maksymalnie 2000 znaków"),
})
type Values = z.infer<typeof schema>

/** Ogłoszenie do wszystkich uczestników turnieju — przychodzi im na żywo jako powiadomienie. */
export function AnnouncementCard({ tournamentId }: { tournamentId: string }) {
  const create = useCreateAnnouncement(tournamentId)
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { title: "", message: "" },
  })
  const { errors } = form.formState

  function submit(values: Values) {
    create.mutate(values, {
      onSuccess: (result) => {
        toast.success(
          `Wysłano do ${plural(result.recipientCount, ["odbiorcy", "odbiorców", "odbiorców"])}`,
        )
        form.reset()
      },
      onError: (error) => applyFieldErrors(error, form),
    })
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Megaphone className="text-primary size-4" />
          Ogłoszenie
        </CardTitle>
        <CardDescription>Trafi do sędziów, sztabów drużyn i rodziców zawodników.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={form.handleSubmit(submit)} noValidate>
          <FieldGroup>
            <TextField
              id="announcement-title"
              label="Tytuł"
              placeholder="np. Zmiana godziny otwarcia hali"
              error={errors.title}
              {...form.register("title")}
            />
            <Field data-invalid={!!errors.message || undefined}>
              <FieldLabel htmlFor="announcement-message">Treść</FieldLabel>
              <Textarea
                id="announcement-message"
                rows={3}
                aria-invalid={!!errors.message || undefined}
                {...form.register("message")}
              />
              <FieldError errors={[errors.message]} />
            </Field>
            <FieldError errors={[errors.root]} />
            <Button type="submit" className="justify-self-start" disabled={create.isPending}>
              {create.isPending ? "Wysyłanie…" : "Wyślij ogłoszenie"}
            </Button>
          </FieldGroup>
        </form>
      </CardContent>
    </Card>
  )
}
