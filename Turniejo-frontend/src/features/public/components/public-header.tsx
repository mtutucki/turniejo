"use client"

import {
  CalendarDays,
  ChevronLeft,
  LayoutDashboard,
  LogIn,
  Trophy,
  Wifi,
  WifiOff,
} from "lucide-react"
import { useSession } from "next-auth/react"
import Link from "next/link"
import { usePathname } from "next/navigation"

import { ThemeToggle } from "@/features/theme/theme-toggle"
import { DEFAULT_APP_PATH } from "@/lib/auth/public-paths"
import { formatDateRange, toZonedDateInput } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { PublicTournament } from "@/types/public"

import { usePublicContext } from "../public-context"
import { publicSections } from "../sections"

const DAY_MS = 24 * 60 * 60 * 1000

/** "Dzień 2 z 3" w trakcie turnieju, inaczej zakres dat albo etap. */
function dayBadge(tournament: PublicTournament) {
  if (tournament.status === "Finished") return "Zakończony"
  if (tournament.status === "Cancelled") return "Odwołany"
  const today = toZonedDateInput(new Date())
  const start = toZonedDateInput(tournament.startAt)
  const end = toZonedDateInput(tournament.endAt)
  if (today < start || today > end) return null
  const dayNumber = Math.round((Date.parse(today) - Date.parse(start)) / DAY_MS) + 1
  const total = Math.round((Date.parse(end) - Date.parse(start)) / DAY_MS) + 1
  return total > 1 ? `Dzień ${dayNumber} z ${total}` : "Dziś"
}

/** Wyjście z portalu do aplikacji: zalogowany → panel, anonim → logowanie (potem /start). */
function AppLink() {
  const { status } = useSession()
  if (status === "loading") return null

  const authenticated = status === "authenticated"
  const Icon = authenticated ? LayoutDashboard : LogIn
  return (
    <Link
      href={authenticated ? DEFAULT_APP_PATH : "/login"}
      className="flex items-center gap-1.5 rounded-md bg-white/15 px-2.5 py-1 text-xs font-semibold hover:bg-white/25"
    >
      <Icon className="size-3.5" />
      {authenticated ? "Mój panel" : "Zaloguj się"}
    </Link>
  )
}

export function PublicHeader() {
  const { slug, tournament, realtime } = usePublicContext()
  const pathname = usePathname()
  const badge = dayBadge(tournament)
  const root = `/t/${slug}`

  return (
    <header className="bg-brand text-brand-foreground">
      <div className="mx-auto max-w-5xl px-4 pt-4">
        <div className="flex items-center justify-between gap-3">
          {/* Nie <Logo /> — to link do panelu aplikacji, a kibic zostaje w części publicznej. */}
          <Link
            href="/t"
            className="flex items-center gap-2 text-sm font-semibold tracking-tight opacity-90 hover:opacity-100"
          >
            <span className="bg-primary text-primary-foreground flex size-7 items-center justify-center rounded-md">
              <Trophy className="size-4" />
            </span>
            Turniejo
          </Link>
          <div className="flex items-center gap-3">
            {/* W odwołanym turnieju nic się już nie aktualizuje (QA v3: PUB-012). */}
            {tournament.status !== "Cancelled" && (
              <span
                role="status"
                className="flex items-center gap-1.5 text-xs opacity-80"
                title="Wyniki aktualizują się same"
              >
                {realtime === "connected" ? (
                  <Wifi className="size-3.5" />
                ) : (
                  <WifiOff className="size-3.5" />
                )}
                {realtime === "connected"
                  ? "Na żywo"
                  : realtime === "disconnected"
                    ? "Offline"
                    : "Łączenie…"}
              </span>
            )}
            <ThemeToggle className="-my-1 size-8" />
            <AppLink />
          </div>
        </div>

        <div className="py-5">
          <Link
            href="/t"
            className="mb-2 -ml-1 inline-flex items-center gap-0.5 text-xs font-medium opacity-75 hover:opacity-100"
          >
            <ChevronLeft className="size-4" />
            Wszystkie turnieje
          </Link>
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-2xl font-bold tracking-tight sm:text-3xl">{tournament.name}</h1>
            {badge && (
              <span className="rounded-full bg-white/15 px-2.5 py-0.5 text-xs font-semibold">
                {badge}
              </span>
            )}
          </div>
          <p className="mt-1 flex items-center gap-2 text-sm opacity-80">
            <CalendarDays className="size-4" />
            {formatDateRange(tournament.startAt, tournament.endAt)}
          </p>
        </div>

        <nav aria-label="Sekcje turnieju" className="-mx-4 overflow-x-auto px-4">
          <ul className="flex min-w-max gap-1">
            {publicSections(tournament).map(({ segment, label }) => {
              const href = segment ? `${root}/${segment}` : root
              const active = segment ? pathname.startsWith(href) : pathname === root
              return (
                <li key={segment}>
                  <Link
                    href={href}
                    aria-current={active ? "page" : undefined}
                    className={cn(
                      "block rounded-t-lg px-3 py-2 text-sm font-medium transition-colors",
                      active
                        ? "bg-background text-foreground"
                        : "text-brand-foreground/80 hover:text-brand-foreground",
                    )}
                  >
                    {label}
                  </Link>
                </li>
              )
            })}
          </ul>
        </nav>
      </div>
    </header>
  )
}
