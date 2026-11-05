import { toast } from "sonner"

import { isApiError } from "./errors"

type ErrorContext = {
  hasToken: boolean
  redirectToLogin: () => void
  /** Formularz sam pokaże błędy 422 przy polach (applyFieldErrors) — nie dublujemy toastem. */
  handlesValidation?: boolean
  /** Ekran sam pokazuje komunikat dla 409 z kodem reguły domenowej — nie dublujemy toastem. */
  handlesConflict?: boolean
}

const RATE_LIMIT_TOAST_ID = "rate-limit"
let rateLimitTimer: ReturnType<typeof setInterval> | undefined

function showRateLimitToast(seconds: number) {
  clearInterval(rateLimitTimer)
  let remaining = seconds
  const render = () =>
    toast.warning("Zbyt wiele żądań", {
      id: RATE_LIMIT_TOAST_ID,
      description: `Spróbuj ponownie za ${remaining} s`,
      duration: Infinity,
    })

  render()
  rateLimitTimer = setInterval(() => {
    remaining -= 1
    if (remaining > 0) return render()
    clearInterval(rateLimitTimer)
    toast.dismiss(RATE_LIMIT_TOAST_ID)
  }, 1000)
}

/** Globalna reakcja na błędy API — podpięta pod QueryCache/MutationCache w providers.tsx. */
export function handleApiError(error: unknown, context: ErrorContext) {
  if (!isApiError(error)) {
    toast.error("Brak połączenia z serwerem")
    return
  }

  switch (error.status) {
    case 401:
      context.redirectToLogin()
      return
    case 403:
      // Backend zwraca 403 zarówno przy braku roli, jak i przy braku zalogowania.
      if (!context.hasToken) context.redirectToLogin()
      else toast.error("Brak uprawnień do tego zasobu")
      return
    case 404:
      // Strony obsługują brak zasobu same (notFound / empty state).
      return
    case 409:
      // Reguła domenowa (Result.Error, np. "Conflict.Status") ma własny, konkretny komunikat.
      // Ogólny tekst zostaje dla konfliktu współbieżności z ExceptionHandlingMiddleware.
      if (error.code && error.detail) {
        if (!context.handlesConflict) toast.error(error.detail)
        return
      }
      toast.error("Konflikt edycji - dane zostały zmodyfikowane w innym miejscu", {
        description: "Odśwież stronę i spróbuj ponownie.",
      })
      return
    case 400:
    case 422:
      if (error.fieldErrors.length > 0 && context.handlesValidation) return
      toast.error(error.fieldErrors[0]?.message ?? error.detail ?? "Nieprawidłowe dane")
      return
    case 429:
      showRateLimitToast(error.retryAfter ?? 60)
      return
    default:
      toast.error("Wystąpił nieoczekiwany błąd", { description: error.detail })
  }
}
