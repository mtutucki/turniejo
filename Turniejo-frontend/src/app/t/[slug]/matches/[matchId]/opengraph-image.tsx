import { ImageResponse } from "next/og"

import { Badge, clip, OG_COLORS, OG_SIZE, OgCard } from "@/features/public/og-card"
import { getPublicMatch, getPublicTournament } from "@/features/public/server"
import { formatDateTime } from "@/lib/format"
import { MATCH_STATUS_LABELS } from "@/types/matches"

export const alt = "Mecz w Turniejo"
export const size = OG_SIZE
export const contentType = "image/png"

// Podgląd linku do meczu: drużyny i wynik (także w trakcie). Nieznany mecz: sam turniej.
export default async function Image({
  params,
}: {
  params: Promise<{ slug: string; matchId: string }>
}) {
  const { slug, matchId } = await params
  const t = await getPublicTournament(slug)
  const m =
    t.settings.showSchedule || t.settings.showLiveMatches
      ? await getPublicMatch(t.id, matchId)
      : undefined

  if (!m) {
    return new ImageResponse(
      <OgCard>
        <div style={{ display: "flex", fontSize: 84, letterSpacing: -2 }}>{clip(t.name, 60)}</div>
      </OgCard>,
      size,
    )
  }

  const live = m.status === "Live" || m.status === "Paused"
  const scored = live || m.status === "Finished"
  const penalties =
    m.homePenaltyScore != null && m.awayPenaltyScore != null
      ? `karne ${m.homePenaltyScore}:${m.awayPenaltyScore}`
      : null
  const team = (name: string, align: "flex-start" | "flex-end") => (
    <div
      style={{
        display: "flex",
        flex: 1,
        justifyContent: align,
        textAlign: align === "flex-end" ? "right" : "left",
        fontSize: 60,
        lineHeight: 1.1,
        letterSpacing: -1,
      }}
    >
      {clip(name, 28)}
    </div>
  )

  return new ImageResponse(
    <OgCard badge={<Badge live={live}>{live ? "Na żywo" : MATCH_STATUS_LABELS[m.status]}</Badge>}>
      <div style={{ display: "flex", fontSize: 32, color: OG_COLORS.muted }}>
        {clip(t.name, 60)}
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: 40, marginTop: 36 }}>
        {team(m.homeTeam.name, "flex-start")}
        <div
          style={{
            display: "flex",
            padding: "8px 32px",
            borderRadius: 20,
            background: OG_COLORS.card,
            border: `2px solid ${live ? OG_COLORS.live : OG_COLORS.border}`,
            fontSize: scored ? 110 : 56,
            color: scored ? OG_COLORS.text : OG_COLORS.muted,
          }}
        >
          {scored ? `${m.homeScore}:${m.awayScore}` : "vs"}
        </div>
        {team(m.awayTeam.name, "flex-end")}
      </div>
      <div style={{ display: "flex", marginTop: 36, fontSize: 32, color: OG_COLORS.muted }}>
        {penalties ?? formatDateTime(m.scheduledAt)}
      </div>
    </OgCard>,
    size,
  )
}
