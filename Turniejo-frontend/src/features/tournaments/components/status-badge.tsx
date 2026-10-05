import { Badge } from "@/components/ui/badge"
import { cn } from "@/lib/utils"
import { TOURNAMENT_STATUS_LABELS, type TournamentStatus } from "@/types/tournaments"

// Bursztyn jest zarezerwowany dla meczów LIVE, więc "W trakcie" też jest zielony.
const STATUS_STYLES: Record<TournamentStatus, string> = {
  Draft: "bg-secondary text-muted-foreground",
  RegistrationOpen: "bg-primary/15 text-primary",
  RegistrationClosed: "bg-secondary text-foreground",
  ScheduleGenerated: "bg-secondary text-foreground",
  InProgress: "bg-primary text-primary-foreground",
  Finished: "bg-brand text-brand-foreground",
  Cancelled: "bg-destructive/15 text-destructive",
}

export function TournamentStatusBadge({
  status,
  className,
}: {
  status: TournamentStatus
  className?: string
}) {
  return (
    <Badge className={cn("border-transparent", STATUS_STYLES[status], className)}>
      {TOURNAMENT_STATUS_LABELS[status]}
    </Badge>
  )
}
