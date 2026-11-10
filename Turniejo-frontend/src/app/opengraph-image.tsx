import { ImageResponse } from "next/og"

import { OG_COLORS, OG_SIZE, OgCard } from "@/features/public/og-card"

export const alt = "Turniejo: turnieje piłkarskie na żywo"
export const size = OG_SIZE
export const contentType = "image/png"

// Podgląd linku do strony głównej i stron bez własnego obrazka.
export default function Image() {
  return new ImageResponse(
    <OgCard>
      <div style={{ display: "flex", fontSize: 84, lineHeight: 1.05, letterSpacing: -2 }}>
        Turnieje piłkarskie na żywo
      </div>
      <div style={{ display: "flex", marginTop: 28, fontSize: 36, color: OG_COLORS.muted }}>
        Wyniki, terminarze i tabele w jednym miejscu.
      </div>
    </OgCard>,
    size,
  )
}
