"use client"

import { useActiveTournamentStore } from "@/stores/active-tournament-store"

import { Logo } from "./logo"
import { NotificationBell } from "./notification-bell"
import { useShowsTournamentSection } from "./use-global-nav"
import { UserMenu } from "./user-menu"

/** Zielony nagłówek aktywnego turnieju nad głównym feedem. Na mobile pełni też rolę topbara. */
export function TournamentHeader() {
  const tournament = useActiveTournamentStore((state) => state.tournament)
  const showTournament = useShowsTournamentSection()

  return (
    <header className="bg-brand text-brand-foreground sticky top-0 z-20 flex h-14 items-center gap-3 px-4 lg:px-6">
      <div className="lg:hidden">
        <Logo />
      </div>
      <p className="min-w-0 flex-1 truncate text-sm font-medium lg:text-base">
        {/* Rodzic/kibic nie wybiera "aktywnego turnieju" — dla niego to tylko pasek z logo. */}
        {showTournament && (
          <>
            <span className="text-brand-foreground/60 hidden lg:inline">Turniej: </span>
            {tournament?.name ?? <span className="text-brand-foreground/60">nie wybrano</span>}
          </>
        )}
      </p>
      <NotificationBell />
      <div className="lg:hidden">
        <UserMenu />
      </div>
    </header>
  )
}
