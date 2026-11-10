"use client"

import { Trash2 } from "lucide-react"
import Link from "next/link"
import { toast } from "sonner"

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
import { plural } from "@/lib/format"
import type { TournamentGroup } from "@/types/tournaments"

import { useDeleteGroup } from "../api"

type DeleteGroupDialogProps = {
  tournamentId: string
  group: TournamentGroup
  /** Oczekujące i przyjęte drużyny w grupie (wycofane i odrzucone nie blokują). */
  activeTeams: number
  /** Mecze tej grupy w terminarzu, także odwołane. */
  matches: number
}

/** Usunięcie grupy. Gdy coś je blokuje (te same reguły co backend), okno mówi, co zrobić najpierw. */
export function DeleteGroupDialog({
  tournamentId,
  group,
  activeTeams,
  matches,
}: DeleteGroupDialogProps) {
  const deleteGroup = useDeleteGroup(tournamentId)
  const blocked = activeTeams > 0 || matches > 0

  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button variant="ghost" size="icon" aria-label={`Usuń ${group.name}`}>
          <Trash2 />
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            {blocked
              ? `Grupy „${group.name}” nie da się jeszcze usunąć`
              : `Usunąć grupę „${group.name}”?`}
          </AlertDialogTitle>
          <AlertDialogDescription asChild>
            {blocked ? (
              <div className="space-y-2">
                <p>Najpierw:</p>
                <ul className="list-disc space-y-1 pl-5">
                  {activeTeams > 0 && (
                    <li>
                      przenieś albo wycofaj jej drużyny (
                      {plural(activeTeams, ["drużyna", "drużyny", "drużyn"])}).{" "}
                      <Link
                        href={`/tournaments/${tournamentId}/teams`}
                        className="text-foreground underline"
                      >
                        Przejdź do Drużyn
                      </Link>
                    </li>
                  )}
                  {matches > 0 && (
                    <li>
                      wyczyść terminarz (ma {plural(matches, ["mecz", "mecze", "meczów"])}).{" "}
                      <Link
                        href={`/tournaments/${tournamentId}/schedule`}
                        className="text-foreground underline"
                      >
                        Przejdź do Terminarza
                      </Link>
                    </li>
                  )}
                </ul>
              </div>
            ) : (
              <p>W grupie nie ma aktywnych drużyn ani meczów, więc nic nie stracisz.</p>
            )}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{blocked ? "Zamknij" : "Anuluj"}</AlertDialogCancel>
          {!blocked && (
            <AlertDialogAction
              variant="destructive"
              disabled={deleteGroup.isPending}
              onClick={() =>
                deleteGroup.mutate(group.id, {
                  onSuccess: () => toast.success(`Usunięto: ${group.name}`),
                })
              }
            >
              Usuń
            </AlertDialogAction>
          )}
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
