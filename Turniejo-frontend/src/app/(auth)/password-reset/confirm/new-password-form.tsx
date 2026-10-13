"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { useMutation } from "@tanstack/react-query"
import { CircleCheck } from "lucide-react"
import Link from "next/link"
import { useForm } from "react-hook-form"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
import { Button, buttonVariants } from "@/components/ui/button"
import { FieldError, FieldGroup } from "@/components/ui/field"
import { apiFetch } from "@/lib/api/client"
import { isApiError } from "@/lib/api/errors"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { PASSWORD_HINT, passwordSchema } from "@/lib/auth/password"

// Limity 1:1 z ConfirmPasswordResetCommandValidator (hasło: passwordSchema).
const schema = z
  .object({
    newPassword: passwordSchema,
    confirmPassword: z.string(),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    path: ["confirmPassword"],
    message: "Hasła nie są takie same",
  })

type NewPasswordValues = z.infer<typeof schema>

export function NewPasswordForm({ token }: { token: string }) {
  const form = useForm<NewPasswordValues>({
    resolver: zodResolver(schema),
    defaultValues: { newPassword: "", confirmPassword: "" },
  })
  const { errors } = form.formState

  const confirmReset = useMutation({
    mutationFn: ({ newPassword }: NewPasswordValues) =>
      apiFetch("/api/auth/password-reset/confirm", {
        method: "POST",
        body: { token, newPassword },
      }),
    meta: { handlesValidation: true },
    onError: (error) => applyFieldErrors(error, form),
  })

  // "Validation.Token" = link nieznany, zużyty albo wygasły — formularz nic już nie zdziała.
  const linkRejected =
    isApiError(confirmReset.error) && confirmReset.error.code === "Validation.Token"

  if (confirmReset.isSuccess) {
    return (
      <div className="flex flex-col items-center gap-3 py-2 text-center">
        <CircleCheck className="text-primary size-8" />
        <p className="text-sm">Hasło zostało zmienione. Możesz się zalogować nowym hasłem.</p>
        <Link href="/login" className={buttonVariants({ size: "lg", className: "w-full" })}>
          Zaloguj się
        </Link>
      </div>
    )
  }

  if (linkRejected) {
    return (
      <div className="flex flex-col items-center gap-3 py-2 text-center" role="alert">
        <p className="text-sm">
          Ten link wygasł albo został już użyty. Link działa godzinę i tylko raz.
        </p>
        <Link
          href="/password-reset"
          className={buttonVariants({ size: "lg", className: "w-full" })}
        >
          Wyślij nowy link
        </Link>
      </div>
    )
  }

  return (
    <form onSubmit={form.handleSubmit((values) => confirmReset.mutate(values))} noValidate>
      <FieldGroup>
        <TextField
          id="newPassword"
          label="Nowe hasło"
          type="password"
          autoComplete="new-password"
          autoFocus
          description={PASSWORD_HINT}
          error={errors.newPassword}
          {...form.register("newPassword")}
        />
        <TextField
          id="confirmPassword"
          label="Powtórz hasło"
          type="password"
          autoComplete="new-password"
          error={errors.confirmPassword}
          {...form.register("confirmPassword")}
        />
        <FieldError errors={[errors.root]} />
        <Button type="submit" size="lg" className="w-full" disabled={confirmReset.isPending}>
          {confirmReset.isPending ? "Zapisywanie…" : "Ustaw hasło"}
        </Button>
      </FieldGroup>
    </form>
  )
}
