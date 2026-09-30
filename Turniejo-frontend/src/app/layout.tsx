import type { Metadata, Viewport } from "next"
import { Geist, Geist_Mono } from "next/font/google"

import { Analytics } from "@/components/analytics"

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

export const metadata: Metadata = {
  title: { default: "Turniejo", template: "%s · Turniejo" },
  description: "Zarządzanie turniejami piłkarskimi",
}

export const viewport: Viewport = {
  themeColor: "#121417",
}

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="pl"
      className={`dark ${geistSans.variable} ${geistMono.variable} h-full antialiased`}
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
