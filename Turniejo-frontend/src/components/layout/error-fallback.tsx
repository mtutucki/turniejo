"use client"

import { RotateCcw, TriangleAlert } from "lucide-react"
import Link from "next/link"
import { useEffect } from "react"

import { Button } from "@/components/ui/button"

type ErrorFallbackProps = {
  error: Error & { digest?: string }
  reset: () => void
  /** Dokąd wraca przycisk "Wróć" — np. /tournaments w aplikacji, strona turnieju w portalu. */
  homeHref?: string
  homeLabel?: string
}

/**
 * Wspólny widok dla error.tsx: zamiast białej strony pokazuje, co się stało, i pozwala spróbować
 * ponownie bez przeładowania (reset re-renderuje segment). Błędy API mają już toasty — tu trafia
 * to, co wysypało render albo serwerowe pobranie danych (np. backend nie odpowiada).
 */
export function ErrorFallback({
  error,
  reset,
  homeHref = "/",
  homeLabel = "Strona główna",
}: ErrorFallbackProps) {
  useEffect(() => {
    console.error(error)
  }, [error])

  return (
    <div
      role="alert"
      className="mx-auto flex max-w-md flex-col items-center gap-4 px-4 py-16 text-center"
    >
      <span className="bg-destructive/15 text-destructive flex size-12 items-center justify-center rounded-full">
        <TriangleAlert className="size-6" />
      </span>
      <div className="space-y-1">
        <h1 className="text-lg font-semibold">Coś poszło nie tak</h1>
        <p className="text-muted-foreground text-sm">
          Nie udało się wyświetlić tej strony. Sprawdź połączenie i spróbuj ponownie — jeśli problem
          wraca, daj znać organizatorowi.
        </p>
        {error.digest && (
          <p className="text-muted-foreground font-mono text-xs">Kod błędu: {error.digest}</p>
        )}
      </div>
      <div className="flex flex-wrap justify-center gap-2">
        <Button onClick={reset}>
          <RotateCcw />
          Spróbuj ponownie
        </Button>
        <Button asChild variant="outline">
          <Link href={homeHref}>{homeLabel}</Link>
        </Button>
      </div>
    </div>
  )
}
