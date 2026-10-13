"use client"

import { LogOut } from "lucide-react"
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

import { useMatchRefereeSelf } from "../live-api"

type ReleaseMatchButtonProps = {
  tournamentId: string
  matchId: string
  title: string
  /** Mecz trwa (Live/Paused) — oddanie nie zatrzymuje gry, więc mówimy o tym wprost. */
  inProgress: boolean
  onReleased?: () => void
}

/** Sędzia oddaje swój mecz z powrotem do puli — także w trakcie, gdy musi nagle zejść z boiska. */
export function ReleaseMatchButton({
  tournamentId,
  matchId,
  title,
  inProgress,
  onReleased,
}: ReleaseMatchButtonProps) {
  const release = useMatchRefereeSelf("release")

  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button
          size="sm"
          variant="ghost"
          disabled={release.isPending}
          aria-label={`Oddaj mecz: ${title}`}
        >
          <LogOut />
          Oddaj mecz
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Oddać ten mecz?</AlertDialogTitle>
          <AlertDialogDescription>
            {inProgress
              ? "Mecz trwa dalej — zegar się nie zatrzyma. Trafi do wolnych meczów i dokończy go inny sędzia albo organizator."
              : "Mecz wróci do wolnych meczów i będzie mógł go wziąć inny sędzia turnieju."}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Anuluj</AlertDialogCancel>
          <AlertDialogAction
            onClick={() =>
              release.mutate(
                { tournamentId, matchId },
                {
                  onSuccess: () => {
                    toast.success("Mecz oddany")
                    onReleased?.()
                  },
                },
              )
            }
          >
            Oddaj mecz
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
