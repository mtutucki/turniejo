import { ClipboardList, Crown, Heart, type LucideIcon, UserRound, Whistle } from "lucide-react"
import Link from "next/link"

import { cn } from "@/lib/utils"
import { UserRole } from "@/types/roles"

type RoleStyle = {
  label: string
  icon: LucideIcon
  className: string
  /** Jedno zdanie o tym, co ta rola robi w turnieju. */
  hint: string
  link?: { href: (tournamentId: string) => string; label: string }
}

// Bursztyn (--live) jest zarezerwowany dla trwających meczów, role mają własne odcienie.
const ROLE_STYLES: Partial<Record<UserRole, RoleStyle>> = {
  [UserRole.Organizer]: {
    label: "Organizator",
    icon: Crown,
    className: "bg-primary/15 text-primary",
    hint: "Zarządzasz całym turniejem.",
  },
  [UserRole.Referee]: {
    label: "Sędzia",
    icon: Whistle,
    className: "bg-sky-500/15 text-sky-300",
    hint: "Sędziujesz mecze w tym turnieju.",
    link: { href: (id) => `/referee/matches?tournament=${id}`, label: "Moje mecze" },
  },
  [UserRole.Coach]: {
    label: "Trener",
    icon: ClipboardList,
    className: "bg-violet-500/15 text-violet-300",
    hint: "Prowadzisz drużynę w tym turnieju.",
    link: { href: () => "/coach/teams", label: "Moje drużyny" },
  },
  [UserRole.Viewer]: {
    label: "Kibic / rodzic",
    icon: Heart,
    className: "bg-rose-500/15 text-rose-300",
    hint: "Kibicujesz! Śledź terminarz i wyniki.",
  },
}

// Kolejność ról, gdy user ma kilka (np. trener, który też sędziuje).
const PRIORITY: UserRole[] = [UserRole.Organizer, UserRole.Referee, UserRole.Coach, UserRole.Viewer]

const styleOf = (role: UserRole): RoleStyle =>
  ROLE_STYLES[role] ?? { label: role, icon: UserRound, className: "bg-secondary", hint: "" }

export function RoleBadge({ role, className }: { role: UserRole; className?: string }) {
  const { label, icon: Icon, className: roleClass } = styleOf(role)
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium",
        roleClass,
        className,
      )}
    >
      <Icon className="size-3" aria-hidden />
      {label}
    </span>
  )
}

/** Odznaka roli, krótkie zdanie, co user tu robi, i skrót do jego panelu. */
function RoleLine({ role, tournamentId }: { role: UserRole; tournamentId: string }) {
  const { hint, link } = styleOf(role)
  return (
    <>
      <RoleBadge role={role} />
      {hint && <span className="text-muted-foreground">{hint}</span>}
      {link && (
        <Link
          href={link.href(tournamentId)}
          className="text-primary font-medium underline-offset-2 hover:underline"
        >
          {link.label} →
        </Link>
      )}
    </>
  )
}

/** "Jesteś tu jako …" w nagłówku turnieju. Przy kilku rolach każda dostaje własną linię. */
export function MyTournamentRole({
  roles,
  tournamentId,
}: {
  roles: UserRole[]
  tournamentId: string
}) {
  if (roles.length === 0) return null
  const rank = (role: UserRole) => PRIORITY.indexOf(role) + 1 || PRIORITY.length + 1
  const sorted = [...new Set(roles)].sort((a, b) => rank(a) - rank(b))

  if (sorted.length === 1) {
    return (
      <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
        <span className="text-muted-foreground">Jesteś tu jako</span>
        <RoleLine role={sorted[0]!} tournamentId={tournamentId} />
      </p>
    )
  }
  return (
    <div className="space-y-1.5 text-sm">
      <p className="text-muted-foreground">Masz tu kilka ról:</p>
      <ul className="space-y-1.5">
        {sorted.map((role) => (
          <li key={role} className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <RoleLine role={role} tournamentId={tournamentId} />
          </li>
        ))}
      </ul>
    </div>
  )
}
