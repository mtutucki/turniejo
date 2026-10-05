"use client"

import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { MotionConfig } from "motion/react"
import { getSession, SessionProvider, signOut, useSession } from "next-auth/react"
import { useEffect, useState, type ReactNode } from "react"

import { AuthSync } from "@/components/auth/auth-sync"
import { NotificationSync } from "@/features/notifications/notification-sync"
import { Toaster } from "@/components/ui/sonner"
import { TooltipProvider } from "@/components/ui/tooltip"
import { handleApiError } from "@/lib/api/error-handler"
import { isApiError } from "@/lib/api/errors"
import { useActiveTournamentStore } from "@/stores/active-tournament-store"

declare module "@tanstack/react-query" {
  interface Register {
    mutationMeta: {
      /** Formularz pokazuje błędy 422 przy polach — globalny handler nie robi toasta. */
      handlesValidation?: boolean
      /** Ekran sam pokazuje komunikat dla 409 z regułą domenową — bez globalnego toasta. */
      handlesConflict?: boolean
    }
  }
}

/** Strony logowania/rejestracji — z nich nigdy nie przekierowujemy na logowanie (inaczej pętla). */
const AUTH_PAGES = ["/login", "/register", "/password-reset"]
const onAuthPage = () => AUTH_PAGES.includes(window.location.pathname)

function loginUrl() {
  const { pathname, search } = window.location
  // Już na logowaniu: zostaw obecny adres (z jego callbackUrl), zamiast zagnieżdżać /login w /login.
  if (pathname === "/login") return pathname + search
  if (onAuthPage()) return "/login"
  return `/login?callbackUrl=${encodeURIComponent(pathname + search)}`
}

function redirectToLogin() {
  // 401 z zapytania w tle na stronie logowania to po prostu brak sesji — nic nie robimy.
  if (onAuthPage()) return
  window.location.assign(loginUrl())
}

// Stan sesji czytamy w chwili błędu — QueryClient powstaje raz i nie widzi zmian useSession.
async function onApiError(
  error: unknown,
  meta?: { handlesValidation?: boolean; handlesConflict?: boolean },
) {
  const session = await getSession()
  handleApiError(error, {
    hasToken: !!session?.accessToken,
    redirectToLogin,
    ...meta,
  })
}

function QueryProvider({ children }: { children: ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        queryCache: new QueryCache({ onError: (error) => onApiError(error) }),
        mutationCache: new MutationCache({
          onError: (error, _variables, _context, mutation) => onApiError(error, mutation.meta),
        }),
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            // Błędy 4xx są deterministyczne — ponawiamy tylko sieć i 5xx.
            retry: (failureCount, error) =>
              failureCount < 2 && !(isApiError(error) && error.status < 500),
          },
        },
      }),
  )

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
}

/** Refresh token wygasł (np. koniec sesji w Keycloaku) — czyścimy sesję i wracamy do logowania. */
function SessionExpiryWatcher() {
  const { data: session } = useSession()
  useEffect(() => {
    if (session?.error !== "RefreshTokenError") return
    // Na stronie logowania tylko czyścimy sesję — przekierowanie przeładowałoby ją w kółko.
    if (onAuthPage()) void signOut({ redirect: false })
    else void signOut({ redirectTo: loginUrl() })
  }, [session?.error])
  return null
}

function PersistedStoresHydration() {
  useEffect(() => {
    void useActiveTournamentStore.persist.rehydrate()
  }, [])
  return null
}

export function Providers({ children }: { children: ReactNode }) {
  return (
    <SessionProvider>
      <QueryProvider>
        {/* Animacje respektują systemowe "ogranicz ruch". */}
        <MotionConfig reducedMotion="user">
          <TooltipProvider>{children}</TooltipProvider>
        </MotionConfig>
        <AuthSync />
        <NotificationSync />
        <PersistedStoresHydration />
        <SessionExpiryWatcher />
        {/* Aplikacja jest tylko ciemna (klasa "dark" na <html>) — bez ThemeProvidera. */}
        <Toaster theme="dark" position="top-center" richColors closeButton />
      </QueryProvider>
    </SessionProvider>
  )
}
