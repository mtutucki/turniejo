/**
 * Dokąd trafia user po zalogowaniu/rejestracji, gdy nie ma callbackUrl. /start przekierowuje dalej
 * wg ról (organizator → turnieje, sędzia → mecze, trener → drużyny, rodzic → dzieci).
 */
export const DEFAULT_APP_PATH = "/start"

// Strony dostępne bez logowania. Portal publiczny turnieju (/t/[slug]) dojdzie w Fazie 6.
const PUBLIC_PATHS = [
  "/login",
  "/register",
  "/password-reset",
  "/password-reset/confirm",
  "/help",
  "/terms",
  "/privacy",
]
const PUBLIC_PREFIXES = ["/t/"]

export function isPublicPath(pathname: string) {
  return (
    PUBLIC_PATHS.includes(pathname) || PUBLIC_PREFIXES.some((prefix) => pathname.startsWith(prefix))
  )
}

/** Strona główna i /start: po zalogowaniu i tak trafia się na start, więc bez callbackUrl. */
export function isStartPath(pathname: string) {
  return pathname === "/" || pathname === DEFAULT_APP_PATH
}

/**
 * Tylko ścieżka (bez domeny) z ?callbackUrl=, żeby nie dało się go użyć jako open redirect na obcą
 * domenę. Pętle na /login i /register wracają do strony startowej.
 */
export function safeCallbackUrl(value: string | string[] | undefined) {
  const url = Array.isArray(value) ? value[0] : value
  if (!url) return DEFAULT_APP_PATH
  try {
    const parsed = new URL(url, "http://localhost")
    if (parsed.pathname.startsWith("/login") || parsed.pathname.startsWith("/register")) {
      return DEFAULT_APP_PATH
    }
    return `${parsed.pathname}${parsed.search}`
  } catch {
    return DEFAULT_APP_PATH
  }
}
