"use client"

import { Download, Pencil, Trash2 } from "lucide-react"
import Link from "next/link"
import { signOut, useSession } from "next-auth/react"
import { useState } from "react"
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
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { useDeleteMyAccount, useExportMyData } from "@/features/account/api"
import { useMyOrganization } from "@/features/tournaments/api"
import { RenameOrganizationDialog } from "@/features/tournaments/components/rename-organization-dialog"
import { isApiError } from "@/lib/api/errors"
import { useActiveTournamentStore } from "@/stores/active-tournament-store"

/** Słowo przepisywane ręcznie przed usunięciem organizacji — drugi, świadomy krok potwierdzenia. */
const ORGANIZATION_CONFIRM_WORD = "USUŃ"

function DeleteAccount() {
  const remove = useDeleteMyAccount()
  const organization = useMyOrganization()
  const setTournament = useActiveTournamentStore((state) => state.setTournament)
  // Drugi krok tylko dla jedynego organizatora: backend odmawia (409), dopóki nie potwierdzi,
  // że razem z kontem znika organizacja — czyli dane także innych osób.
  const [confirmOrganization, setConfirmOrganization] = useState(false)
  const [confirmText, setConfirmText] = useState("")
  const confirmed = confirmText.trim().toUpperCase() === ORGANIZATION_CONFIRM_WORD

  function onDeleted() {
    setTournament(null)
    toast.success("Konto zostało usunięte")
    void signOut({ redirectTo: "/login" })
  }

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
                  remove.mutate(
                    {},
                    {
                      onSuccess: onDeleted,
                      onError: (error) => {
                        if (
                          isApiError(error) &&
                          error.status === 409 &&
                          error.code === "Conflict.Account"
                        ) {
                          setConfirmText("")
                          setConfirmOrganization(true)
                        }
                      },
                    },
                  )
                }
              >
                Usuń konto na zawsze
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        <AlertDialog open={confirmOrganization} onOpenChange={setConfirmOrganization}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Usunąć też całą organizację?</AlertDialogTitle>
              <AlertDialogDescription asChild>
                <div className="space-y-3">
                  <p>
                    Jesteś jedynym organizatorem
                    {organization.data
                      ? ` organizacji „${organization.data.name}”`
                      : " organizacji"}
                    , więc razem z kontem usuniemy ją w całości — nie tylko dla Ciebie, ale dla
                    wszystkich:
                  </p>
                  <ul className="list-disc space-y-1 pl-5">
                    <li>wszystkie turnieje z terminarzem, wynikami, tabelami i drabinkami,</li>
                    <li>bazę drużyn i ich składy,</li>
                    <li>dane zawodników i powiązania z rodzicami,</li>
                    <li>dostęp trenerów, sędziów, rodziców i kibiców do tych turniejów.</li>
                  </ul>
                  <p className="text-destructive font-medium">Tej operacji nie da się cofnąć.</p>
                </div>
              </AlertDialogDescription>
            </AlertDialogHeader>
            <div className="space-y-2">
              <Label htmlFor="confirm-organization-delete">
                Aby potwierdzić, wpisz {ORGANIZATION_CONFIRM_WORD}
              </Label>
              <Input
                id="confirm-organization-delete"
                autoComplete="off"
                value={confirmText}
                onChange={(event) => setConfirmText(event.target.value)}
              />
            </div>
            <AlertDialogFooter>
              <AlertDialogCancel>Anuluj</AlertDialogCancel>
              <AlertDialogAction
                variant="destructive"
                disabled={!confirmed || remove.isPending}
                onClick={() =>
                  remove.mutate({ deleteOrganization: true }, { onSuccess: onDeleted })
                }
              >
                {remove.isPending ? "Usuwanie…" : "Usuń konto i organizację"}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </CardContent>
    </Card>
  )
}

/** Organizacja zakłada się sama przy rejestracji — tu ją widać i tu zmienia się jej nazwę. */
function OrganizationCard() {
  const organization = useMyOrganization()
  if (!organization.data) return null

  return (
    <Card>
      <CardHeader>
        <CardDescription>Organizacja</CardDescription>
        <CardTitle className="text-base">{organization.data.name}</CardTitle>
        <CardDescription>
          Pod tą nazwą prowadzisz turnieje i bazę drużyn — może to być klub, szkoła albo Ty sam.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <RenameOrganizationDialog
          organization={organization.data}
          trigger={
            <Button variant="outline">
              <Pencil />
              Zmień nazwę
            </Button>
          }
        />
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

        <OrganizationCard />

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
