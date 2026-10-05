// "Moja drużyna" kibica/rodzica: wybór z filtra meczów zapamiętany w przeglądarce (bez konta),
// osobno dla każdego turnieju — wraca w kolejnych sekcjach portalu i przy następnej wizycie.
const key = (slug: string) => `turniejo:public-team:${slug}`

export function readFavoriteTeam(slug: string): string | null {
  try {
    return localStorage.getItem(key(slug))
  } catch {
    return null
  }
}

export function writeFavoriteTeam(slug: string, tournamentTeamId: string | null) {
  try {
    if (tournamentTeamId) localStorage.setItem(key(slug), tournamentTeamId)
    else localStorage.removeItem(key(slug))
  } catch {
    // Brak localStorage (tryb prywatny) — filtr działa tylko do odświeżenia strony.
  }
}
