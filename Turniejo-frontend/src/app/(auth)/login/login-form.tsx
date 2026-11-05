"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import Link from "next/link"
import { signIn } from "next-auth/react"
import { useState } from "react"
import { useForm } from "react-hook-form"
import { z } from "zod"

import type { LoginErrorCode } from "@/auth"
import { TextField } from "@/components/forms/text-field"
import { Button } from "@/components/ui/button"
import { FieldError, FieldGroup } from "@/components/ui/field"

const schema = z.object({
  email: z.email("Nieprawidłowy adres email"),
  password: z.string().min(1, "Podaj hasło"),
})

type LoginValues = z.infer<typeof schema>

const LOGIN_ERRORS: Record<LoginErrorCode, string> = {
  // Keycloak po 5 nieudanych próbach blokuje konto czasowo i zwraca wtedy ten sam błąd co przy złym haśle
  // (celowo — nie zdradza, czy konto istnieje), więc uprzedzamy o blokadzie w tym samym komunikacie.
  invalid_credentials:
    "Nieprawidłowy email lub hasło. Po kilku nieudanych próbach logowanie jest blokowane na kilka minut.",
  account_not_ready:
    "Konto wymaga dokończenia konfiguracji - sprawdź skrzynkę email (np. link do ustawienia hasła).",
}

export function LoginForm({ callbackUrl }: { callbackUrl: string }) {
  const [isPending, setIsPending] = useState(false)
  const form = useForm<LoginValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: "", password: "" },
  })
  const { errors } = form.formState

  async function onSubmit(values: LoginValues) {
    setIsPending(true)
    const result = await signIn("credentials", { ...values, redirect: false })

    if (result.error) {
      setIsPending(false)
      const message =
        LOGIN_ERRORS[result.code as LoginErrorCode] ??
        "Nie udało się zalogować. Spróbuj ponownie za chwilę."
      form.setError("root", { message })
      form.resetField("password", { keepError: true })
      return
    }

    // Pełne przejście zamiast router.replace: świeży stan aplikacji (bez cache zapytań poprzedniego
    // konta), a gdy sesja od razu okaże się nieważna i proxy odeśle z powrotem na /login, formularz
    // startuje od nowa zamiast wisieć na "Logowanie…".
    window.location.assign(callbackUrl)
  }

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} noValidate>
      <FieldGroup>
        <TextField
          id="email"
          label="Email"
          type="email"
          autoComplete="email"
          autoFocus
          error={errors.email}
          {...form.register("email")}
        />
        <div className="space-y-2">
          <TextField
            id="password"
            label="Hasło"
            type="password"
            autoComplete="current-password"
            error={errors.password}
            {...form.register("password")}
          />
          <Link
            href="/password-reset"
            className="text-muted-foreground hover:text-foreground block text-right text-sm"
          >
            Nie pamiętasz hasła?
          </Link>
        </div>
        <FieldError errors={[errors.root]} />
        <Button type="submit" size="lg" className="w-full" disabled={isPending}>
          {isPending ? "Logowanie…" : "Zaloguj się"}
        </Button>
      </FieldGroup>
    </form>
  )
}
