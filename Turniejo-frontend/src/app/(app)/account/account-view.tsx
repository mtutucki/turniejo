"use client"

import { Download, Trash2 } from "lucide-react"
import Link from "next/link"
import { signOut, useSession } from "next-auth/react"
import { toast } from "sonner"

import { LegalLinks } from "@/components/layout/legal-links"
import { PageHeader } from "@/components/layout/page-header"
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
import { useDeleteMyAccount, useExportMyData } from "@/features/account/api"
import { isApiError } from "@/lib/api/errors"
import { LEGAL } from "@/lib/legal"
import { useActiveTournamentStore } from "@/stores/active-tournament-store"

function DeleteAccount() {
  const remove = useDeleteMyAccount()
  const setTournament = useActiveTournamentStore((state) => state.setTournament)
  const soleOrganizer =
    isApiError(remove.error) &&
    remove.error.status === 409 &&
    remove.error.code === "Conflict.Account"

  return (
    <Card className="border-destructive/40">
      <CardHeader>
        <CardTitle className="text-base">Usuń konto</CardTitle>
        <CardDescription>
          Usuniemy Twoje imię, nazwisko, e-mail i telefon, role w turniejach i powiązania z dziećmi,
          a logowanie przestanie działać. Wyniki meczów, które sędziowałeś, zostaną w historii
          turnieju jako „Usunięty użytkownik”. Tej operacji nie da się cofnąć.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {soleOrganizer && (
          <p role="alert" className="bg-destructive/10 text-destructive rounded-lg p-3 text-sm">
            Jesteś jedynym organizatorem organizacji — jej usunięcie obejmuje turnieje, drużyny i
            dane zawodników, więc robimy to na prośbę mailową: {LEGAL.contactEmail}.
          </p>
        )}
        <AlertDialog>
          <AlertDialogTrigger asChild>
            <Button variant="outline" className="text-destructive hover:text-destructive">
              <Trash2 />
              Usuń konto
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Na pewno usunąć konto?</AlertDialogTitle>
              <AlertDialogDescription>
                Twoje dane osobowe zostaną usunięte, a Ty zostaniesz wylogowany. Jeśli chcesz
                zachować kopię danych, najpierw je pobierz.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Anuluj</AlertDialogCancel>
              <AlertDialogAction
                variant="destructive"
                disabled={remove.isPending}
                onClick={() =>
                  remove.mutate(undefined, {
                    onSuccess: () => {
                      setTournament(null)
                      toast.success("Konto zostało usunięte")
                      void signOut({ redirectTo: "/login" })
                    },
                  })
                }
              >
                Usuń konto na zawsze
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </CardContent>
    </Card>
  )
}

/** Ustawienia konta: dane profilu, eksport danych i usunięcie konta (prawa z RODO). */
export function AccountView() {
  const { data: session } = useSession()
  const exportData = useExportMyData()

  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader title="Moje konto" />
      <div className="space-y-4">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{session?.user?.name ?? "Użytkownik"}</CardTitle>
            <CardDescription>{session?.user?.email}</CardDescription>
          </CardHeader>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Twoje dane</CardTitle>
            <CardDescription>
              Pobierz kopię danych, które przechowujemy o Tobie: profil, role w turniejach i dzieci,
              których jesteś opiekunem. Jak je przetwarzamy, opisuje{" "}
              <Link href="/privacy" className="text-primary font-medium hover:underline">
                polityka prywatności
              </Link>
              .
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button
              variant="outline"
              disabled={exportData.isPending}
              onClick={() => exportData.mutate()}
            >
              <Download />
              {exportData.isPending ? "Przygotowywanie…" : "Pobierz moje dane (JSON)"}
            </Button>
          </CardContent>
        </Card>

        <DeleteAccount />

        <LegalLinks className="pt-2" />
      </div>
    </div>
  )
}
