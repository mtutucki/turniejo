import type { MetadataRoute } from "next"

import { SITE_NAME } from "@/lib/site"

// Dodanie do ekranu głównego telefonu (sędzia, kibic na trybunach) i nazwa aplikacji w wyszukiwarce.
export default function manifest(): MetadataRoute.Manifest {
  return {
    name: `${SITE_NAME}: turnieje piłkarskie na żywo`,
    short_name: SITE_NAME,
    description: "Wyniki, terminarze i tabele turniejów piłkarskich na żywo.",
    lang: "pl",
    start_url: "/",
    display: "standalone",
    background_color: "#f5f6f8",
    theme_color: "#f5f6f8",
    icons: [
      { src: "/favicon.ico", sizes: "any", type: "image/x-icon" },
      { src: "/apple-icon", sizes: "180x180", type: "image/png" },
    ],
  }
}
