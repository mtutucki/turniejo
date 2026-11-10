import { parseFilters } from "../directory"
import { getPublicTournaments } from "../server"
import { PublicSite } from "./public-site"
import { TournamentDirectory } from "./tournament-directory"

/** Lista turniejów publicznych: strona główna dla gości (/) i stały adres /t. */
export async function PublicTournamentsPage({
  searchParams,
}: {
  searchParams: Record<string, string | string[] | undefined>
}) {
  const tournaments = await getPublicTournaments()
  return (
    <PublicSite
      title="Turnieje piłkarskie na żywo"
      subtitle="Wyniki, terminarze i tabele turniejów."
    >
      <TournamentDirectory initialData={tournaments} initialFilters={parseFilters(searchParams)} />
    </PublicSite>
  )
}
