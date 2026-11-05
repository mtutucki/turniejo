"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { KeyRound } from "lucide-react"
import { signIn, useSession } from "next-auth/react"
import { useState } from "react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { FieldError, FieldGroup } from "@/components/ui/field"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { PASSWORD_HINT, passwordSchema } from "@/lib/auth/password"

import { useChangeMyPassword } from "../api"

// Limity 1:1 z ChangeMyPasswordCommandValidator (nowe hasło: passwordSchema).
const schema = z
  .object({
    currentPassword: z.string().min(1, "Podaj obecne hasło"),
    newPassword: passwordSchema,
    confirmPassword: z.string(),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    path: ["confirmPassword"],
    message: "Hasła nie są takie same",
  })
  .refine((data) => data.newPassword !== data.currentPassword, {
    path: ["newPassword"],
    message: "Nowe hasło musi się różnić od obecnego",
  })
type Values = z.infer<typeof schema>

function PasswordForm({ onDone }: { onDone: () => void }) {
  const { data: session } = useSession()
  const change = useChangeMyPassword()
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
  })
  const { errors } = form.formState

  function submit({ currentPassword, newPassword }: Values) {
    change.mutate(
      { currentPassword, newPassword },
      {
        onSuccess: async () => {
          // Backend zamknął wszystkie sesje (także tę). Logujemy od razu nowym hasłem, żeby user
          // nie wypadł z aplikacji po kilku minutach, gdy wygaśnie token.
          const email = session?.user?.email
          const result = email
            ? await signIn("credentials", { email, password: newPassword, redirect: false })
            : null
          toast.success(
            result && !result.error
              ? "Hasło zmienione. Na innych urządzeniach zaloguj się nowym hasłem."
              : "Hasło zmienione. Zaloguj się ponownie nowym hasłem.",
          )
          onDone()
        },
        onError: (error) => applyFieldErrors(error, form),
      },
    )
  }

  return (
    <form onSubmit={form.handleSubmit(submit)} noValidate>
      <FieldGroup>
        <TextField
          id="currentPassword"
          label="Obecne hasło"
          type="password"
          autoComplete="current-password"
          autoFocus
          error={errors.currentPassword}
          {...form.register("currentPassword")}
        />
        <TextField
          id="newPassword"
          label="Nowe hasło"
          type="password"
          autoComplete="new-password"
          description={PASSWORD_HINT}
          error={errors.newPassword}
          {...form.register("newPassword")}
        />
        <TextField
          id="confirmPassword"
          label="Powtórz nowe hasło"
          type="password"
          autoComplete="new-password"
          error={errors.confirmPassword}
          {...form.register("confirmPassword")}
        />
        <FieldError errors={[errors.root]} />
        <div className="flex gap-2">
          <Button type="submit" disabled={change.isPending}>
            {change.isPending ? "Zmienianie…" : "Zapisz nowe hasło"}
          </Button>
          <Button type="button" variant="ghost" onClick={onDone}>
            Anuluj
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}

/** Hasło: sam przycisk, formularz dopiero po kliknięciu. */
export function ChangePasswordForm() {
  const [editing, setEditing] = useState(false)

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-4">
        <div className="space-y-1.5">
          <CardTitle className="text-base">Hasło</CardTitle>
          <CardDescription>
            Po zmianie wylogujemy Cię na innych urządzeniach. Tutaj zostaniesz zalogowany.
          </CardDescription>
        </div>
        {!editing && (
          <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
            <KeyRound />
            Zmień hasło
          </Button>
        )}
      </CardHeader>
      {editing && (
        <CardContent>
          <PasswordForm onDone={() => setEditing(false)} />
        </CardContent>
      )}
    </Card>
  )
}
