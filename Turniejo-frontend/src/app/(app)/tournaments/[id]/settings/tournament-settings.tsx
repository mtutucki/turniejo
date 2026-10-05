"use client"

import { Lock } from "lucide-react"
import type { ReactNode } from "react"
import { toast } from "sonner"

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
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
import { statusAtLeast, type TournamentDetailsInput } from "@/types/tournaments"

function Section({
  title,
  description,
  lockedReason,
  children,
}: {
  title: string
  description?: string
  /** Gdy ustawione — sekcja jest tylko do odczytu i pokazuje dlaczego. */
  lockedReason?: string | null
  children: ReactNode
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{title}</CardTitle>
        {description && <CardDescription>{description}</CardDescription>}
        {lockedReason && (
          <p className="text-muted-foreground flex items-center gap-2 pt-1 text-sm">
            <Lock className="size-4 shrink-0" />
            {lockedReason}
          </p>
        )}
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  )
}

export function TournamentSettingsView() {
  const { id, tournament, isOrganizer } = useTournamentContext()
  const settings = useTournamentSettings(id)
  const updateDetails = useUpdateTournamentDetails(id)
  const publicSettings = usePublicSettings(id, isOrganizer)
  const data = tournament.data!

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
      ? "Ustawień sportowych nie można zmieniać, gdy turniej trwa lub się zakończył."
      : null)
  const detailsLock =
    notOrganizer ?? (isClosed ? "Zakończonego lub anulowanego turnieju nie można edytować." : null)

  async function saveDetails(input: TournamentDetailsInput) {
    await updateDetails.mutateAsync(input)
    toast.success("Dane turnieju zapisane")
  }

  return (
    <div className="max-w-3xl space-y-6">
      <Section title="Status" description="Kolejne etapy turnieju — nie można ich cofać.">
        <TournamentStatusStepper tournament={data} canManage={isOrganizer} />
      </Section>

      <Section
        title="Format rozgrywek"
        description="Wymagany przed wygenerowaniem harmonogramu."
        lockedReason={formatLock}
      >
        <FormatForm key={data.format ?? "none"} tournament={data} disabled={!!formatLock} />
      </Section>

      <Section title="Dane turnieju" lockedReason={detailsLock}>
        <TournamentDetailsForm
          key={`${data.name}-${data.startAt}-${data.endAt}-${data.teamLimit}`}
          tournament={data}
          submitLabel="Zapisz zmiany"
          pendingLabel="Zapisywanie…"
          disabled={!!detailsLock}
          onSubmit={saveDetails}
        />
      </Section>

      <Section title="Ustawienia sportowe" lockedReason={sportLock}>
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
  )
}
