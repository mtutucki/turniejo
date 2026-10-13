import type { Metadata } from "next"

import { SETUP_STEP_KEYS, type SetupStepKey } from "@/features/tournaments/setup-steps"

import { TournamentSetupView } from "./tournament-setup"

export const metadata: Metadata = { title: "Konfiguracja turnieju" }

// ?step= przychodzi z listy "Przygotowanie turnieju" na przeglądzie — otwiera konkretny krok.
export default async function TournamentSetupPage({
  searchParams,
}: PageProps<"/tournaments/[id]/setup">) {
  const { step } = await searchParams
  const requested = Array.isArray(step) ? step[0] : step
  const initialStep = SETUP_STEP_KEYS.find((key) => key === requested) as SetupStepKey | undefined
  return <TournamentSetupView initialStep={initialStep} />
}
