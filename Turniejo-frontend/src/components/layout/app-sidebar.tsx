"use client"

import Link from "next/link"

import { cn } from "@/lib/utils"
import { useActiveTournamentStore } from "@/stores/active-tournament-store"

import { Logo } from "./logo"
import { tournamentNavItems } from "./nav-items"
import { NavLink } from "./nav-link"
import { useGlobalNavItems, useShowsTournamentSection } from "./use-global-nav"
import { UserMenu } from "./user-menu"

function ActiveTournamentCard() {
  const tournament = useActiveTournamentStore((state) => state.tournament)

  return (
    <div className="bg-brand text-brand-foreground rounded-lg p-3">
      <p className="text-brand-foreground/60 text-xs font-medium tracking-wide uppercase">
        Aktywny turniej
      </p>
      {tournament ? (
        <Link
          href={`/tournaments/${tournament.id}/dashboard`}
          className="mt-1 block truncate font-semibold hover:underline"
        >
          {tournament.name}
        </Link>
      ) : (
        <Link href="/tournaments" className="mt-1 block text-sm hover:underline">
          Wybierz turniej →
        </Link>
      )}
    </div>
  )
}

export function AppSidebar() {
  const tournament = useActiveTournamentStore((state) => state.tournament)
  const globalItems = useGlobalNavItems()
  const showTournament = useShowsTournamentSection()

  return (
    <aside className="bg-sidebar fixed inset-y-0 left-0 z-30 hidden w-60 flex-col border-r lg:flex">
      <div className="flex h-14 items-center border-b px-5">
        <Logo />
      </div>
      <nav aria-label="Nawigacja główna" className="flex flex-1 flex-col gap-4 overflow-y-auto p-3">
        {showTournament && (
          <>
            <ActiveTournamentCard />
            <div className="flex flex-col gap-1">
              {/* Sekcje turnieju są nieaktywne, dopóki user nie wybierze turnieju (karta wyżej). */}
              {tournamentNavItems(tournament).map((item) => (
                <NavLink key={item.label} {...item} disabled={!tournament} />
              ))}
            </div>
          </>
        )}
        <div className={cn("flex flex-col gap-1", showTournament && "border-t pt-4")}>
          {globalItems.map((item) => (
            <NavLink key={item.href} {...item} />
          ))}
        </div>
      </nav>
      <div className="border-t p-3">
        <UserMenu showDetails />
      </div>
    </aside>
  )
}
