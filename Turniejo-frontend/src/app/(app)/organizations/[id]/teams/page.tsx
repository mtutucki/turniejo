import type { Metadata } from "next"

import { OrganizationTeams } from "./organization-teams"

export const metadata: Metadata = { title: "Baza drużyn" }

export default function OrganizationTeamsPage() {
  return <OrganizationTeams />
}
