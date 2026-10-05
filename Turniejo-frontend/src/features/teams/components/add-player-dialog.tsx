"use client"

import { UserPlus } from "lucide-react"
import { useState } from "react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
import type { TournamentPlayer } from "@/types/teams"
import type { TournamentCategory } from "@/types/tournaments"

import { useAddNewPlayerToSquad } from "../api"
import { PlayerForm } from "./player-form"

/** Nowy zawodnik od razu w składzie — organizator (szczegóły drużyny) i trener (panel trenera). */
export function AddPlayerDialog({
  tournamentId,
  tournamentTeamId,
  category,
  squad,
}: {
  tournamentId: string
  tournamentTeamId: string
  category: TournamentCategory | undefined
  squad: TournamentPlayer[]
}) {
  const [open, setOpen] = useState(false)
  const addPlayer = useAddNewPlayerToSquad(tournamentId, tournamentTeamId)

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm">
          <UserPlus />
          Dodaj zawodnika
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Nowy zawodnik</DialogTitle>
          <DialogDescription>Profil zawodnika zostanie od razu dodany do składu.</DialogDescription>
        </DialogHeader>
        {/* Formularz montowany tylko przy otwartym dialogu — każde otwarcie zaczyna od pustych pól. */}
        {open && (
          <PlayerForm
            category={category}
            squad={squad}
            submitLabel="Dodaj do składu"
            onSubmit={async (input) => {
              await addPlayer.mutateAsync(input)
              toast.success(`${input.firstName} ${input.lastName} w składzie`)
              setOpen(false)
            }}
            secondaryAction={
              <Button type="button" variant="ghost" onClick={() => setOpen(false)}>
                Anuluj
              </Button>
            }
          />
        )}
      </DialogContent>
    </Dialog>
  )
}
