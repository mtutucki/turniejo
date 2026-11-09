"use client"

import "./globals.css"

// Ostatnia linia obrony — błąd w samym root layoucie (np. Providers). Musi mieć własne <html>/<body>
// i nie może korzystać z komponentów zależnych od providerów.

/** Motyw bez next-themes: wybór zapisany w przeglądarce (klucz "theme"), inaczej ustawienie systemu. */
function isDark() {
  if (typeof window === "undefined") return false
  let saved: string | null = null
  try {
    saved = localStorage.getItem("theme")
  } catch {}
  if (saved === "light" || saved === "dark") return saved === "dark"
  return window.matchMedia("(prefers-color-scheme: dark)").matches
}

export default function GlobalError({
  reset,
}: {
  error: Error & { digest?: string }
  reset: () => void
}) {
  return (
    <html lang="pl" className={isDark() ? "dark" : undefined}>
      <body className="bg-background text-foreground flex min-h-dvh items-center justify-center p-4 font-sans">
        <div role="alert" className="max-w-md space-y-4 text-center">
          <h1 className="text-lg font-semibold">Turniejo chwilowo nie działa</h1>
          <p className="text-muted-foreground text-sm">Odśwież stronę za chwilę.</p>
          <button
            type="button"
            onClick={reset}
            className="bg-primary text-primary-foreground rounded-md px-4 py-2 text-sm font-medium"
          >
            Spróbuj ponownie
          </button>
        </div>
      </body>
    </html>
  )
}
