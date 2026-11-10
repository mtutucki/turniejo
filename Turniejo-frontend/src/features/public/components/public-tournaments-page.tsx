import { getPublicTournaments } from "../server"
import { PublicSite } from "./public-site"
import { TournamentDirectory } from "./tournament-directory"

type SearchParams = Record<string, string | string[] | undefined>

const first = (value: string | string[] | undefined) => (Array.isArray(value) ? value[0] : value)

/** Lista turniejów publicznych: strona główna dla gości (/) i stały adres /t. */
export async function PublicTournamentsPage({ searchParams }: { searchParams: SearchParams }) {
  const tournaments = await getPublicTournaments()
  return (
    <PublicSite
      title="Turnieje piłkarskie na żywo"
      subtitle="Wyniki, terminarze i tabele turniejów, bez logowania."
    >
      <TournamentDirectory
        initialData={tournaments}
        initialQuery={first(searchParams.q) ?? ""}
        initialOrganizerId={first(searchParams.org) ?? null}
      />
    </PublicSite>
  )
}
