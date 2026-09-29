"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { useMutation } from "@tanstack/react-query"
import { signIn } from "next-auth/react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
import { Button } from "@/components/ui/button"
import { FieldError, FieldGroup } from "@/components/ui/field"
import { apiFetch } from "@/lib/api/client"
import { applyFieldErrors } from "@/lib/api/form-errors"

// Limity 1:1 z RegisterUserCommandValidator.
const schema = z
  .object({
    firstName: z.string().trim().min(1, "Podaj imię").max(100),
    lastName: z.string().trim().min(1, "Podaj nazwisko").max(100),
    email: z.email("Nieprawidłowy adres email").max(256),
    phoneNumber: z.string().trim().max(20, "Maksymalnie 20 znaków").optional(),
    password: z.string().min(8, "Hasło musi mieć co najmniej 8 znaków").max(100),
    confirmPassword: z.string(),
  })
  .refine((data) => data.password === data.confirmPassword, {
    path: ["confirmPassword"],
    message: "Hasła nie są takie same",
  })

type RegisterValues = z.infer<typeof schema>

export function RegisterForm() {
  const form = useForm<RegisterValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      firstName: "",
      lastName: "",
      email: "",
      phoneNumber: "",
      password: "",
      confirmPassword: "",
    },
  })
  const { errors } = form.formState

  const register = useMutation({
    mutationFn: ({ firstName, lastName, email, password, phoneNumber }: RegisterValues) =>
      apiFetch<{ userId: string }>("/api/auth/register", {
        method: "POST",
        body: { firstName, lastName, email, password, phoneNumber: phoneNumber || null },
      }),
    meta: { handlesValidation: true },
    onError: (error) => applyFieldErrors(error, form.setError),
    onSuccess: (_, { email }) => {
      toast.success("Konto utworzone", { description: "Zaloguj się nowym hasłem." })
      // Keycloak podstawi email w formularzu logowania.
      void signIn("keycloak", { redirectTo: "/dashboard" }, { login_hint: email })
    },
  })

  return (
    <form onSubmit={form.handleSubmit((values) => register.mutate(values))} noValidate>
      <FieldGroup>
        <div className="grid grid-cols-2 gap-4">
          <TextField
            id="firstName"
            label="Imię"
            autoComplete="given-name"
            error={errors.firstName}
            {...form.register("firstName")}
          />
          <TextField
            id="lastName"
            label="Nazwisko"
            autoComplete="family-name"
            error={errors.lastName}
            {...form.register("lastName")}
          />
        </div>
        <TextField
          id="email"
          label="Email"
          type="email"
          autoComplete="email"
          error={errors.email}
          {...form.register("email")}
        />
        <TextField
          id="phoneNumber"
          label="Telefon (opcjonalnie)"
          type="tel"
          autoComplete="tel"
          error={errors.phoneNumber}
          {...form.register("phoneNumber")}
        />
        <TextField
          id="password"
          label="Hasło"
          type="password"
          autoComplete="new-password"
          error={errors.password}
          {...form.register("password")}
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
        <Button type="submit" size="lg" className="w-full" disabled={register.isPending}>
          {register.isPending ? "Tworzenie konta…" : "Załóż konto"}
        </Button>
      </FieldGroup>
    </form>
  )
}
