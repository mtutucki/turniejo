"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { Pencil } from "lucide-react"
import { useSession } from "next-auth/react"
import { useState } from "react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { FieldError, FieldGroup } from "@/components/ui/field"
import { Skeleton } from "@/components/ui/skeleton"
import { applyFieldErrors } from "@/lib/api/form-errors"

import { type MyProfile, useMyProfile, useUpdateMyProfile } from "../api"

// Limity 1:1 z UpdateMyProfileCommandValidator (jak przy rejestracji).
const schema = z.object({
  firstName: z.string().trim().min(1, "Podaj imię").max(100, "Maksymalnie 100 znaków"),
  lastName: z.string().trim().min(1, "Podaj nazwisko").max(100, "Maksymalnie 100 znaków"),
  phoneNumber: z.string().trim().max(20, "Maksymalnie 20 znaków"),
})
type Values = z.infer<typeof schema>

function ProfileDetails({ profile }: { profile: MyProfile }) {
  const rows = [
    { label: "Imię i nazwisko", value: `${profile.firstName} ${profile.lastName}` },
    { label: "E-mail", value: profile.email },
    { label: "Telefon", value: profile.phoneNumber },
  ]
  return (
    <dl className="grid gap-3 text-sm sm:grid-cols-[10rem_1fr]">
      {rows.map(({ label, value }) => (
        <div key={label} className="contents">
          <dt className="text-muted-foreground">{label}</dt>
          <dd className={value ? "font-medium" : "text-muted-foreground"}>
            {value || "Nie podano"}
          </dd>
        </div>
      ))}
    </dl>
  )
}

function ProfileEditForm({ profile, onDone }: { profile: MyProfile; onDone: () => void }) {
  const update = useUpdateMyProfile()
  const { update: updateSession } = useSession()
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      firstName: profile.firstName,
      lastName: profile.lastName,
      phoneNumber: profile.phoneNumber ?? "",
    },
  })
  const { errors } = form.formState

  function submit(values: Values) {
    update.mutate(
      { ...values, phoneNumber: values.phoneNumber || null },
      {
        onSuccess: () => {
          // Imię i nazwisko w sesji (menu, nagłówek) bez ponownego logowania.
          void updateSession({ name: `${values.firstName} ${values.lastName}` })
          toast.success("Dane zapisane")
          onDone()
        },
        onError: (error) => applyFieldErrors(error, form),
      },
    )
  }

  return (
    <form onSubmit={form.handleSubmit(submit)} noValidate>
      <FieldGroup>
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            id="profile-firstName"
            label="Imię"
            autoComplete="given-name"
            autoFocus
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
          value={profile.email}
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
        <div className="flex gap-2">
          <Button type="submit" disabled={update.isPending}>
            {update.isPending ? "Zapisywanie…" : "Zapisz"}
          </Button>
          <Button type="button" variant="ghost" onClick={onDone}>
            Anuluj
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}

/** Dane osobowe: najpierw podgląd, formularz dopiero po "Edytuj". E-mail tylko do odczytu (to login). */
export function ProfileForm() {
  const profile = useMyProfile()
  const [editing, setEditing] = useState(false)

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-4">
        <div className="space-y-1.5">
          <CardTitle className="text-base">Dane osobowe</CardTitle>
          <CardDescription>
            Widzi je organizator turnieju, do którego dołączysz, i sztab Twojej drużyny.
          </CardDescription>
        </div>
        {profile.data && !editing && (
          <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
            <Pencil />
            Edytuj
          </Button>
        )}
      </CardHeader>
      <CardContent>
        {!profile.data ? (
          <Skeleton className="h-24 w-full" />
        ) : editing ? (
          <ProfileEditForm profile={profile.data} onDone={() => setEditing(false)} />
        ) : (
          <ProfileDetails profile={profile.data} />
        )}
      </CardContent>
    </Card>
  )
}
