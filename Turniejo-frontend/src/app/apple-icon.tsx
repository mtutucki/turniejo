import { ImageResponse } from "next/og"

export const size = { width: 180, height: 180 }
export const contentType = "image/png"

// Ikona na ekranie głównym iPhone'a i w manifeście: puchar na zielonym tle, jak logo w aplikacji.
export default function AppleIcon() {
  return new ImageResponse(
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        width: "100%",
        height: "100%",
        background: "#047857",
      }}
    >
      <svg
        width={112}
        height={112}
        viewBox="0 0 24 24"
        fill="none"
        stroke="#ffffff"
        strokeWidth={2}
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        <path d="M10 14.66v1.626a2 2 0 0 1-.976 1.696A5 5 0 0 0 7 21.978" />
        <path d="M14 14.66v1.626a2 2 0 0 0 .976 1.696A5 5 0 0 1 17 21.978" />
        <path d="M18 9h1.5a1 1 0 0 0 0-5H18" />
        <path d="M4 22h16" />
        <path d="M6 9a6 6 0 0 0 12 0V3a1 1 0 0 0-1-1H7a1 1 0 0 0-1 1z" />
        <path d="M6 9H4.5a1 1 0 0 1 0-5H6" />
      </svg>
    </div>,
    size,
  )
}
