"use client"

import { Copy, ExternalLink } from "lucide-react"
import Link from "next/link"
import { Controller, useForm, useWatch } from "react-hook-form"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import {
  Field,
  FieldContent,
  FieldDescription,
  FieldGroup,
  FieldLabel,
  FieldSeparator,
} from "@/components/ui/field"
import { Switch } from "@/components/ui/switch"
import type { PublicSettings } from "@/types/public"

import { useUpdatePublicSettings } from "../api"

type ToggleName = Exclude<keyof PublicSettings, "isPublic" | "abbreviatePlayerNames">

const SECTIONS: { name: ToggleName; label: string; description: string }[] = [
  {
    name: "showSchedule",
    label: "Terminarz i wyniki",
    description: "Lista meczów z godzinami, boiskami i wynikami.",
  },
  {
    name: "showLiveMatches",
    label: "Mecze na żywo",
    description: "Minuta i wynik trwających meczów.",
  },
  {
    name: "showStandings",
    label: "Tabele grupowe",
    description: "Aktualizowane po każdym zakończonym meczu.",
  },
  {
    name: "showBracket",
    label: "Drabinka",
    description: "Faza pucharowa (formaty z fazą pucharową).",
  },
  {
    name: "showTeams",
    label: "Drużyny",
    description: "Nazwy drużyn — bez tego portal pokazuje „Gospodarze/Goście”.",
  },
  {
    name: "showPlayers",
    label: "Składy",
    description: "Imiona i numery zawodników w meczach na żywo.",
  },
  { name: "showStats", label: "Statystyki", description: "Rankingi strzelców, asystentów i MVP." },
]

export function PublicSettingsForm({
  tournamentId,
  slug,
  settings,
  disabled,
}: {
  tournamentId: string
  slug: string
  settings: PublicSettings
  disabled: boolean
}) {
  const update = useUpdatePublicSettings(tournamentId)
  const form = useForm<PublicSettings>({ values: settings })
  const isPublic = useWatch({ control: form.control, name: "isPublic" })
  const { isDirty } = form.formState
  const path = `/t/${slug}`

  function submit(values: PublicSettings) {
    update.mutate(values, {
      onSuccess: () =>
        toast.success(values.isPublic ? "Portal kibica włączony" : "Portal kibica wyłączony"),
    })
  }

  async function copyLink() {
    const url = `${window.location.origin}${path}`
    try {
      await navigator.clipboard.writeText(url)
      toast.success("Skopiowano link do portalu")
    } catch {
      toast.info(url)
    }
  }

  const toggle = (name: keyof PublicSettings, label: string, description: string, off = false) => (
    <Controller
      key={name}
      control={form.control}
      name={name}
      render={({ field }) => (
        <Field orientation="horizontal" data-disabled={disabled || off || undefined}>
          <FieldContent>
            <FieldLabel htmlFor={`public-${name}`}>{label}</FieldLabel>
            <FieldDescription>{description}</FieldDescription>
          </FieldContent>
          <Switch
            id={`public-${name}`}
            checked={field.value}
            onCheckedChange={field.onChange}
            disabled={disabled || off}
          />
        </Field>
      )}
    />
  )

  return (
    <form onSubmit={form.handleSubmit(submit)} noValidate>
      <FieldGroup>
        {toggle(
          "isPublic",
          "Turniej publiczny",
          "Kibice i rodzice widzą portal bez logowania. Wyłączony portal zwraca „nie znaleziono”.",
        )}
        {settings.isPublic && (
          <div className="bg-muted/40 flex flex-wrap items-center gap-2 rounded-lg border p-3 text-sm">
            <span className="text-muted-foreground">Adres portalu:</span>
            <code className="font-mono">{path}</code>
            <span className="ml-auto flex gap-1">
              <Button type="button" size="sm" variant="ghost" onClick={copyLink}>
                <Copy />
                Kopiuj
              </Button>
              <Button asChild size="sm" variant="ghost">
                <Link href={path} target="_blank">
                  <ExternalLink />
                  Otwórz
                </Link>
              </Button>
            </span>
          </div>
        )}
        <FieldSeparator>Co pokazać</FieldSeparator>
        {SECTIONS.map(({ name, label, description }) =>
          toggle(name, label, description, !isPublic),
        )}
        <FieldSeparator>Prywatność zawodników</FieldSeparator>
        {toggle(
          "abbreviatePlayerNames",
          "Skracaj nazwiska zawodników",
          "Kibice widzą „Jan K.” zamiast pełnego nazwiska. Wyłącz tylko, jeśli masz zgodę rodziców na publikację pełnych danych.",
          !isPublic,
        )}
        <div>
          <Button type="submit" disabled={disabled || !isDirty || update.isPending}>
            {update.isPending ? "Zapisywanie…" : "Zapisz ustawienia portalu"}
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}
