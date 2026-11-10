import type { Metadata } from "next"

import { CircleHelp, Trophy } from "lucide-react"
import Link from "next/link"

import { DemoBanner } from "@/components/demo-banner"
import { LegalLinks } from "@/components/layout/legal-links"
import { ThemeToggle } from "@/features/theme/theme-toggle"

// Logowanie, rejestracja i reset hasła: nic do znalezienia w Google, a linki z niego nie są dla kibiców.
export const metadata: Metadata = { robots: { index: false, follow: false } }

export default function AuthLayout({ children }: LayoutProps<"/">) {
  return (
    <>
      <DemoBanner />
      <main className="relative flex flex-1 flex-col items-center justify-center gap-6 p-4">
        <ThemeToggle className="hover:bg-accent absolute top-3 right-3" />
        <div className="flex items-center gap-2 text-lg font-semibold tracking-tight">
          <span className="bg-primary text-primary-foreground flex size-9 items-center justify-center rounded-lg">
            <Trophy className="size-5" />
          </span>
          Turniejo
        </div>
        <div className="w-full max-w-sm">{children}</div>
        <Link
          href="/help"
          className="text-muted-foreground hover:text-foreground flex items-center gap-1.5 text-sm"
        >
          <CircleHelp className="size-4" />
          Czym jest Turniejo? Pomoc i FAQ
        </Link>
        <LegalLinks className="justify-center" withHelp={false} />
      </main>
    </>
  )
}
