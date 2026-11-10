import type { Metadata } from "next"
import { Suspense } from "react"

import { OrganizationTeams } from "./organization-teams"

export const metadata: Metadata = { title: "Baza drużyn" }

export default function OrganizationTeamsPage() {
  // Suspense: zakładka jest w adresie (?tab=labels), a useSearchParams tego wymaga.
  return (
    <Suspense>
      <OrganizationTeams />
    </Suspense>
  )
}
