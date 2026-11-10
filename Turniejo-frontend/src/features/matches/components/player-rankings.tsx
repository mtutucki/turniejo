"use client"

import { BarChart3 } from "lucide-react"
import {
  Bar,
  BarChart,
  CartesianGrid,
  LabelList,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts"

import { Skeleton } from "@/components/ui/skeleton"
import type { RankingEntry } from "@/types/public"

// Jedna seria (liczba na zawodnika) → jeden odcień z tokenu --chart-bar (globals.css), dobranego
// pod kontrast kart w każdym motywie.
const BAR_COLOR = "var(--chart-bar)"
const ROW_HEIGHT = 44

export type PlayerRankingKind = "scorers" | "assists" | "mvp"

/** Rankingi zawodników turnieju. Te same w portalu publicznym i w panelu turnieju. */
export const PLAYER_RANKINGS: {
  kind: PlayerRankingKind
  title: string
  unit: [string, string, string]
}[] = [
  { kind: "scorers", title: "Strzelcy", unit: ["gol", "gole", "goli"] },
  { kind: "assists", title: "Asysty", unit: ["asysta", "asysty", "asyst"] },
  { kind: "mvp", title: "MVP meczu", unit: ["raz", "razy", "razy"] },
]

const pluralRules = new Intl.PluralRules("pl-PL")
const unitFor = (count: number, [one, few, many]: [string, string, string]) => {
  const form = pluralRules.select(count)
  return form === "one" ? one : form === "few" ? few : many
}

type Row = RankingEntry & { name: string; team: string | null }

function RankingTooltip({
  active,
  payload,
  unit,
}: {
  active?: boolean
  payload?: { payload: Row }[]
  unit: [string, string, string]
}) {
  const row = payload?.[0]?.payload
  if (!active || !row) return null
  return (
    <div className="bg-popover text-popover-foreground rounded-md border px-3 py-2 text-sm shadow-md">
      <p className="font-medium">{row.name}</p>
      {row.team && <p className="text-muted-foreground text-xs">{row.team}</p>}
      <p className="text-muted-foreground font-mono tabular-nums">
        {row.count} {unitFor(row.count, unit)}
      </p>
    </div>
  )
}

/** Opis słupka: zawodnik i (mniejszą czcionką) drużyna — tekst w kolorach tekstu, nie serii. */
function PlayerTick({ x, y, row }: { x: number; y: number; row: Row | undefined }) {
  if (!row) return null
  const clip = (text: string, max: number) =>
    text.length > max ? `${text.slice(0, max - 1)}…` : text
  return (
    <g transform={`translate(${x},${y})`}>
      <text x={-8} y={row.team ? -3 : 4} textAnchor="end" fill="var(--foreground)" fontSize={12}>
        {clip(row.name, 20)}
      </text>
      {row.team && (
        <text x={-8} y={11} textAnchor="end" fill="var(--muted-foreground)" fontSize={11}>
          {clip(row.team, 22)}
        </text>
      )}
    </g>
  )
}

/** Wykres jednego rankingu. Dane i nazwy drużyn podaje strona (portal albo panel turnieju). */
export function RankingCard({
  title,
  unit,
  ranking,
  teamName,
}: {
  title: string
  unit: [string, string, string]
  ranking: { data?: RankingEntry[]; isPending: boolean }
  /** null, gdy nazwy drużyny nie wolno albo nie da się pokazać. */
  teamName: (tournamentTeamId: string) => string | null
}) {
  const rows: Row[] = (ranking.data ?? [])
    .filter((r) => r.count > 0)
    .sort((a, b) => b.count - a.count)
    .map((r) => ({
      ...r,
      name: `${r.firstName} ${r.lastName}`,
      team: r.tournamentTeamId ? teamName(r.tournamentTeamId) : null,
    }))

  return (
    <section className="bg-card rounded-xl border p-4" aria-label={title}>
      <h2 className="mb-3 font-semibold">{title}</h2>
      {ranking.isPending ? (
        <Skeleton className="h-48" />
      ) : rows.length === 0 ? (
        <p className="text-muted-foreground flex items-center gap-2 py-6 text-sm">
          <BarChart3 className="size-4" />
          Brak danych - ranking pojawi się po pierwszych meczach.
        </p>
      ) : (
        <>
          {/* Wykres jest dla oka; ta sama treść jako lista dla czytników ekranu. */}
          <div aria-hidden style={{ height: rows.length * ROW_HEIGHT + 24 }}>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                // Recharts 3 robi z wykresu element fokusowalny — tu jest ukryty (aria-hidden), więc wyłączamy.
                accessibilityLayer={false}
                data={rows}
                layout="vertical"
                margin={{ top: 0, right: 32, bottom: 0, left: 0 }}
                barCategoryGap={8}
              >
                <CartesianGrid horizontal={false} stroke="var(--border)" strokeOpacity={0.6} />
                <XAxis type="number" allowDecimals={false} hide />
                <YAxis
                  type="category"
                  dataKey="name"
                  width={140}
                  tickLine={false}
                  axisLine={false}
                  tick={(props) => (
                    <PlayerTick x={Number(props.x)} y={Number(props.y)} row={rows[props.index]} />
                  )}
                />
                <Tooltip
                  cursor={{ fill: "var(--accent)", fillOpacity: 0.4 }}
                  content={<RankingTooltip unit={unit} />}
                />
                <Bar
                  dataKey="count"
                  fill={BAR_COLOR}
                  radius={[0, 4, 4, 0]}
                  maxBarSize={18}
                  isAnimationActive={false}
                >
                  <LabelList
                    dataKey="count"
                    position="right"
                    style={{
                      fill: "var(--foreground)",
                      fontSize: 12,
                      fontFamily: "var(--font-geist-mono)",
                    }}
                  />
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
          <ol className="sr-only">
            {rows.map((r) => (
              <li key={r.playerId}>
                {r.name}
                {r.team ? ` (${r.team})` : ""}: {r.count} {unitFor(r.count, unit)}
              </li>
            ))}
          </ol>
        </>
      )}
    </section>
  )
}
