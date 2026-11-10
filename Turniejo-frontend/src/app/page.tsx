import type { Metadata } from "next"
import { redirect } from "next/navigation"

import { auth } from "@/auth"
import { PublicTournamentsPage } from "@/features/public/components/public-tournaments-page"
import { DEFAULT_APP_PATH } from "@/lib/auth/public-paths"

export const metadata: Metadata = {
  title: { absolute: "Turniejo · turnieje piłkarskie na żywo" },
  description: "Wyniki, terminarze i tabele turniejów piłkarskich na żywo.",
}

// Zalogowany od razu trafia do panelu; gość widzi listę turniejów publicznych.
export default async function HomePage({ searchParams }: PageProps<"/">) {
  const session = await auth()
  if (session && !session.error) redirect(DEFAULT_APP_PATH)
  return <PublicTournamentsPage searchParams={await searchParams} />
}
