import { ImageResponse } from "next/og"

import { Badge, clip, OG_COLORS, OG_SIZE, OgCard } from "@/features/public/og-card"
import { tournamentDates } from "@/features/public/seo"
import { getPublicTournament } from "@/features/public/server"
import { TOURNAMENT_STATUS_LABELS } from "@/types/tournaments"

export const alt = "Turniej piłkarski w Turniejo"
export const size = OG_SIZE
export const contentType = "image/png"

// Podgląd linku do portalu turnieju i jego podstron (Terminarz, Tabele…).
export default async function Image({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params
  const t = await getPublicTournament(slug)
  const categories = [...t.categories].sort((a, b) => a.order - b.order).map((c) => c.name)
  const live = t.status === "InProgress"
  const badge =
    t.status === "Finished" || t.status === "Cancelled" || live ? (
      <Badge live={live}>{live ? "Trwa teraz" : TOURNAMENT_STATUS_LABELS[t.status]}</Badge>
    ) : undefined

  return new ImageResponse(
    <OgCard badge={badge}>
      <div
        style={{
          display: "flex",
          fontSize: t.name.length > 32 ? 68 : 84,
          lineHeight: 1.05,
          letterSpacing: -2,
        }}
      >
        {clip(t.name, 60)}
      </div>
      <div style={{ display: "flex", marginTop: 28, fontSize: 40, color: OG_COLORS.muted }}>
        {tournamentDates(t.startAt, t.endAt)}
      </div>
      {categories.length > 0 && (
        <div style={{ display: "flex", flexWrap: "wrap", gap: 12, marginTop: 28 }}>
          {categories.slice(0, 6).map((name) => (
            <div
              key={name}
              style={{
                display: "flex",
                padding: "6px 18px",
                borderRadius: 12,
                background: OG_COLORS.card,
                border: `2px solid ${OG_COLORS.border}`,
                fontSize: 28,
              }}
            >
              {name}
            </div>
          ))}
          {categories.length > 6 && (
            <div
              style={{ display: "flex", padding: "6px 0", fontSize: 28, color: OG_COLORS.muted }}
            >
              +{categories.length - 6}
            </div>
          )}
        </div>
      )}
    </OgCard>,
    size,
  )
}
