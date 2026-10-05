"use client"

import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import type { TournamentPlayer } from "@/types/teams"
import type { TournamentCategory } from "@/types/tournaments"

import { useUpdatePlayerInSquad } from "../api"
import { PlayerForm } from "./player-form"

/**
 * Edycja zawodnika już dodanego do składu — organizator (szczegóły drużyny) i trener (panel trenera).
 * Renderowany tylko dla wybranego zawodnika, więc formularz zawsze startuje z jego danymi.
 */
export function EditPlayerDialog({
  tournamentId,
  tournamentTeamId,
  category,
  squad,
  player,
  onClose,
}: {
  tournamentId: string
  tournamentTeamId: string
  category: TournamentCategory | undefined
  squad: TournamentPlayer[]
  player: TournamentPlayer
  onClose: () => void
}) {
  const update = useUpdatePlayerInSquad(tournamentId, tournamentTeamId)

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Edycja zawodnika</DialogTitle>
          <DialogDescription>
            Imię, nazwisko, data urodzenia i zdjęcie zmienią się też w innych turniejach tego
            zawodnika. Numer, pozycja i status dotyczą tylko tego składu.
          </DialogDescription>
        </DialogHeader>
        <PlayerForm
          category={category}
          squad={squad}
          player={player}
          submitLabel="Zapisz"
          onSubmit={async (input) => {
            await update.mutateAsync({
              ...input,
              tournamentPlayerId: player.id,
              // Formularz nie ma opcji "bez pozycji" — nietknięty brak pozycji zostaje brakiem.
              position:
                player.position === null && input.position === "Unknown" ? null : input.position,
            })
            toast.success("Zapisano zmiany")
            onClose()
          }}
          secondaryAction={
            <Button type="button" variant="ghost" onClick={onClose}>
              Anuluj
            </Button>
          }
        />
      </DialogContent>
    </Dialog>
  )
}
