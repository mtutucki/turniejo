"use client"

import { CalendarCheck, GitBranch, RefreshCw, Trash2, Trophy } from "lucide-react"
import Link from "next/link"
import { useSearchParams } from "next/navigation"
import { useState } from "react"
import { toast } from "sonner"

import { EmptyState } from "@/components/layout/empty-state"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import {
  useBracket,
  usePitches,
  useRecalculateBracket,
  useResetBracket,
  useSchedule,
  useSwapBracketSlots,
} from "@/features/matches/api"
import { BracketBoard } from "@/features/matches/components/bracket-board"
import { GenerateBracketForm } from "@/features/matches/components/generate-bracket-form"
import { useTournamentMatchesRealtime } from "@/features/matches/use-tournament-matches-realtime"
import { autoBracketPairs } from "@/features/matches/suggest-bracket"
import {
  bracketChampion,
  bracketThirdPlace,
  showsChampion,
} from "@/features/public/components/champion-banner"
import { useTournamentTeams } from "@/features/teams/api"
import { useCategories, useGroups, useTournamentSettings } from "@/features/tournaments/api"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { CategoryPicker } from "@/features/tournaments/components/category-picker"
import { cn } from "@/lib/utils"
import type { BracketSlotRef } from "@/types/public"

function CategoryBracket({ categoryId }: { categoryId: string }) {
  const { id, isOrganizer, tournament } = useTournamentContext()
  const tournamentFormat = tournament.data?.format
  const tournamentStatus = tournament.data?.status
  const bracket = useBracket(id, categoryId)
  const schedule = useSchedule(id)
  const pitches = usePitches(id)
  const groups = useGroups(id)
  const teams = useTournamentTeams(id)
  const settings = useTournamentSettings(id)
  const resetBracket = useResetBracket(id)
  const swapSlots = useSwapBracketSlots(id)
  const recalculate = useRecalculateBracket(id)
  const [editingPairs, setEditingPairs] = useState(false)
  const [selectedSlot, setSelectedSlot] = useState<BracketSlotRef | null>(null)

  if (bracket.isPending || groups.isPending || teams.isPending) {
    return <Skeleton className="h-64 w-full rounded-xl" />
  }

  // Puchar: pary tylko z drużyn — grupy mogą zostać po zmianie formatu, ale tu nie grają.
  const categoryGroups =
    tournamentFormat === "Knockout"
      ? []
      : (groups.data ?? []).filter((g) => g.categoryId === categoryId)
  const teamById = new Map((teams.data ?? []).map((t) => [t.id, t.teamName]))
  const groupById = new Map((groups.data ?? []).map((g) => [g.id, g.name]))
  const pitchById = new Map((pitches.data ?? []).map((p) => [p.id, p.name]))

  if (bracket.data) {
    // Puchar: drabinka gotowa, ale mecze 1. rundy nie mają jeszcze godzin — następny krok to terminarz.
    const needsSchedule =
      isOrganizer && tournamentFormat === "Knockout" && tournamentStatus === "RegistrationClosed"
    const champion = showsChampion(tournamentStatus, tournamentFormat)
      ? bracketChampion(bracket.data, schedule.data ?? [])
      : null
    const thirdPlace = champion ? bracketThirdPlace(bracket.data, schedule.data ?? []) : null
    // Drużyna, która już nie gra (wycofana, odrzucona albo usunięta z turnieju), zostaje w swojej parze
    // na stałe (węzły mają zamrożone drużyny), a slot w kolejnej rundzie nigdy by się nie wypełnił —
    // jedyne wyjście to ułożyć drabinkę od nowa, póki nic nie zagrano.
    const nodes = bracket.data.rounds.flatMap((r) => r.nodes)
    const acceptedIds = new Set(
      (teams.data ?? [])
        .filter((t) => t.categoryId === categoryId && t.registrationStatus === "Accepted")
        .map((t) => t.id),
    )
    const bracketTeamIds = new Set(
      nodes
        .flatMap((n) => [n.homeTournamentTeamId, n.awayTournamentTeamId])
        .filter((teamId): teamId is string => !!teamId),
    )
    const goneFromBracket = [...bracketTeamIds].filter((teamId) => !acceptedIds.has(teamId))
    // Puchar układa pary z konkretnych drużyn — zaakceptowana później nie ma w drabince miejsca.
    const missingFromBracket =
      tournamentFormat === "Knockout"
        ? [...acceptedIds].filter((teamId) => !bracketTeamIds.has(teamId))
        : []
    const bracketMatchIds = new Set(nodes.map((n) => n.matchId).filter(Boolean))
    const anyStarted = (schedule.data ?? []).some(
      (m) =>
        bracketMatchIds.has(m.id) &&
        (m.status === "Live" || m.status === "Paused" || m.status === "Finished"),
    )
    // Drabinka ułożona z miejsc w grupach, a format zmieniono potem na pucharowy: grup już nikt nie
    // rozegra, więc pary nigdy nie dostaną drużyn ani meczów, a terminarz nie ma czego rozstawić.
    const groupSourcesInKnockout =
      tournamentFormat === "Knockout" &&
      nodes.some(
        (n) => n.homeSourceType === "GroupPosition" || n.awaySourceType === "GroupPosition",
      )
    const needsRegeneration =
      isOrganizer &&
      (goneFromBracket.length > 0 || missingFromBracket.length > 0 || groupSourcesInKnockout) &&
      !anyStarted
    // Zamiana par ma sens, dopóki nic nie zagrano — potem trzeba by cofać awanse i wyniki.
    const canEditPairs = isOrganizer && !anyStarted && tournamentStatus !== "Finished"
    const editing = editingPairs && canEditPairs
    // Puchar po wygenerowaniu terminarza: usunięcie drabinki zabiera też terminarz (backend cofa etap).
    // Gdy inne kategorie mają jeszcze mecze, terminarz zostaje (backend cofa etap tylko przy ostatniej).
    const scheduledKnockout =
      tournamentFormat === "Knockout" &&
      tournamentStatus === "ScheduleGenerated" &&
      !(schedule.data ?? []).some((m) => m.categoryId !== categoryId && m.status !== "Cancelled")

    function selectSlot(slot: BracketSlotRef) {
      if (!selectedSlot) return setSelectedSlot(slot)
      setSelectedSlot(null)
      // Ponowne kliknięcie tego samego slotu cofa wybór.
      if (selectedSlot.nodeId === slot.nodeId && selectedSlot.slot === slot.slot) return
      swapSlots.mutate(
        { categoryId, first: selectedSlot, second: slot },
        { onSuccess: () => toast.success("Pary zmienione") },
      )
    }

    return (
      <div className="space-y-4">
        {canEditPairs && (
          <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
            <p className="text-muted-foreground" role={editing ? "status" : undefined}>
              {editing &&
                (selectedSlot
                  ? "Teraz kliknij drugi slot - zamienią się miejscami."
                  : "Kliknij dwa sloty pierwszej rundy, żeby zamienić je miejscami. Terminy meczów zostają.")}
            </p>
            <div className="flex flex-wrap gap-2">
              {!editing && (
                <AlertDialog>
                  <AlertDialogTrigger asChild>
                    <Button
                      size="sm"
                      variant="outline"
                      className="text-destructive hover:text-destructive"
                    >
                      <Trash2 />
                      Usuń drabinkę
                    </Button>
                  </AlertDialogTrigger>
                  <AlertDialogContent>
                    <AlertDialogHeader>
                      <AlertDialogTitle>Usunąć drabinkę?</AlertDialogTitle>
                      <AlertDialogDescription>
                        {scheduledKnockout
                          ? "Znikną pary i mecze drabinki z terminarza, a turniej wróci do etapu „Zapisy zamknięte”. Potem ułożysz pary i wygenerujesz terminarz od nowa."
                          : "Znikną pary i mecze drabinki. Potem ułożysz pary od nowa."}
                      </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                      <AlertDialogCancel>Anuluj</AlertDialogCancel>
                      <AlertDialogAction
                        variant="destructive"
                        disabled={resetBracket.isPending}
                        onClick={() =>
                          resetBracket.mutate(
                            { categoryId },
                            { onSuccess: () => toast.success("Drabinka usunięta") },
                          )
                        }
                      >
                        Usuń drabinkę
                      </AlertDialogAction>
                    </AlertDialogFooter>
                  </AlertDialogContent>
                </AlertDialog>
              )}
              <Button
                size="sm"
                variant={editing ? "default" : "outline"}
                onClick={() => {
                  setEditingPairs(!editing)
                  setSelectedSlot(null)
                }}
              >
                {editing ? "Gotowe" : "Edytuj pary"}
              </Button>
            </div>
          </div>
        )}
        {isOrganizer && anyStarted && (
          <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
            <p className="text-muted-foreground">
              Poprawiony wynik nie trafił dalej? Przelicz drabinkę.
            </p>
            <Button
              size="sm"
              variant="outline"
              disabled={recalculate.isPending}
              onClick={() =>
                recalculate.mutate(
                  { categoryId },
                  {
                    onSuccess: ({ changed, blockedMatchIds }) => {
                      if (blockedMatchIds.length > 0) {
                        toast.warning(
                          blockedMatchIds.length === 1
                            ? "Kolejny mecz już się zaczął, więc nie dało się w nim zmienić drużyny."
                            : `Kolejne mecze (${blockedMatchIds.length}) już się zaczęły, więc nie dało się w nich zmienić drużyn.`,
                        )
                      } else {
                        toast.success(changed ? "Drabinka przeliczona" : "Drabinka jest aktualna")
                      }
                    },
                  },
                )
              }
            >
              <RefreshCw className={cn(recalculate.isPending && "animate-spin")} />
              Przelicz drabinkę
            </Button>
          </div>
        )}
        {needsRegeneration && (
          <div
            role="alert"
            className="border-destructive/40 bg-destructive/10 flex flex-wrap items-center justify-between gap-3 rounded-xl border px-4 py-3 text-sm"
          >
            <p>
              {groupSourcesInKnockout
                ? "Ta drabinka powstała z miejsc w grupach, a turniej ma teraz format pucharowy bez grup."
                : goneFromBracket.length > 0
                  ? `W drabince jest drużyna, która już nie gra w turnieju (${goneFromBracket
                      .map((teamId) => teamById.get(teamId) ?? "usunięta drużyna")
                      .join(", ")}).`
                  : `Drabinka nie obejmuje wszystkich zaakceptowanych drużyn (brakuje: ${missingFromBracket
                      .map((teamId) => teamById.get(teamId) ?? "Drużyna")
                      .join(", ")}).`}{" "}
              Drabinka zostanie wygenerowana jeszcze raz - pary ułożysz od nowa.
            </p>
            <Button
              size="sm"
              disabled={resetBracket.isPending}
              onClick={() =>
                resetBracket.mutate(
                  { categoryId },
                  { onSuccess: () => toast.success("Drabinka usunięta - ułóż pary od nowa") },
                )
              }
            >
              Wygeneruj drabinkę od nowa
            </Button>
          </div>
        )}
        {needsSchedule && !needsRegeneration && (
          <div
            role="status"
            className="bg-primary/10 border-primary/40 flex flex-wrap items-center justify-between gap-3 rounded-xl border px-4 py-3 text-sm"
          >
            <p>Pary gotowe. Teraz wygeneruj terminarz - mecze 1. rundy dostaną godziny i boiska.</p>
            <Button asChild size="sm">
              <Link href={`/tournaments/${id}/schedule/generate`}>Generuj terminarz</Link>
            </Button>
          </div>
        )}
        {champion && (
          <div
            role="status"
            className="flex items-center gap-3 rounded-xl border border-yellow-400/40 bg-yellow-400/10 p-4"
          >
            <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-yellow-400/20">
              <Trophy className="size-5 text-yellow-700 dark:text-yellow-500" aria-hidden />
            </span>
            <span className="min-w-0">
              <span className="text-muted-foreground block text-xs font-medium tracking-wide uppercase">
                Zwycięzca turnieju
              </span>
              <span className="block truncate text-lg font-bold">
                {teamById.get(champion) ?? "Drużyna"}
              </span>
              {thirdPlace && (
                <span className="text-muted-foreground block truncate text-sm">
                  3. miejsce: {teamById.get(thirdPlace) ?? "Drużyna"}
                </span>
              )}
            </span>
          </div>
        )}
        <BracketBoard
          bracket={bracket.data}
          matches={schedule.data ?? []}
          lookups={{
            teamName: (teamId) => teamById.get(teamId) ?? "Drużyna",
            groupName: (groupId) => (groupId ? (groupById.get(groupId) ?? null) : null),
            pitchName: (pitchId) => (pitchId ? (pitchById.get(pitchId) ?? null) : null),
          }}
          // Puchar przed terminarzem: godziny meczów z drabinki to zaślepki, nie obowiązujące terminy.
          showTimes={
            !(
              tournamentFormat === "Knockout" &&
              (tournamentStatus === "Draft" ||
                tournamentStatus === "RegistrationOpen" ||
                tournamentStatus === "RegistrationClosed")
            )
          }
          swap={
            editing
              ? { selected: selectedSlot, disabled: swapSlots.isPending, onSelect: selectSlot }
              : undefined
          }
        />
      </div>
    )
  }

  if (!isOrganizer) {
    return (
      <EmptyState
        icon={GitBranch}
        title="Drabinka jeszcze nie powstała"
        description="Organizator utworzy ją po fazie grupowej."
      />
    )
  }

  // Do wygenerowania terminarza brakującą drabinkę tworzy generator (z tej samej propozycji par).
  const beforeSchedule =
    tournamentStatus === "Draft" ||
    tournamentStatus === "RegistrationOpen" ||
    tournamentStatus === "RegistrationClosed"
  const acceptedTeams = (teams.data ?? []).filter(
    (t) => t.categoryId === categoryId && t.registrationStatus === "Accepted",
  )
  // Puchar układa pary z konkretnych drużyn — bez dwóch zaakceptowanych formularz miałby same puste listy.
  if (tournamentFormat === "Knockout" && acceptedTeams.length < 2) {
    return (
      <EmptyState
        icon={GitBranch}
        title="Za mało drużyn na drabinkę"
        description={
          acceptedTeams.length === 0
            ? "Ta kategoria nie ma jeszcze żadnej zaakceptowanej drużyny. Do ułożenia par potrzeba co najmniej dwóch."
            : "Ta kategoria ma tylko jedną zaakceptowaną drużynę. Do ułożenia par potrzeba co najmniej dwóch."
        }
        action={
          <Button asChild size="sm">
            <Link href={`/tournaments/${id}/teams`}>Przejdź do Drużyn</Link>
          </Button>
        }
      />
    )
  }

  // Czy generator terminarza ułoży tę drabinkę sam. Puchar: zawsze, bo drabinka mieści każdą liczbę
  // drużyn (PUC-020). Grupy + puchar: propozycja "na krzyż" działa przy 1, 2, 4 albo 8 grupach (grup
  // może jeszcze nie być, wtedy zakładamy, że się uda).
  const groupCount = categoryGroups.length
  const autoReason =
    tournamentFormat === "Knockout"
      ? autoBracketPairs([], acceptedTeams)
        ? null
        : "Pary trzeba ułożyć samodzielnie."
      : groupCount === 0 || [1, 2, 4, 8].includes(groupCount)
        ? null
        : `Przy ${groupCount} grupach pary trzeba ułożyć samodzielnie.`
  const autoCreates = beforeSchedule && !autoReason

  return (
    <div className="max-w-3xl space-y-4">
      {beforeSchedule && (
        <div
          role="status"
          className={cn(
            "rounded-xl border px-4 py-3 text-sm",
            autoCreates ? "border-primary/40 bg-primary/10" : "border-live/50 bg-live/10",
          )}
        >
          {autoCreates ? (
            <>
              <p className="font-medium">Drabinka utworzy się sama</p>
              <p className="text-muted-foreground">
                {tournamentFormat === "Knockout"
                  ? "Gdy wygenerujesz terminarz, pary ułożą się według rozstawienia, tak jak poniżej."
                  : "Po wygenerowaniu terminarza pary ułożą się z miejsc w grupach, tak jak poniżej."}{" "}
                Nie musisz nic tu robić. Jeśli chcesz inne pary, zmień je i kliknij „Utwórz
                drabinkę”.
              </p>
              <Button asChild size="sm" className="mt-3">
                <Link href={`/tournaments/${id}/schedule/generate`}>
                  <CalendarCheck />
                  Generuj terminarz
                </Link>
              </Button>
            </>
          ) : (
            <p>{autoReason}</p>
          )}
        </div>
      )}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            {autoCreates ? "Własne pary (opcjonalnie)" : "Utwórz drabinkę"}
          </CardTitle>
          <CardDescription>
            Pary pierwszej rundy z miejsc w grupach albo konkretnych drużyn.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <GenerateBracketForm
            // Propozycja par i wielkość drabinki liczą się przy montowaniu — zmiana składu drużyn albo
            // grup (np. świeższe dane po usunięciu zgłoszeń) ma je przeliczyć od nowa.
            key={[...acceptedTeams.map((t) => t.id), ...categoryGroups.map((g) => g.id)].join(",")}
            tournamentId={id}
            categoryId={categoryId}
            groups={categoryGroups}
            teams={acceptedTeams}
            thirdPlaceEnabled={settings.data?.isThirdPlaceMatchEnabled ?? false}
          />
        </CardContent>
      </Card>
    </div>
  )
}

