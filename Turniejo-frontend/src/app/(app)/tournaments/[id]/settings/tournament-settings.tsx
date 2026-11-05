"use client"

import { FileText, Flag, Globe, ListChecks, Lock, type LucideIcon, Network } from "lucide-react"
import Link from "next/link"
import { type ReactNode, useEffect, useState } from "react"
import { toast } from "sonner"

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useSchedule } from "@/features/matches/api"
import {
  usePublicSettings,
  useTournamentSettings,
  useUpdateTournamentDetails,
} from "@/features/tournaments/api"
import { FormatForm } from "@/features/tournaments/components/format-form"
import { PublicSettingsForm } from "@/features/tournaments/components/public-settings-form"
import { SportSettingsForm } from "@/features/tournaments/components/sport-settings-form"
import { TournamentStatusStepper } from "@/features/tournaments/components/status-stepper"
import { TournamentDetailsForm } from "@/features/tournaments/components/tournament-details-form"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { cn } from "@/lib/utils"
import {
  statusAtLeast,
  TOURNAMENT_FORMAT_LABELS,
  type TournamentDetailsInput,
} from "@/types/tournaments"

type SectionKey = "status" | "details" | "format" | "rules" | "portal"

type SectionMeta = {
  key: SectionKey
  label: string
  /** Krótki stan sekcji w nawigacji, np. wybrany format. */
  summary?: string
  icon: LucideIcon
  locked?: boolean
}

function Section({
  title,
  description,
  lockedReason,
  hidden,
  children,
}: {
  title: string
  description?: string
  /** Gdy ustawione — sekcja jest tylko do odczytu i pokazuje dlaczego. */
  lockedReason?: string | null
  hidden: boolean
  children: ReactNode
}) {
  return (
    // Ukryta sekcja zostaje zamontowana — niezapisane zmiany przeżywają przełączenie sekcji.
    <Card className={cn(hidden && "hidden")}>
      <CardHeader>
        <CardTitle className="text-base">{title}</CardTitle>
        {description && <CardDescription>{description}</CardDescription>}
        {lockedReason && (
          <p className="bg-muted/50 text-muted-foreground mt-2 flex items-center gap-2 rounded-lg border px-3 py-2 text-sm">
            <Lock className="size-4 shrink-0" />
            {lockedReason}
          </p>
        )}
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  )
}

function SectionNav({
  sections,
  active,
  onSelect,
}: {
  sections: SectionMeta[]
  active: SectionKey
  onSelect: (key: SectionKey) => void
}) {
  return (
    <nav aria-label="Sekcje ustawień" className="-mx-4 overflow-x-auto px-4 lg:mx-0 lg:px-0">
      <ul className="flex min-w-max gap-1 lg:min-w-0 lg:flex-col">
        {sections.map(({ key, label, summary, icon: Icon, locked }) => (
          <li key={key}>
            <button
              type="button"
              onClick={() => onSelect(key)}
              aria-current={key === active ? "page" : undefined}
              className={cn(
                "flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm transition-colors",
                key === active
                  ? "bg-primary/10 text-primary font-medium"
                  : "text-muted-foreground hover:bg-accent hover:text-foreground",
              )}
            >
              <Icon className="size-4 shrink-0" />
              <span className="min-w-0">
                <span className="block">{label}</span>
                {summary && (
                  <span className="text-muted-foreground hidden truncate text-xs font-normal lg:block">
                    {summary}
                  </span>
                )}
              </span>
              {locked && (
                <Lock className="ml-auto size-3.5 shrink-0 opacity-60" aria-label="Zablokowane" />
              )}
            </button>
          </li>
        ))}
      </ul>
    </nav>
  )
}

const isSectionKey = (value: string): value is SectionKey =>
  ["status", "details", "format", "rules", "portal"].includes(value)

function sectionFromHash(): SectionKey | null {
  if (typeof window === "undefined") return null
  const hash = window.location.hash.slice(1)
  return isSectionKey(hash) ? hash : null
}

