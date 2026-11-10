import Link from "next/link"

import { auth } from "@/auth"
import { DemoBanner } from "@/components/demo-banner"
import { LegalLinks } from "@/components/layout/legal-links"
import { Logo } from "@/components/layout/logo"
import { Button } from "@/components/ui/button"
import { DEFAULT_APP_PATH } from "@/lib/auth/public-paths"
import { formatVersion } from "@/lib/version"

// Strony informacyjne dostępne bez logowania: pomoc, regulamin, polityka prywatności.
export default async function PublicPagesLayout({ children }: LayoutProps<"/">) {
  const session = await auth()
  const signedIn = !!session && !session.error
  const version = formatVersion()

  return (
    <div className="flex min-h-dvh flex-1 flex-col">
      <DemoBanner />
      <header className="bg-card/95 sticky top-0 z-10 border-b backdrop-blur">
        <div className="mx-auto flex h-14 w-full max-w-3xl items-center justify-between gap-3 px-4">
          <Logo />
          {signedIn ? (
            <Button asChild size="sm" variant="outline">
              <Link href={DEFAULT_APP_PATH}>Wróć do aplikacji</Link>
            </Button>
          ) : (
            <div className="flex gap-2">
              <Button asChild size="sm" variant="ghost">
                <Link href="/login">Zaloguj się</Link>
              </Button>
              <Button asChild size="sm">
                <Link href="/register">Załóż konto</Link>
              </Button>
            </div>
          )}
        </div>
      </header>
      <main className="mx-auto w-full max-w-3xl flex-1 px-4 py-8">{children}</main>
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
