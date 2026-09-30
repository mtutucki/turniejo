import type { Metadata } from "next"

import { TournamentCategoriesView } from "./tournament-categories"

export const metadata: Metadata = { title: "Kategorie i grupy" }

export default function TournamentCategoriesPage() {
  return <TournamentCategoriesView />
}
