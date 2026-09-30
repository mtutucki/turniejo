"use client"

import { Menu, UserRound } from "lucide-react"
import Link from "next/link"
import { usePathname } from "next/navigation"
import { useState } from "react"
import type { ReactNode } from "react"

import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet"
import { cn } from "@/lib/utils"
import { useActiveTournamentStore } from "@/stores/active-tournament-store"

import { isNavItemActive, tournamentNavItems } from "./nav-items"
import { NavLink } from "./nav-link"
import { useGlobalNavItems, useShowsTournamentSection } from "./use-global-nav"
import { SignOutButton } from "./user-menu"

const itemClass =
  "flex h-full w-full flex-col items-center justify-center gap-1 text-xs font-medium transition-colors"

function BottomNavItem({ active, children }: { active: boolean; children: ReactNode }) {
  return (
    <span className={cn(itemClass, active ? "text-primary" : "text-muted-foreground")}>
      {children}
    </span>
  )
}

/**
 * Dolna nawigacja kciukowa (< 1024px): trzy sekcje turnieju + arkusz "Więcej". Rodzic/kibic nie
 * pracuje w turnieju — dostaje zamiast nich trzy pierwsze pozycje swojego menu (np. "Moje dzieci").
 */
export function BottomNav() {
  const pathname = usePathname()
  const tournament = useActiveTournamentStore((state) => state.tournament)
  const globalItems = useGlobalNavItems()
  const showTournament = useShowsTournamentSection()
  const [moreOpen, setMoreOpen] = useState(false)
  const moreActive = globalItems.some((item) => isNavItemActive(pathname, item))
  const primaryItems = showTournament
    ? tournamentNavItems(tournament?.id).map((item) => ({
        ...item,
        // Bez turnieju wszystkie sekcje prowadzą do listy — nie podświetlamy ich wtedy.
        active: !!tournament && isNavItemActive(pathname, { href: item.href }),
      }))
    : globalItems.slice(0, 3).map((item) => ({ ...item, active: isNavItemActive(pathname, item) }))

  return (
    <nav
      aria-label="Nawigacja główna"
      className="bg-card/95 fixed inset-x-0 bottom-0 z-30 border-t pb-[env(safe-area-inset-bottom)] backdrop-blur lg:hidden"
    >
      <ul
        className="grid h-16"
        // Rodzic/kibic może mieć mniej niż trzy pozycje — bez pustych kolumn.
        style={{ gridTemplateColumns: `repeat(${primaryItems.length + 1}, minmax(0, 1fr))` }}
      >
        {primaryItems.map(({ href, label, icon: Icon, active }) => {
          return (
            <li key={label}>
              <Link href={href} aria-current={active ? "page" : undefined}>
                <BottomNavItem active={active}>
                  <Icon className="size-5" />
                  {label}
                </BottomNavItem>
              </Link>
            </li>
          )
        })}
        <li>
          <Sheet open={moreOpen} onOpenChange={setMoreOpen}>
            <SheetTrigger className="h-full w-full" aria-label="Więcej opcji">
              <BottomNavItem active={moreActive}>
                <Menu className="size-5" />
                Więcej
              </BottomNavItem>
            </SheetTrigger>
            <SheetContent side="bottom" className="rounded-t-xl pb-[env(safe-area-inset-bottom)]">
              <SheetHeader>
                <SheetTitle>Więcej</SheetTitle>
              </SheetHeader>
              <div className="flex flex-col gap-1 px-4 pb-4">
                {globalItems.map((item) => (
                  <NavLink key={item.href} {...item} onNavigate={() => setMoreOpen(false)} />
                ))}
                <div className="mt-2 border-t pt-2">
                  <NavLink
                    href="/account"
                    label="Moje konto"
                    icon={UserRound}
                    onNavigate={() => setMoreOpen(false)}
                  />
                  <SignOutButton />
                </div>
              </div>
            </SheetContent>
          </Sheet>
        </li>
      </ul>
    </nav>
  )
}
