"use client"

import { Tags } from "lucide-react"
import Link from "next/link"

import { cn } from "@/lib/utils"
import type { TeamLabel } from "@/types/teams"

import {
  ALL_TEAMS,
  countByFilter,
  isAllTeams,
  type LabelFilter,
  MANY_TEAMS,
  NO_LABEL,
  toggleLabel,
} from "../team-labels"

type TeamLabelFilterProps = {
  labels: TeamLabel[]
  teams: { labelIds: string[] }[]
  value: LabelFilter
  onChange: (filter: LabelFilter) => void
}

/**
 * Przełączniki etykiet nad listą drużyn: można włączyć kilka naraz (drużyny z którąkolwiek z nich),
 * „Wszystkie” czyści wybór. Bez etykiet nic nie pokazuje.
 */
export function TeamLabelFilter({ labels, teams, value, onChange }: TeamLabelFilterProps) {
  if (labels.length === 0) return null
  const counts = countByFilter(teams)
  const options = [
    ...labels.map((l) => ({
      key: l.id,
      label: l.name,
      count: counts.byLabel.get(l.id) ?? 0,
      active: value.labelIds.includes(l.id),
    })),
    { key: NO_LABEL, label: "Bez etykiety", count: counts.none, active: value.none },
  ]

  return (
    <div role="group" aria-label="Etykiety" className="flex flex-wrap gap-1.5">
      <Chip
        label="Wszystkie"
        count={counts.all}
        active={isAllTeams(value)}
        onClick={() => onChange(ALL_TEAMS)}
      />
      {options.map((o) => (
        <Chip
          key={o.key}
          label={o.label}
          count={o.count}
          active={o.active}
          onClick={() => onChange(toggleLabel(value, o.key))}
        />
      ))}
    </div>
  )
}

function Chip({
  label,
  count,
  active,
  onClick,
}: {
  label: string
  count: number
  active: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      className={cn(
        "inline-flex max-w-full items-center gap-1.5 rounded-full border px-3 py-1 text-sm transition-colors",
        active
          ? "border-primary bg-primary text-primary-foreground"
          : "hover:bg-accent text-foreground",
      )}
    >
      <span className="truncate">{label}</span>
      <span className={cn("text-xs tabular-nums", active ? "opacity-80" : "text-muted-foreground")}>
        {count}
      </span>
    </button>
  )
}

/** Podpowiedź przy dużej bazie bez etykiet: lista szybko robi się długa. */
export function TeamLabelsHint({
  organizationId,
  teamCount,
  labelCount,
}: {
  organizationId: string
  teamCount: number
  labelCount: number
}) {
  if (teamCount <= MANY_TEAMS || labelCount > 0) return null
  return (
    <p className="bg-muted/50 text-muted-foreground flex items-start gap-2 rounded-md border px-3 py-2 text-sm">
      <Tags className="mt-0.5 size-4 shrink-0" aria-hidden />
      <span>
        Masz dużo drużyn. Pogrupuj je etykietami, np. rocznik albo klub, a łatwiej je znajdziesz.{" "}
        <Link
          href={`/organizations/${organizationId}/teams?tab=labels`}
          className="text-primary font-medium hover:underline"
        >
          Dodaj etykiety
        </Link>
      </span>
    </p>
  )
}
