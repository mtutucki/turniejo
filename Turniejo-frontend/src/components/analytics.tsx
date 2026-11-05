import Script from "next/script"

const src = process.env.NEXT_PUBLIC_UMAMI_SCRIPT_URL
const websiteId = process.env.NEXT_PUBLIC_UMAMI_WEBSITE_ID

/** Czy statystyki są włączone — polityka prywatności opisuje Umami tylko wtedy. */
export const ANALYTICS_ENABLED = !!src && !!websiteId

/**
 * Statystyki odwiedzin (Umami) — bez ciasteczek i localStorage, więc bez banera zgody (opis w
 * /privacy#cookies). Bez zmiennych środowiskowych skrypt się nie ładuje (dev, testy E2E).
 * data-exclude-search: adresy bez query stringu — kody zaproszeń (/join?code=…) nie trafiają do
 * statystyk. data-do-not-track: przeglądarka z włączonym DNT nie jest liczona.
 */
export function Analytics() {
  if (!ANALYTICS_ENABLED) return null
  return (
    <Script
      src={src}
      data-website-id={websiteId}
      data-exclude-search="true"
      data-exclude-hash="true"
      data-do-not-track="true"
      strategy="afterInteractive"
    />
  )
}
