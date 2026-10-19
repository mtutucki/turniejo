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
  link?: { href: string; label: string }
}

// Bursztyn (--live) jest zarezerwowany dla trwających meczów — role mają własne odcienie.
const ROLE_STYLES: Partial<Record<UserRole, RoleStyle>> = {
  [UserRole.Organizer]: {
    label: "Organizator",
    icon: Crown,
    className: "bg-primary/15 text-primary",
    hint: "zarządzasz turniejem — drużyny, terminarz i ustawienia są w Twoich rękach.",
  },
  [UserRole.Referee]: {
    label: "Sędzia",
    icon: Whistle,
    className: "bg-sky-500/15 text-sky-300",
    hint: "prowadzisz mecze, które przydzieli Ci organizator, i możesz brać wolne.",
    link: { href: "/referee/matches", label: "Moje mecze" },
  },
  [UserRole.Coach]: {
    label: "Trener",
    icon: ClipboardList,
    className: "bg-violet-500/15 text-violet-300",
    hint: "organizator przypisze Cię do drużyny — jej skład i mecze znajdziesz w panelu trenera.",
    link: { href: "/coach/teams", label: "Moje drużyny" },
  },
  [UserRole.Viewer]: {
    label: "Kibic / rodzic",
    icon: Heart,
    className: "bg-rose-500/15 text-rose-300",
    hint: "śledzisz terminarz, wyniki i tabelę. Miłego kibicowania!",
  },
}

// Najważniejsza rola decyduje o podpowiedzi, gdy user ma kilka (np. trener, który też sędziuje).
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

/** "Jesteś tu jako …" w nagłówku turnieju — rola usera i jedno zdanie, co może tu robić. */
export function MyTournamentRole({ roles }: { roles: UserRole[] }) {
  if (roles.length === 0) return null
  const sorted = [...new Set(roles)].sort(
    (a, b) =>
      (PRIORITY.indexOf(a) + 1 || PRIORITY.length + 1) -
      (PRIORITY.indexOf(b) + 1 || PRIORITY.length + 1),
  )
  const main = styleOf(sorted[0]!)
  return (
    <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
      <span className="text-muted-foreground">Jesteś tu jako</span>
      {sorted.map((role) => (
        <RoleBadge key={role} role={role} />
      ))}
      {main.hint && <span className="text-muted-foreground">— {main.hint}</span>}
      {main.link && (
        <Link
          href={main.link.href}
          className="text-primary font-medium underline-offset-2 hover:underline"
        >
          {main.link.label} →
        </Link>
      )}
    </p>
  )
}
