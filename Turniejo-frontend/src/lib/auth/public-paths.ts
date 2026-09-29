// Strony dostępne bez logowania. Portal publiczny turnieju (/t/[slug]) dojdzie w Fazie 6.
const PUBLIC_PREFIXES = ["/login", "/register", "/password-reset", "/t/"]

export function isPublicPath(pathname: string) {
  return PUBLIC_PREFIXES.some((prefix) => pathname === prefix || pathname.startsWith(prefix))
}
