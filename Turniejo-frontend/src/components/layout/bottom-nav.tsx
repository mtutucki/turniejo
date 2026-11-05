"use client"

import { Menu, UserRound } from "lucide-react"
import Link from "next/link"
import { usePathname } from "next/navigation"
import { useState } from "react"
import type { ReactNode } from "react"

import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet"
import { cn } from "@/lib/utils"
import { useActiveTournamentStore } from "@/stores/active-tournament-store"
import { useAuthStore } from "@/stores/auth-store"
import { selectUnreadCount, useNotificationStore } from "@/stores/notification-store"

import { isNavItemActive, type NavItem, tournamentNavItems } from "./nav-items"
import { NavLink } from "./nav-link"
import { useGlobalNavItems, useShowsTournamentSection } from "./use-global-nav"
import { SignOutButton } from "./user-menu"

const itemClass =
  "flex h-full w-full flex-col items-center justify-center gap-1 text-xs font-medium transition-colors"

function BottomNavItem({
  active,
  unread = false,
  children,
}: {
  active: boolean
  /** Kropka nieprzeczytanych powiadomień przy ikonie. */
  unread?: boolean
  children: ReactNode
}) {
  return (
    <span className={cn(itemClass, "relative", active ? "text-primary" : "text-muted-foreground")}>
      {children}
      {unread && (
        <span
          className="bg-live ring-card absolute top-2.5 left-1/2 ml-1.5 size-2.5 rounded-full ring-2"
          data-testid="unread-dot"
        >
          <span className="sr-only">Nieprzeczytane powiadomienia</span>
        </span>
      )}
    </span>
  )
}

/**
 * Dolna nawigacja kciukowa (< 1024px): trzy sekcje turnieju + arkusz "Więcej". Rodzic/kibic nie
 * pracuje w turnieju, a reszta może jeszcze nie mieć wybranego turnieju — wtedy zamiast sekcji są
 * trzy pierwsze pozycje menu (np. "Moje turnieje", "Moje dzieci").
 */
export function BottomNav() {
  const pathname = usePathname()
  const tournament = useActiveTournamentStore((state) => state.tournament)
  const globalItems = useGlobalNavItems()
  const rolesLoading = useAuthStore((state) => state.roleAssignments === null)
  // Bez wybranego turnieju Mecze/Tabela/Drużyny nie mają dokąd prowadzić, więc pasek pokazuje
  // zwykłe menu. Póki role się wczytują, zostają sekcje turnieju, żeby układ nie mignął.
  const showTournament = useShowsTournamentSection() && (!!tournament || rolesLoading)
  const hasUnread = useNotificationStore(selectUnreadCount) > 0
  const [moreOpen, setMoreOpen] = useState(false)
  const moreActive = globalItems.some((item) => isNavItemActive(pathname, item))
  const primaryItems = showTournament
    ? tournamentNavItems(tournament).map((item): NavItem & { active: boolean } => ({
        ...item,
        active: !!tournament && isNavItemActive(pathname, { href: item.href }),
      }))
    : globalItems.slice(0, 3).map((item) => ({ ...item, active: isNavItemActive(pathname, item) }))

  // Kropka na "Więcej" tylko wtedy, gdy Powiadomienia nie są już widoczne w samym pasku.
  const moreHasUnread = hasUnread && !primaryItems.some((item) => item.showsUnread)

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
        {primaryItems.map(({ href, label, icon: Icon, active, showsUnread }) => {
          return (
            <li key={label}>
              <Link href={href} aria-current={active ? "page" : undefined}>
                <BottomNavItem active={active} unread={showsUnread && hasUnread}>
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
              <BottomNavItem active={moreActive} unread={moreHasUnread}>
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
