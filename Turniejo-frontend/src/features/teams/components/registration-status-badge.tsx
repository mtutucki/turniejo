import { Badge } from "@/components/ui/badge"
import { cn } from "@/lib/utils"
import { REGISTRATION_STATUS_LABELS, type RegistrationStatus } from "@/types/teams"

const STATUS_STYLES: Record<RegistrationStatus, string> = {
  Pending: "bg-secondary text-foreground",
  Accepted: "bg-primary/15 text-primary",
  Rejected: "bg-destructive/15 text-destructive",
  Withdrawn: "bg-secondary text-muted-foreground",
}

export function RegistrationStatusBadge({
  status,
  className,
}: {
  status: RegistrationStatus
  className?: string
}) {
  return (
    <Badge className={cn("border-transparent", STATUS_STYLES[status], className)}>
      {REGISTRATION_STATUS_LABELS[status]}
    </Badge>
  )
}
