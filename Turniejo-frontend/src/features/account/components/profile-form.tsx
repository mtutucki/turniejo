"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { useSession } from "next-auth/react"
import { useEffect } from "react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { FieldError, FieldGroup } from "@/components/ui/field"
import { Skeleton } from "@/components/ui/skeleton"
import { applyFieldErrors } from "@/lib/api/form-errors"

import { useMyProfile, useUpdateMyProfile } from "../api"

// Limity 1:1 z UpdateMyProfileCommandValidator (jak przy rejestracji).
const schema = z.object({
  firstName: z.string().trim().min(1, "Podaj imię").max(100, "Maksymalnie 100 znaków"),
  lastName: z.string().trim().min(1, "Podaj nazwisko").max(100, "Maksymalnie 100 znaków"),
  phoneNumber: z.string().trim().max(20, "Maksymalnie 20 znaków"),
})
type Values = z.infer<typeof schema>

/** Dane osobowe: imię, nazwisko i telefon do edycji, e-mail tylko do odczytu (to login). */
export function ProfileForm() {
  const profile = useMyProfile()
  const update = useUpdateMyProfile()
  const { update: updateSession } = useSession()
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { firstName: "", lastName: "", phoneNumber: "" },
  })
  const { errors, isDirty } = form.formState

  useEffect(() => {
    if (!profile.data) return
    form.reset({
      firstName: profile.data.firstName,
      lastName: profile.data.lastName,
      phoneNumber: profile.data.phoneNumber ?? "",
    })
  }, [profile.data, form])

  function submit(values: Values) {
    update.mutate(
      { ...values, phoneNumber: values.phoneNumber || null },
      {
        onSuccess: () => {
          // Imię i nazwisko w sesji (menu, nagłówek) bez ponownego logowania.
          void updateSession({ name: `${values.firstName} ${values.lastName}` })
          form.reset(values)
          toast.success("Dane zapisane")
        },
        onError: (error) => applyFieldErrors(error, form),
      },
    )
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Dane osobowe</CardTitle>
        <CardDescription>
          Widzi je organizator turnieju, do którego dołączysz, i sztab Twojej drużyny.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {profile.isPending ? (
          <Skeleton className="h-56 w-full" />
        ) : (
          <form onSubmit={form.handleSubmit(submit)} noValidate>
            <FieldGroup>
              <div className="grid gap-4 sm:grid-cols-2">
                <TextField
                  id="profile-firstName"
                  label="Imię"
                  autoComplete="given-name"
                  error={errors.firstName}
                  {...form.register("firstName")}
                />
                <TextField
                  id="profile-lastName"
                  label="Nazwisko"
                  autoComplete="family-name"
                  error={errors.lastName}
                  {...form.register("lastName")}
                />
              </div>
              <TextField
                id="profile-email"
                label="E-mail"
                value={profile.data?.email ?? ""}
                readOnly
                disabled
                description="Tym adresem się logujesz, więc nie można go tu zmienić."
              />
              <TextField
                id="profile-phoneNumber"
                label="Telefon (opcjonalnie)"
                type="tel"
                autoComplete="tel"
                error={errors.phoneNumber}
                {...form.register("phoneNumber")}
              />
              <FieldError errors={[errors.root]} />
              <div>
                <Button type="submit" disabled={!isDirty || update.isPending}>
                  {update.isPending ? "Zapisywanie…" : "Zapisz zmiany"}
                </Button>
              </div>
            </FieldGroup>
          </form>
        )}
      </CardContent>
    </Card>
  )
}
