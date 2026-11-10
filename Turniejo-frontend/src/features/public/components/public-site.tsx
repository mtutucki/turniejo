import { LayoutDashboard, LogIn, Trophy } from "lucide-react"
import Link from "next/link"
import type { ReactNode } from "react"

import { auth } from "@/auth"
import { DemoBanner } from "@/components/demo-banner"
import { LegalLinks } from "@/components/layout/legal-links"
import { Button } from "@/components/ui/button"
import { ThemeToggle } from "@/features/theme/theme-toggle"
import { DEFAULT_APP_PATH } from "@/lib/auth/public-paths"
import { formatVersion } from "@/lib/version"

/** Strona główna dla gości i lista turniejów (/t): zielony nagłówek jak w portalu kibica. */
export async function PublicSite({
  title,
  subtitle,
  children,
}: {
  title: string
  subtitle: string
  children: ReactNode
}) {
  const session = await auth()
  const signedIn = !!session && !session.error
  const version = formatVersion()
  const AppIcon = signedIn ? LayoutDashboard : LogIn

  return (
    <div className="flex min-h-dvh flex-1 flex-col">
      <DemoBanner />
      <header className="bg-brand text-brand-foreground">
        <div className="mx-auto max-w-5xl px-4 pt-4 pb-8">
          <div className="flex items-center justify-between gap-3">
            {/* Zalogowanego "/" przekierowuje do panelu, więc logo prowadzi go do listy turniejów
                (do panelu jest „Mój panel”). Gość na "/" widzi tę samą listę. */}
            <Link
              href={signedIn ? "/t" : "/"}
              className="flex items-center gap-2 text-sm font-semibold tracking-tight opacity-90 hover:opacity-100"
            >
              <span className="bg-primary text-primary-foreground flex size-7 items-center justify-center rounded-md">
                <Trophy className="size-4" />
              </span>
              Turniejo
            </Link>
            <div className="flex items-center gap-3">
              <ThemeToggle className="-my-1 size-8" />
              <Link
                href={signedIn ? DEFAULT_APP_PATH : "/login"}
                className="flex items-center gap-1.5 rounded-md bg-white/15 px-2.5 py-1 text-xs font-semibold hover:bg-white/25"
              >
                <AppIcon className="size-3.5" />
                {signedIn ? "Mój panel" : "Zaloguj się"}
              </Link>
            </div>
          </div>
          <div className="pt-8">
            <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">{title}</h1>
            <p className="mt-2 max-w-xl text-sm opacity-80 sm:text-base">{subtitle}</p>
            {!signedIn && (
              <Button asChild size="sm" variant="secondary" className="mt-5">
                <Link href="/register">Zorganizuj własny turniej</Link>
              </Button>
            )}
          </div>
        </div>
      </header>
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6">{children}</main>
      <footer className="border-t py-6">
        <LegalLinks className="justify-center" />
        {version && (
          <p className="text-muted-foreground mt-2 text-center text-xs" title={version.details}>
            Wersja {version.label}
          </p>
        )}
      </footer>
    </div>
  )
}
