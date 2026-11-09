import type { Metadata } from "next"

import { RELEASES } from "@/features/whats-new/releases"

export const metadata: Metadata = {
  title: "Co nowego",
  description: "Lista zmian w kolejnych wersjach Turniejo.",
}

const dateFormatter = new Intl.DateTimeFormat("pl-PL", { dateStyle: "long", timeZone: "Europe/Warsaw" })

// Pełna historia wydań z CHANGELOG.md (okno „Co nowego” pokazuje tylko nowe i zmienione funkcje).
export default function ChangelogPage() {
  return (
    <article className="space-y-8">
      <h1 className="text-2xl font-semibold tracking-tight">Co nowego</h1>
      {RELEASES.map((release) => (
        <section key={release.version} id={release.version} className="scroll-mt-20 space-y-3">
          <header>
            <h2 className="text-lg font-semibold">Wersja {release.version}</h2>
            {release.date && (
              <p className="text-muted-foreground text-sm">
                {dateFormatter.format(new Date(`${release.date}T12:00:00Z`))}
              </p>
            )}
          </header>
          {release.intro && <p className="text-muted-foreground text-sm">{release.intro}</p>}
          {release.sections.map((section) => (
            <div key={section.category} className="space-y-1">
              <h3 className="text-sm font-medium">{section.category}</h3>
              <ul className="text-muted-foreground list-disc space-y-1 pl-5 text-sm leading-relaxed">
                {section.items.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </div>
          ))}
        </section>
      ))}
    </article>
  )
}
