"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { useMutation } from "@tanstack/react-query"
import { type LucideIcon, Ticket, Trophy } from "lucide-react"
import { useRouter } from "next/navigation"
import { signIn } from "next-auth/react"
import Link from "next/link"
import { Controller, useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { markOrganizationProvisioning } from "@/features/tournaments/api"
import { apiFetch } from "@/lib/api/client"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { PASSWORD_HINT, passwordSchema } from "@/lib/auth/password"
import { DEFAULT_APP_PATH } from "@/lib/auth/public-paths"
import { cn } from "@/lib/utils"

// Limity 1:1 z RegisterUserCommandValidator (hasło: passwordSchema).
const schema = z
  .object({
    firstName: z.string().trim().min(1, "Podaj imię").max(100),
    lastName: z.string().trim().min(1, "Podaj nazwisko").max(100),
    email: z.email("Nieprawidłowy adres email").max(256),
    phoneNumber: z.string().trim().max(20, "Maksymalnie 20 znaków").optional(),
    password: passwordSchema,
    confirmPassword: z.string(),
    accountType: z.enum(["organizer", "participant"], {
      error: "Wybierz, jak chcesz korzystać z Turniejo",
    }),
    // Obowiązkowa akceptacja Regulaminu (umowa o świadczenie usług) — backend zapisuje datę i wersję.
    acceptTerms: z.boolean().refine((value) => value, {
      message: "Zaakceptuj Regulamin, aby założyć konto",
    }),
  })
  .refine((data) => data.password === data.confirmPassword, {
    path: ["confirmPassword"],
    message: "Hasła nie są takie same",
    // Domyślnie zod pomija refine, gdy inne pole (np. niewybrany typ konta) jest błędne — a user
    // ma od razu zobaczyć wszystkie problemy. Wystarczy, że oba hasła są stringami.
    when: ({ value }) => {
      const v = value as { password?: unknown; confirmPassword?: unknown }
      return typeof v.password === "string" && typeof v.confirmPassword === "string"
    },
  })

type RegisterValues = z.infer<typeof schema>
type AccountType = RegisterValues["accountType"]

const ACCOUNT_TYPES: {
  value: AccountType
  title: string
  description: string
  icon: LucideIcon
}[] = [
  {
    value: "participant",
    title: "Dołączam do turnieju",
    description: "Rodzic, trener albo sędzia — z kodem od organizatora.",
    icon: Ticket,
  },
  {
    value: "organizer",
    title: "Organizuję turnieje",
    description: "Tworzę turnieje, drużyny, terminarz i wyniki.",
    icon: Trophy,
  },
]

type RegisterFormProps = {
  /** Cel po rejestracji (np. /join?code=… z linku zaproszenia); null = strona startowa. */
  callbackUrl: string | null
  defaultAccountType?: AccountType
}

export function RegisterForm({ callbackUrl, defaultAccountType }: RegisterFormProps) {
  const form = useForm<RegisterValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      firstName: "",
      lastName: "",
      email: "",
      phoneNumber: "",
      password: "",
      confirmPassword: "",
      // Bez domyślnego wyboru (poza linkiem zaproszenia) — user świadomie decyduje, czy organizuje.
      accountType: defaultAccountType as AccountType,
      acceptTerms: false,
    },
  })
  const { errors } = form.formState
  const router = useRouter()

  const register = useMutation({
    mutationFn: ({
      firstName,
      lastName,
      email,
      password,
      phoneNumber,
      accountType,
    }: RegisterValues) =>
      apiFetch<{ userId: string }>("/api/auth/register", {
        method: "POST",
        body: {
          firstName,
          lastName,
          email,
          password,
          phoneNumber: phoneNumber || null,
          // Organizacja (i rola organizatora) tylko dla organizatora — reszta może ją założyć później.
          createOrganization: accountType === "organizer",
          acceptTerms: true,
        },
      }),
    meta: { handlesValidation: true },
    onError: (error) => applyFieldErrors(error, form),
    onSuccess: async (_, { email, password, accountType }) => {
      const result = await signIn("credentials", { email, password, redirect: false })
      if (result.error) {
        // Konto istnieje, ale automatyczne logowanie się nie udało — niech user zaloguje się ręcznie.
        toast.success("Konto utworzone", { description: "Zaloguj się, aby kontynuować." })
        router.replace(
          callbackUrl ? `/login?callbackUrl=${encodeURIComponent(callbackUrl)}` : "/login",
        )
        return
      }
      toast.success("Witaj w Turniejo!")
      if (accountType === "organizer") markOrganizationProvisioning()
      // Organizator od razu do listy turniejów (organizacja dojdzie za chwilę z outboxa);
      // uczestnik do celu z zaproszenia albo na stronę startową z instrukcją.
      router.replace(
        accountType === "organizer" ? "/tournaments" : (callbackUrl ?? DEFAULT_APP_PATH),
      )
      router.refresh()
    },
  })

  return (
    <form onSubmit={form.handleSubmit((values) => register.mutate(values))} noValidate>
      <FieldGroup>
        <fieldset className="space-y-2">
          <legend className="mb-2 text-sm font-medium">Jak chcesz korzystać z Turniejo?</legend>
          <div className="grid gap-2 sm:grid-cols-2">
            {ACCOUNT_TYPES.map(({ value, title, description, icon: Icon }) => (
              <label
                key={value}
                className={cn(
                  "has-[:checked]:border-primary has-[:checked]:bg-primary/10 has-[:focus-visible]:ring-ring flex cursor-pointer gap-3 rounded-lg border p-3 transition-colors has-[:focus-visible]:ring-2",
                  errors.accountType && "border-destructive",
                )}
              >
                <input
                  type="radio"
                  value={value}
                  className="sr-only"
                  aria-describedby={`account-${value}-desc`}
                  {...form.register("accountType")}
                />
                <Icon className="text-primary mt-0.5 size-5 shrink-0" />
                <span>
                  <span className="block text-sm font-medium">{title}</span>
                  <span
                    id={`account-${value}-desc`}
                    className="text-muted-foreground block text-xs"
                  >
                    {description}
                  </span>
                </span>
              </label>
            ))}
          </div>
          <FieldError errors={[errors.accountType]} />
        </fieldset>
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
          description={PASSWORD_HINT}
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
        <Controller
          control={form.control}
          name="acceptTerms"
          render={({ field }) => (
            <Field orientation="horizontal" data-invalid={!!errors.acceptTerms || undefined}>
              <Checkbox
                id="acceptTerms"
                checked={field.value}
                onCheckedChange={(checked) => field.onChange(checked === true)}
                aria-invalid={!!errors.acceptTerms}
                aria-describedby={errors.acceptTerms ? "acceptTerms-error" : undefined}
              />
              <FieldLabel htmlFor="acceptTerms" className="block text-sm leading-snug font-normal">
                Akceptuję{" "}
                <Link href="/terms" target="_blank" className="text-primary font-medium underline">
                  Regulamin
                </Link>{" "}
                i zapoznałem się z{" "}
                <Link
                  href="/privacy"
                  target="_blank"
                  className="text-primary font-medium underline"
                >
                  Polityką prywatności
                </Link>
              </FieldLabel>
            </Field>
          )}
        />
        <FieldError id="acceptTerms-error" errors={[errors.acceptTerms]} />
        <FieldError errors={[errors.root]} />
        <Button
          type="submit"
          size="lg"
          className="w-full"
          disabled={register.isPending || register.isSuccess}
        >
          {/* Po utworzeniu konta trwa jeszcze logowanie i przekierowanie — przycisk nie może wyglądać na bezczynny. */}
          {register.isPending
            ? "Tworzenie konta…"
            : register.isSuccess
              ? "Konfigurujemy Twoje konto…"
              : "Załóż konto"}
        </Button>
      </FieldGroup>
    </form>
  )
}
