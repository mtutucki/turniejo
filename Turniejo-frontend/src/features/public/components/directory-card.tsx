import { CalendarDays, Shield } from "lucide-react"
import Link from "next/link"

import { formatDateRange, plural } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { PublicTournamentListItem } from "@/types/public"

import { type DirectorySection, startsInLabel } from "../directory"

const MAX_CATEGORIES = 3

function StatusBadge({
  tournament,
  section,
}: {
  tournament: PublicTournamentListItem
  section: DirectorySection
}) {
  if (tournament.liveMatchCount > 0) {
    return (
      <span className="text-live flex items-center gap-1.5 text-xs font-semibold">
        <span className="relative flex size-2">
          <span className="bg-live absolute inline-flex size-full animate-ping rounded-full opacity-75" />
          <span className="bg-live relative inline-flex size-2 rounded-full" />
        </span>
        {plural(tournament.liveMatchCount, ["mecz na żywo", "mecze na żywo", "meczów na żywo"])}
      </span>
    )
  }
  const label =
    section === "live"
      ? "Trwa"
      : section === "finished"
        ? "Zakończony"
        : tournament.status === "RegistrationOpen"
          ? "Zapisy otwarte"
          : startsInLabel(tournament.startAt)
  if (!label) return null
  return (
    <span
      className={cn(
        "rounded-full px-2 py-0.5 text-xs font-medium",
        section === "finished" ? "bg-muted text-muted-foreground" : "bg-primary/10 text-primary",
      )}
    >
      {label}
    </span>
  )
}

export function DirectoryCard({
  tournament,
  section,
  onOrganizerClick,
}: {
  tournament: PublicTournamentListItem
  section: DirectorySection
  onOrganizerClick: (organizerId: string) => void
}) {
  const extraCategories = tournament.categories.length - MAX_CATEGORIES
  return (
    <article
      aria-label={tournament.name}
      className={cn(
        "bg-card relative flex flex-col gap-3 rounded-xl border p-4 transition-shadow hover:shadow-md",
        tournament.liveMatchCount > 0 && "border-live/40",
        section === "finished" && "opacity-90",
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <h3 className="min-w-0 leading-snug font-semibold break-words">
          {/* Link rozciągnięty na całą kartę; organizator nad nim jest osobnym przyciskiem. */}
          <Link
            href={`/t/${tournament.slug}`}
            className="after:absolute after:inset-0 after:rounded-xl"
          >
            {tournament.name}
          </Link>
        </h3>
        <div className="shrink-0 pt-0.5">
          <StatusBadge tournament={tournament} section={section} />
        </div>
      </div>

      <button
        type="button"
        onClick={() => onOrganizerClick(tournament.organizer.id)}
        className="text-muted-foreground hover:text-foreground relative z-10 -mt-2 self-start text-left text-sm hover:underline"
        title="Pokaż turnieje tego organizatora"
      >
        {tournament.organizer.name}
      </button>

      <div className="text-muted-foreground mt-auto flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
        <span className="flex items-center gap-1.5">
          <CalendarDays className="size-4" />
          {formatDateRange(tournament.startAt, tournament.endAt)}
        </span>
        {tournament.teamCount > 0 && (
          <span className="flex items-center gap-1.5">
            <Shield className="size-4" />
            {plural(tournament.teamCount, ["drużyna", "drużyny", "drużyn"])}
          </span>
        )}
      </div>

      {tournament.categories.length > 0 && (
        <ul className="flex flex-wrap gap-1.5" aria-label="Kategorie">
          {tournament.categories.slice(0, MAX_CATEGORIES).map((name) => (
            <li key={name} className="bg-muted rounded-md px-2 py-0.5 text-xs font-medium">
              {name}
            </li>
          ))}
          {extraCategories > 0 && (
            <li className="text-muted-foreground px-1 py-0.5 text-xs">+{extraCategories}</li>
          )}
        </ul>
      )}
    </article>
  )
}
