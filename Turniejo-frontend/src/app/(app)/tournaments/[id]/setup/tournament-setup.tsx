"use client"

import { Check, Layers, type LucideIcon, MapPin, PartyPopper, Shield } from "lucide-react"
import Link from "next/link"
import { useRef, useState } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { usePublicSettings, useTournamentSettings } from "@/features/tournaments/api"
import { FormatForm } from "@/features/tournaments/components/format-form"
import { PublicSettingsForm } from "@/features/tournaments/components/public-settings-form"
import { SportSettingsForm } from "@/features/tournaments/components/sport-settings-form"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { cn } from "@/lib/utils"
import { statusAtLeast } from "@/types/tournaments"

const STEPS = [
  {
    title: "Format rozgrywek",
    description:
      "Jak drużyny będą ze sobą grać. Format możesz zmienić do czasu wygenerowania terminarza.",
  },
  {
    title: "Zasady gry",
    description:
      "Punktacja i czas meczu. Domyślne wartości pasują do większości turniejów — zmień tylko to, co trzeba.",
  },
  {
    title: "Portal kibica",
    description: "Publiczna strona z wynikami na żywo dla rodziców i kibiców, bez logowania.",
  },
  { title: "Gotowe", description: "Podstawy ustawione. Co dalej?" },
]

function StepIndicator({ current, onSelect }: { current: number; onSelect: (i: number) => void }) {
  return (
    <ol className="flex flex-wrap gap-x-2 gap-y-3" aria-label="Kroki konfiguracji">
      {STEPS.map((step, index) => {
        const done = index < current
        return (
          <li key={step.title} className="flex items-center gap-2">
            <button
              type="button"
              // Wracać można zawsze; do przodu prowadzą przyciski kroku.
              disabled={index > current}
              onClick={() => onSelect(index)}
              aria-current={index === current ? "step" : undefined}
              className={cn(
                "flex items-center gap-2 rounded-full py-1 pr-3 pl-1 text-sm font-medium transition-colors",
                index === current ? "bg-primary/10 text-primary" : "text-muted-foreground",
                done && "hover:text-foreground",
              )}
            >
              <span
                className={cn(
                  "flex size-6 items-center justify-center rounded-full border font-mono text-xs tabular-nums",
                  index === current && "border-primary",
                  done && "bg-primary text-primary-foreground border-primary",
                )}
              >
                {done ? <Check className="size-3.5" aria-label="Zrobione" /> : index + 1}
              </span>
              {step.title}
            </button>
            {index < STEPS.length - 1 && (
              <span className="bg-border hidden h-px w-4 sm:block" aria-hidden />
            )}
          </li>
        )
      })}
    </ol>
  )
}

function NextAction({
  href,
  icon: Icon,
  title,
  hint,
}: {
  href: string
  icon: LucideIcon
  title: string
  hint: string
}) {
  return (
    <li>
      <Link
        href={href}
        className="hover:bg-accent flex items-center gap-3 rounded-lg border p-3 transition-colors"
      >
        <Icon className="text-primary size-5 shrink-0" />
        <span>
          <span className="block text-sm font-medium">{title}</span>
          <span className="text-muted-foreground block text-xs">{hint}</span>
        </span>
      </Link>
    </li>
  )
}

/**
 * Kreator po utworzeniu turnieju: te same formularze co w Ustawieniach, ale po jednym na ekran
 * i z przyciskiem "Zapisz i dalej". Każdy krok można pominąć — wszystko da się zmienić później.
 */
export function TournamentSetupView() {
  const { id, tournament, isOrganizer, rolesLoading } = useTournamentContext()
  const settings = useTournamentSettings(id)
  const publicSettings = usePublicSettings(id, isOrganizer)
  const [step, setStep] = useState(0)
  const top = useRef<HTMLDivElement>(null)
  const data = tournament.data!
  const base = `/tournaments/${id}`

  if (rolesLoading) return <Skeleton className="h-72 max-w-3xl rounded-xl" />
  if (!isOrganizer) {
    return (
      <EmptyState
        icon={Shield}
        title="Konfigurację prowadzi organizator"
        description="Tylko organizator może zmieniać ustawienia turnieju."
      />
    )
  }

  // Kolejny krok zaczyna się od góry — poprzedni formularz mógł być przewinięty do przycisku.
  function goTo(index: number) {
    setStep(Math.max(0, Math.min(index, STEPS.length - 1)))
    top.current?.scrollIntoView({ block: "start" })
  }
  const next = () => goTo(step + 1)
  const current = STEPS[step]!
  const isLast = step === STEPS.length - 1
  // Te same blokady co w Ustawieniach (domena backendu).
  const formatLocked = statusAtLeast(data.status, "ScheduleGenerated")
  const rulesLocked = statusAtLeast(data.status, "InProgress")

  return (
    <div ref={top} className="max-w-3xl scroll-mt-20 space-y-6">
      <StepIndicator current={step} onSelect={goTo} />

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            {isLast && <PartyPopper className="text-primary size-4" />}
            {current.title}
          </CardTitle>
          <CardDescription>{current.description}</CardDescription>
        </CardHeader>
        <CardContent>
          {step === 0 && <FormatForm tournament={data} disabled={formatLocked} onSaved={next} />}
          {step === 1 &&
            (settings.data ? (
              <SportSettingsForm
                tournamentId={id}
                settings={settings.data}
                disabled={rulesLocked}
                onSaved={next}
              />
            ) : (
              <Skeleton className="h-72" />
            ))}
          {step === 2 &&
            (publicSettings.data ? (
              <PublicSettingsForm
                tournamentId={id}
                slug={data.slug}
                settings={publicSettings.data}
                disabled={data.status === "Cancelled"}
                onSaved={next}
              />
            ) : (
              <Skeleton className="h-72" />
            ))}
          {isLast && (
            <div className="space-y-4">
              <ul className="grid gap-2 sm:grid-cols-3">
                <NextAction
                  href={`${base}/categories`}
                  icon={Layers}
                  title={data.format === "Knockout" ? "Kategorie" : "Kategorie i grupy"}
                  hint="Podział wiekowy drużyn"
                />
                <NextAction
                  href={`${base}/pitches`}
                  icon={MapPin}
                  title="Boiska"
                  hint="Potrzebne do terminarza"
                />
                <NextAction
                  href={`${base}/teams`}
                  icon={Shield}
                  title="Drużyny"
                  hint="Zgłoszenia i składy"
                />
              </ul>
              <Button asChild>
                <Link href={`${base}/dashboard`}>Przejdź do przeglądu turnieju</Link>
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      {!isLast && (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex gap-2">
            {step > 0 && (
              <Button variant="ghost" size="sm" onClick={() => goTo(step - 1)}>
                Wstecz
              </Button>
            )}
            <Button variant="ghost" size="sm" onClick={next}>
              Pomiń ten krok
            </Button>
          </div>
          <Button asChild variant="link" size="sm" className="text-muted-foreground">
            <Link href={`${base}/dashboard`}>Pomiń konfigurację</Link>
          </Button>
        </div>
      )}
    </div>
  )
}
