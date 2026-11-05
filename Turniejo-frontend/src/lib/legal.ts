/**
 * Dane do Regulaminu i Polityki prywatności. Wartości w [NAWIASACH] trzeba uzupełnić przed
 * uruchomieniem produkcji — strony pokazują je wyróżnione, żeby nie przeoczyć żadnej.
 *
 * LEGAL_VERSION musi być równe LegalDocuments.CurrentVersion w backendzie (Turniejo.Users.Application)
 * — backend zapisuje przy rejestracji, którą wersję user zaakceptował. Zmieniasz treść dokumentów
 * → podbij datę w obu miejscach.
 */
export const LEGAL_VERSION = "2026-11-05"

export const LEGAL = {
  /** Pełna nazwa administratora danych / usługodawcy (firma albo imię i nazwisko). */
  controllerName: "Marcin Tutucki",
  /** Adres siedziby / prowadzenia działalności. Pusty = nie pokazujemy (osoba prywatna). */
  controllerAddress: "",
  /** NIP / REGON / KRS. Pusty = nie dotyczy (bez działalności gospodarczej). */
  controllerRegistry: "",
  /** Adres do spraw danych osobowych i usunięcia organizacji. */
  contactEmail: "m.tutucki@gmail.com",
  /** Dostawca serwerów (hosting aplikacji i bazy danych). */
  hostingProvider: "OVHcloud (serwer w Niemczech)",
  /** Dostawca wysyłki e-maili (reset hasła, powiadomienia). */
  emailProvider: "Brevo",
  /** Jak długo trzymane są kopie zapasowe bazy (po usunięciu konta dane znikają z nich najpóźniej po tym czasie).
   *  Musi się zgadzać z KEEP_DAYS w deploy/backup.sh. */
  backupRetention: "14 dniach",
  /** Jak długo trzymane są logi serwera z adresami IP. Docker rotuje je po rozmiarze (docker-compose.prod.yml). */
  logRetention: "do nadpisania nowszymi wpisami (limit 50 MB na usługę)",
  /** Model płatności za korzystanie z serwisu. */
  pricing: "Korzystanie z Serwisu jest bezpłatne.",
} as const

/** Czy wartość to jeszcze placeholder do uzupełnienia. */
export const isPlaceholder = (value: string) => value.startsWith("[") && value.endsWith("]")
