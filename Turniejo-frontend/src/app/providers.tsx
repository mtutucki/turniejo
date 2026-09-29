"use client"

import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { getSession, SessionProvider, signIn, useSession } from "next-auth/react"
import { ThemeProvider } from "next-themes"
import { useEffect, useState, type ReactNode } from "react"

import { Toaster } from "@/components/ui/sonner"
import { TooltipProvider } from "@/components/ui/tooltip"
import { handleApiError } from "@/lib/api/error-handler"
import { isApiError } from "@/lib/api/errors"

declare module "@tanstack/react-query" {
  interface Register {
    mutationMeta: {
      /** Formularz pokazuje błędy 422 przy polach — globalny handler nie robi toasta. */
      handlesValidation?: boolean
    }
  }
}

function redirectToLogin() {
  void signIn("keycloak", { redirectTo: window.location.href })
}

// Stan sesji czytamy w chwili błędu — QueryClient powstaje raz i nie widzi zmian useSession.
async function onApiError(error: unknown, handlesValidation?: boolean) {
  const session = await getSession()
  handleApiError(error, {
    hasToken: !!session?.accessToken,
    redirectToLogin,
    handlesValidation,
  })
}

function QueryProvider({ children }: { children: ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        queryCache: new QueryCache({ onError: (error) => onApiError(error) }),
        mutationCache: new MutationCache({
          onError: (error, _variables, _context, mutation) =>
            onApiError(error, mutation.meta?.handlesValidation),
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

/** Refresh token wygasł (np. koniec sesji SSO) — wracamy do logowania w Keycloaku. */
function SessionExpiryWatcher() {
  const { data: session } = useSession()
  useEffect(() => {
    if (session?.error === "RefreshTokenError") redirectToLogin()
  }, [session?.error])
  return null
}

export function Providers({ children }: { children: ReactNode }) {
  return (
    <SessionProvider>
      <ThemeProvider attribute="class" defaultTheme="dark" enableSystem={false}>
        <QueryProvider>
          <TooltipProvider>{children}</TooltipProvider>
          <SessionExpiryWatcher />
          <Toaster position="top-center" richColors closeButton />
        </QueryProvider>
      </ThemeProvider>
    </SessionProvider>
  )
}
