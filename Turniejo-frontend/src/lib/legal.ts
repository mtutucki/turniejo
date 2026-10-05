/**
 * Dane do Regulaminu i Polityki prywatności. Wartości w [NAWIASACH] trzeba uzupełnić przed
 * uruchomieniem produkcji — strony pokazują je wyróżnione, żeby nie przeoczyć żadnej.
 *
 * LEGAL_VERSION musi być równe LegalDocuments.CurrentVersion w backendzie (Turniejo.Users.Application)
 * — backend zapisuje przy rejestracji, którą wersję user zaakceptował. Zmieniasz treść dokumentów
 * → podbij datę w obu miejscach.
 */
export const LEGAL_VERSION = "2026-09-30"

export const LEGAL = {
  /** Pełna nazwa administratora danych / usługodawcy (firma albo imię i nazwisko). */
  controllerName: "[NAZWA ADMINISTRATORA]",
  /** Adres siedziby / prowadzenia działalności. */
  controllerAddress: "[ADRES]",
  /** NIP / REGON / KRS — jeśli dotyczy. */
  controllerRegistry: "[NIP / REGON / KRS]",
  /** Adres do spraw danych osobowych i usunięcia organizacji. */
  contactEmail: "[EMAIL KONTAKTOWY]",
  /** Dostawca serwerów (hosting aplikacji i bazy danych). */
  hostingProvider: "[DOSTAWCA HOSTINGU]",
  /** Dostawca wysyłki e-maili (reset hasła, powiadomienia). */
  emailProvider: "[DOSTAWCA E-MAILI]",
  /** Jak długo trzymane są kopie zapasowe bazy (po usunięciu konta dane znikają z nich najpóźniej po tym czasie). */
  backupRetention: "[30 dni]",
  /** Jak długo trzymane są logi serwera z adresami IP. */
  logRetention: "[90 dni]",
  /** Model płatności za korzystanie z serwisu. */
  pricing: "[Korzystanie z Serwisu jest bezpłatne.]",
} as const

/** Czy wartość to jeszcze placeholder do uzupełnienia. */
export const isPlaceholder = (value: string) => value.startsWith("[") && value.endsWith("]")
