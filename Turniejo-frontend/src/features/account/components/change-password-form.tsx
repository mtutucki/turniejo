"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { signIn, useSession } from "next-auth/react"
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

const EMPTY: Values = { currentPassword: "", newPassword: "", confirmPassword: "" }

export function ChangePasswordForm() {
  const { data: session } = useSession()
  const change = useChangeMyPassword()
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: EMPTY })
  const { errors } = form.formState

  function submit({ currentPassword, newPassword }: Values) {
    change.mutate(
      { currentPassword, newPassword },
      {
        onSuccess: async () => {
          form.reset(EMPTY)
          // Backend zamknął wszystkie sesje (także tę). Logujemy od razu nowym hasłem, żeby user
          // nie wypadł z aplikacji po kilku minutach, gdy wygaśnie token.
          const email = session?.user?.email
          const result = email
            ? await signIn("credentials", { email, password: newPassword, redirect: false })
            : null
          if (result && !result.error) {
            toast.success("Hasło zmienione. Na innych urządzeniach zaloguj się nowym hasłem.")
          } else {
            toast.success("Hasło zmienione. Zaloguj się ponownie nowym hasłem.")
          }
        },
        onError: (error) => applyFieldErrors(error, form),
      },
    )
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Zmiana hasła</CardTitle>
        <CardDescription>
          Po zmianie wylogujemy Cię na innych urządzeniach. Tutaj zostaniesz zalogowany.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={form.handleSubmit(submit)} noValidate>
          <FieldGroup>
            <TextField
              id="currentPassword"
              label="Obecne hasło"
              type="password"
              autoComplete="current-password"
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
            <div>
              <Button type="submit" disabled={change.isPending}>
                {change.isPending ? "Zmienianie…" : "Zmień hasło"}
              </Button>
            </div>
          </FieldGroup>
        </form>
      </CardContent>
    </Card>
  )
}
