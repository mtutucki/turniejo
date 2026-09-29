import { LayoutDashboard, type LucideIcon, Ticket, Timer, Trophy } from "lucide-react"

export type NavItem = {
  href: string
  label: string
  icon: LucideIcon
}

// Wspólne dla sidebaru (desktop) i dolnej nawigacji (mobile) — max 5 pozycji, żeby zmieściły się na 375px.
export const NAV_ITEMS: NavItem[] = [
  { href: "/dashboard", label: "Start", icon: LayoutDashboard },
  { href: "/tournaments", label: "Turnieje", icon: Trophy },
  { href: "/referee/matches", label: "Mecze", icon: Timer },
  { href: "/join", label: "Dołącz", icon: Ticket },
]

export function isNavItemActive(pathname: string, href: string) {
  return pathname === href || pathname.startsWith(`${href}/`)
}