export function TournamentBracketView() {
  const { id, tournament } = useTournamentContext()
  const categories = useCategories(id)
  useTournamentMatchesRealtime([id])
  // ?category=… otwiera drabinkę konkretnej kategorii (np. link z usuwania kategorii).
  const searchParams = useSearchParams()
  const [selected, setSelected] = useState<string | undefined>(
    () => searchParams.get("category") ?? undefined,
  )
  const format = tournament.data?.format

  if (format !== "Knockout" && format !== "GroupAndKnockout") {
    return (
      <EmptyState
        icon={GitBranch}
        title="Ten turniej nie ma fazy pucharowej"
        description="Drabinka jest dostępna w formatach „Pucharowy” i „Grupy + puchar” (Ustawienia → Format)."
      />
    )
  }
  if (categories.isPending) return <Skeleton className="h-64 w-full rounded-xl" />
  const list = categories.data ?? []
  const categoryId = list.some((c) => c.id === selected) ? selected : list[0]?.id
  if (!categoryId) return <EmptyState icon={GitBranch} title="Najpierw dodaj kategorię" />

  return (
    <div className="space-y-4">
      <CategoryPicker categories={list} value={categoryId} onChange={setSelected} />
      <CategoryBracket key={categoryId} categoryId={categoryId} />
    </div>
  )
}
