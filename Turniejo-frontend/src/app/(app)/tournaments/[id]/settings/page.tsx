import type { Metadata } from "next"

import { TournamentSettingsView } from "./tournament-settings"

export const metadata: Metadata = { title: "Ustawienia turnieju" }

export default function TournamentSettingsPage() {
  return <TournamentSettingsView />
}
