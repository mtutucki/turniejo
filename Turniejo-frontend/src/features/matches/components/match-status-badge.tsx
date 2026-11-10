import { Badge } from "@/components/ui/badge"
import { cn } from "@/lib/utils"
import { AWAITING_DECISION_LABEL, MATCH_STATUS_LABELS, type MatchStatus } from "@/types/matches"

// Bursztyn (--live) tylko dla meczu, który właśnie trwa (Live/Paused) — reszta w neutralnych kolorach.
const STATUS_STYLES: Record<MatchStatus, string> = {
  Scheduled: "bg-secondary text-muted-foreground",
  Ready: "bg-primary/15 text-primary",
  Live: "bg-live text-live-foreground",
  Paused: "bg-live/20 text-live",
  Finished: "bg-brand text-brand-foreground",
  Postponed: "bg-secondary text-foreground",
  Cancelled: "bg-destructive/15 text-destructive",
}

export function MatchStatusBadge({
  status,
  awaitingDecision = false,
  className,
}: {
  status: MatchStatus
  /** Mecz wstrzymany przez system: zamiast „Przerwa” prośba o rozstrzygnięcie. */
  awaitingDecision?: boolean
  className?: string
}) {
  const waiting = awaitingDecision && status === "Paused"
  return (
    <Badge
      className={cn(
        "border-transparent",
        waiting ? "bg-destructive/15 text-destructive" : STATUS_STYLES[status],
        className,
      )}
    >
      {status === "Live" && (
        <span className="bg-live-foreground size-1.5 animate-pulse rounded-full" aria-hidden />
      )}
      {waiting ? AWAITING_DECISION_LABEL : MATCH_STATUS_LABELS[status]}
    </Badge>
  )
}
