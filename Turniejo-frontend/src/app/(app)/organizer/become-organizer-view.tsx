"use client"

import { CalendarDays, Radio, Shirt, Trophy } from "lucide-react"
import { useRouter } from "next/navigation"
import { useEffect } from "react"

import { PageHeader } from "@/components/layout/page-header"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useMyOrganization } from "@/features/tournaments/api"
import { BecomeOrganizerDialog } from "@/features/tournaments/components/become-organizer-dialog"

const PERKS = [
  { icon: Trophy, text: "Tworzysz własne turnieje: format, kategorie i grupy." },
  { icon: Shirt, text: "Prowadzisz bazę drużyn i ich składy." },
  { icon: CalendarDays, text: "Generujesz terminarz i przydzielasz sędziów." },
  { icon: Radio, text: "Wyniki na żywo i portal dla kibiców." },
]

/**
 * Wejście z menu dla konta bez organizacji (np. sędzia albo rodzic, który dołączył kodem).
 * Role w turniejach, do których user dołączył, zostają bez zmian.
 */
export function BecomeOrganizerView() {
  const organization = useMyOrganization()
  const router = useRouter()

  // Ktoś, kto ma już organizację (np. wszedł ze starego linku), ląduje od razu w swoich turniejach.
  const hasOrganization = !!organization.data
  useEffect(() => {
    if (hasOrganization) router.replace("/tournaments")
  }, [hasOrganization, router])

  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader
        title="Zostań organizatorem"
        description="Załóż organizację i prowadź własne turnieje. Turnieje, do których dołączyłeś, zostają tak jak są."
      />
      {organization.isPending || hasOrganization ? (
        <Skeleton className="h-64 rounded-xl" />
      ) : (
        <Card>
          <CardContent className="space-y-5">
            <ul className="space-y-3">
              {PERKS.map(({ icon: Icon, text }) => (
                <li key={text} className="flex items-center gap-3 text-sm">
                  <span className="bg-primary/15 text-primary flex size-8 shrink-0 items-center justify-center rounded-full">
                    <Icon className="size-4" />
                  </span>
                  {text}
                </li>
              ))}
            </ul>
            <BecomeOrganizerDialog
              defaultOpen
              trigger={<Button className="w-full sm:w-auto">Załóż organizację</Button>}
            />
          </CardContent>
        </Card>
      )}
    </div>
  )
}
