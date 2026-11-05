import {
  Baby,
  Bell,
  CalendarDays,
  CircleHelp,
  ClipboardList,
  type LucideIcon,
  ListOrdered,
  Medal,
  Network,
  Shield,
  Shirt,
  Ticket,
  Timer,
  Trophy,
  UserPlus,
} from "lucide-react"

import {
  hasJoinedTournament,
  hasOrganization,
  isOrganizer,
  isParent,
  isReferee,
  isTeamStaff,
} from "@/lib/auth/role-landing"
import type { ActiveTournament } from "@/stores/active-tournament-store"
import type { RoleAssignment } from "@/types/roles"

export type NavItem = {
  href: string
  label: string
  icon: LucideIcon
  /** Ścieżki podświetlające pozycję — domyślnie href i wszystko pod nim. */
  activePattern?: RegExp
  /** Pozycja widoczna tylko dla userów z pasującą rolą (np. panel rodzica). */
  requires?: (assignments: RoleAssignment[]) => boolean
  /** Pozycja pokazuje licznik nieprzeczytanych powiadomień. */
  showsUnread?: boolean
}

/**
 * Sekcje aktywnego turnieju. Bez wybranego turnieju prowadzą do listy turniejów,
 * żeby user najpierw go wybrał. Puchar nie ma fazy grupowej, więc zamiast pustej tabeli
 * dostaje drabinkę (tak samo jak portal publiczny w publicSections).
 */
export function tournamentNavItems(tournament: ActiveTournament | null): NavItem[] {
  const base = tournament ? `/tournaments/${tournament.id}` : null
  return [
    { href: base ? `${base}/schedule` : "/tournaments", label: "Mecze", icon: CalendarDays },
    tournament?.format === "Knockout"
      ? { href: `${base}/bracket`, label: "Drabinka", icon: Network }
      : { href: base ? `${base}/standings` : "/tournaments", label: "Tabela", icon: ListOrdered },
    { href: base ? `${base}/teams` : "/tournaments", label: "Drużyny", icon: Shield },
  ]
}

/** Sekcje niezależne od turnieju — w sidebarze i w arkuszu "Więcej" na mobile. */
export const GLOBAL_NAV_ITEMS: NavItem[] = [
  // Podstrony konkretnego turnieju należą do sekcji turnieju, nie do listy.
  {
    href: "/tournaments",
    label: "Moje turnieje",
    icon: Trophy,
    activePattern: /^\/tournaments(\/new)?$/,
    requires: isOrganizer,
  },
  {
    href: "/joined",
    label: "Dołączone turnieje",
    icon: Medal,
    requires: hasJoinedTournament,
  },
  {
    href: "/teams",
    label: "Baza drużyn",
    icon: Shirt,
    activePattern: /^\/(teams|organizations\/[^/]+\/teams)$/,
    requires: hasOrganization,
  },
  { href: "/referee/matches", label: "Sędziowanie", icon: Timer, requires: isReferee },
  {
    href: "/coach/teams",
    label: "Moje drużyny",
    icon: ClipboardList,
    requires: isTeamStaff,
  },
  {
    href: "/parent/players",
    label: "Moje dzieci",
    icon: Baby,
    requires: isParent,
  },
  { href: "/notifications", label: "Powiadomienia", icon: Bell, showsUnread: true },
  { href: "/join", label: "Dołącz kodem", icon: Ticket },
  // Konto założone jako sędzia, trener albo rodzic może w każdej chwili zacząć organizować turnieje.
  {
    href: "/organizer",
    label: "Zostań organizatorem",
    icon: UserPlus,
    requires: (assignments) => !hasOrganization(assignments),
  },
  { href: "/help", label: "Pomoc", icon: CircleHelp },
]

export function isNavItemActive(
  pathname: string,
  { href, activePattern }: Pick<NavItem, "href" | "activePattern">,
) {
  if (activePattern) return activePattern.test(pathname)
  return pathname === href || pathname.startsWith(`${href}/`)
}
