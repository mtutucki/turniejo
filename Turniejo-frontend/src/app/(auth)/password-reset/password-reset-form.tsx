"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { useMutation } from "@tanstack/react-query"
import { MailCheck } from "lucide-react"
import { useForm } from "react-hook-form"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
import { Button } from "@/components/ui/button"
import { FieldGroup } from "@/components/ui/field"
import { apiFetch } from "@/lib/api/client"
import { applyFieldErrors } from "@/lib/api/form-errors"

const schema = z.object({
  email: z.email("Nieprawidłowy adres email"),
})

type PasswordResetValues = z.infer<typeof schema>

export function PasswordResetForm() {
  const form = useForm<PasswordResetValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: "" },
  })

  const requestReset = useMutation({
    mutationFn: (values: PasswordResetValues) =>
      apiFetch("/api/auth/password-reset/request", { method: "POST", body: values }),
    meta: { handlesValidation: true },
    onError: (error) => applyFieldErrors(error, form),
  })

  // Backend zawsze odpowiada 200 — nie zdradzamy, czy konto o tym emailu istnieje.
  if (requestReset.isSuccess) {
    return (
      <div className="flex flex-col items-center gap-3 py-2 text-center">
        <MailCheck className="text-primary size-8" />
        <p className="text-sm">
          Jeśli konto <span className="font-medium">{requestReset.variables.email}</span> istnieje,
          wysłaliśmy na nie link do ustawienia nowego hasła.
        </p>
      </div>
    )
  }

  return (
    <form onSubmit={form.handleSubmit((values) => requestReset.mutate(values))} noValidate>
      <FieldGroup>
        <TextField
          id="email"
          label="Email"
          type="email"
          autoComplete="email"
          error={form.formState.errors.email}
          {...form.register("email")}
        />
        <Button type="submit" size="lg" className="w-full" disabled={requestReset.isPending}>
          {requestReset.isPending ? "Wysyłanie…" : "Wyślij link"}
        </Button>
      </FieldGroup>
    </form>
  )
}