export function TournamentSettingsView() {
  const { id, tournament, isOrganizer } = useTournamentContext()
  const settings = useTournamentSettings(id)
  const schedule = useSchedule(id)
  const updateDetails = useUpdateTournamentDetails(id)
  const publicSettings = usePublicSettings(id, isOrganizer)
  const data = tournament.data!
  // Link może wskazać sekcję (np. "zmień" czas meczu w generatorze → #rules). Widok renderuje się
  // dopiero po załadowaniu turnieju w przeglądarce, więc odczyt hasha nie rozjeżdża hydracji.
  const [active, setActive] = useState<SectionKey>(() => sectionFromHash() ?? "status")
  // Przy przejściu linkiem w aplikacji Next podmienia adres dopiero po pierwszym renderze, więc
  // initializer widzi jeszcze stary adres (bez #details). Czytamy hash ponownie po zamontowaniu
  // i przy każdej jego zmianie.
  useEffect(() => {
    const sync = () => {
      const key = sectionFromHash()
      if (key) setActive(key)
    }
    sync()
    window.addEventListener("hashchange", sync)
    return () => window.removeEventListener("hashchange", sync)
  }, [])

  function select(key: SectionKey) {
    setActive(key)
    window.history.replaceState(null, "", `#${key}`)
  }

  const notOrganizer = isOrganizer ? null : "Tylko organizator może zmieniać ustawienia."
  const isClosed = data.status === "Finished" || data.status === "Cancelled"

  // Blokady 1:1 z domeną backendu (Tournament.SetFormat / UpdateSettings / UpdateDetails).
  const formatLock =
    notOrganizer ??
    (statusAtLeast(data.status, "ScheduleGenerated")
      ? "Formatu nie można zmienić po wygenerowaniu harmonogramu."
      : null)
  const sportLock =
    notOrganizer ??
    (statusAtLeast(data.status, "InProgress")
      ? "Zasad gry nie można zmieniać, gdy turniej trwa lub się zakończył."
      : null)
  const detailsLock =
    notOrganizer ?? (isClosed ? "Zakończonego lub anulowanego turnieju nie można edytować." : null)

  // Czas meczu z zasad gry — bez niego liczymy tylko początek meczu.
  const matchMs = (settings.data?.matchDurationMinutes ?? 0) * 60_000
  const scheduledMatches = (schedule.data ?? [])
    .filter((m) => m.status !== "Cancelled")
    .map((m) => {
      const startAt = new Date(m.scheduledAt).getTime()
      return { startAt, endAt: startAt + matchMs }
    })

  async function saveDetails(input: TournamentDetailsInput) {
    await updateDetails.mutateAsync(input)
    toast.success("Dane turnieju zapisane")
  }

  const sections: SectionMeta[] = [
    { key: "status", label: "Etap turnieju", icon: Flag },
    { key: "details", label: "Dane turnieju", icon: FileText, locked: !!detailsLock },
    {
      key: "format",
      label: "Format rozgrywek",
      summary: data.format ? TOURNAMENT_FORMAT_LABELS[data.format] : "Nie wybrano",
      icon: Network,
      locked: !!formatLock,
    },
    {
      key: "rules",
      label: "Zasady gry",
      summary: settings.data
        ? `Mecz ${settings.data.matchDurationMinutes} min · ${settings.data.pointsForWin}/${settings.data.pointsForDraw}/${settings.data.pointsForLoss} pkt`
        : undefined,
      icon: ListChecks,
      locked: !!sportLock,
    },
    ...(isOrganizer
      ? [
          {
            key: "portal" as const,
            label: "Portal kibica",
            summary: data.isPublic ? "Włączony" : "Wyłączony",
            icon: Globe,
          },
        ]
      : []),
  ]
  // Sekcja z linku może być niedostępna (portal tylko dla organizatora).
  const current = sections.some((s) => s.key === active) ? active : "status"

  return (
    <div className="grid max-w-5xl grid-cols-[minmax(0,1fr)] gap-6 lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start">
      <div className="space-y-3 lg:sticky lg:top-4">
        <SectionNav sections={sections} active={current} onSelect={select} />
        {isOrganizer && data.status === "Draft" && (
          <p className="text-muted-foreground hidden px-3 text-xs lg:block">
            Wolisz po kolei?{" "}
            <Link href={`/tournaments/${id}/setup`} className="text-primary underline">
              Przejdź przez konfigurację krok po kroku
            </Link>
          </p>
        )}
      </div>

      <div>
        <Section
          title="Etap turnieju"
          description="Turniej przechodzi przez kolejne etapy — od szkicu do zakończenia."
          hidden={current !== "status"}
        >
          <TournamentStatusStepper tournament={data} canManage={isOrganizer} />
        </Section>

        <Section
          title="Dane turnieju"
          description="Nazwa, termin i limit drużyn."
          lockedReason={detailsLock}
          hidden={current !== "details"}
        >
          <TournamentDetailsForm
            key={`${data.name}-${data.startAt}-${data.endAt}-${data.teamLimit}`}
            tournament={data}
            submitLabel="Zapisz zmiany"
            pendingLabel="Zapisywanie…"
            disabled={!!detailsLock}
            scheduledMatches={scheduledMatches}
            onSubmit={saveDetails}
          />
        </Section>

        <Section
          title="Format rozgrywek"
          description="Wymagany przed wygenerowaniem harmonogramu."
          lockedReason={formatLock}
          hidden={current !== "format"}
        >
          <FormatForm key={data.format ?? "none"} tournament={data} disabled={!!formatLock} />
        </Section>

        <Section
          title="Zasady gry"
          description="Punktacja, czas meczu, walkower i uprawnienia sędziów."
          lockedReason={sportLock}
          hidden={current !== "rules"}
        >
          {settings.data ? (
            <SportSettingsForm tournamentId={id} settings={settings.data} disabled={!!sportLock} />
          ) : (
            <Skeleton className="h-72" />
          )}
        </Section>

        {isOrganizer && (
          <Section
            title="Portal kibica"
            description="Publiczna strona turnieju: wyniki na żywo, tabele, drabinka i statystyki."
            hidden={current !== "portal"}
          >
            {publicSettings.data ? (
              <PublicSettingsForm
                tournamentId={id}
                slug={data.slug}
                settings={publicSettings.data}
                disabled={data.status === "Cancelled"}
              />
            ) : (
              <Skeleton className="h-72" />
            )}
          </Section>
        )}
      </div>
    </div>
  )
}
