/**
 * Publiczny adres aplikacji (bez ukośnika na końcu) do linków bezwzględnych: adresy kanoniczne,
 * sitemapa, podglądy linków. Na produkcji SITE_URL (https://DOMAIN, także przy budowaniu obrazu),
 * inaczej NEXTAUTH_URL, lokalnie localhost. Pusty argument budowania (SITE_URL="") to brak adresu.
 */
export const SITE_URL = (
  process.env.SITE_URL ||
  process.env.NEXTAUTH_URL ||
  "http://localhost:3000"
).replace(/\/+$/, "")

export const SITE_NAME = "Turniejo"

/**
 * Wspólne pola podglądu linku. openGraph strony zastępuje cały openGraph z layoutu (Next nie łączy
 * pól), więc każde miejsce, które go ustawia, zaczyna od tych.
 */
export const OPEN_GRAPH_DEFAULTS = {
  type: "website",
  siteName: SITE_NAME,
  locale: "pl_PL",
} as const

export const absoluteUrl = (path: string) =>
  `${SITE_URL}${path.startsWith("/") ? path : `/${path}`}`
