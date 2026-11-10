import type { Metadata, Viewport } from "next"
import { Geist, Geist_Mono } from "next/font/google"

import { Analytics } from "@/components/analytics"
import { OPEN_GRAPH_DEFAULTS, SITE_NAME, SITE_URL } from "@/lib/site"

import "./globals.css"
import { Providers } from "./providers"

const geistSans = Geist({
  variable: "--font-sans",
  subsets: ["latin", "latin-ext"],
})

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin", "latin-ext"],
})

const DESCRIPTION = "Wyniki, terminarze i tabele turniejów piłkarskich na żywo."

// Adresy względne w metadanych (canonical, obrazki podglądu) stają się bezwzględne względem SITE_URL.
export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  applicationName: SITE_NAME,
  title: { default: SITE_NAME, template: `%s · ${SITE_NAME}` },
  description: DESCRIPTION,
  openGraph: { ...OPEN_GRAPH_DEFAULTS, description: DESCRIPTION },
  twitter: { card: "summary_large_image" },
  formatDetection: { telephone: false },
}

// Pasek przeglądarki na telefonie w kolorze tła motywu systemowego.
export const viewport: Viewport = {
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#f5f6f8" },
    { media: "(prefers-color-scheme: dark)", color: "#121417" },
  ],
}

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    // suppressHydrationWarning: next-themes ustawia klasę motywu na <html> przed hydratacją.
    <html
      lang="pl"
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
      suppressHydrationWarning
    >
      {/* Rozszerzenia przeglądarki (np. ColorZilla: cz-shortcut-listen) dopisują atrybuty do <body>
          przed hydratacją — wyciszamy tylko różnice atrybutów tego jednego elementu. */}
      <body className="flex min-h-full flex-col" suppressHydrationWarning>
        <Providers>{children}</Providers>
        <Analytics />
      </body>
    </html>
  )
}
