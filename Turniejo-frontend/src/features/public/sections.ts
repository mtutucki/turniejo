import type { PublicTournament } from "@/types/public"

/** Zakładki portalu kibica, które organizator pokazuje. Wspólne dla nagłówka i sitemapy. */
export function publicSections(tournament: PublicTournament) {
  const s = tournament.settings
  const hasKnockout = tournament.format === "Knockout" || tournament.format === "GroupAndKnockout"
  return [
    // Strona główna portalu istnieje zawsze — przy ukrytym terminarzu pokazuje komunikat.
    { segment: "", label: "Mecze", visible: true },
    { segment: "schedule", label: "Terminarz", visible: s.showSchedule },
    // Puchar nie ma fazy grupowej — nie ma tabel do pokazania.
    {
      segment: "standings",
      label: "Tabele",
      visible: s.showStandings && tournament.format !== "Knockout",
    },
    { segment: "bracket", label: "Drabinka", visible: s.showBracket && hasKnockout },
    { segment: "teams", label: "Drużyny", visible: s.showTeams },
    { segment: "stats", label: "Statystyki", visible: s.showStats },
  ].filter((section) => section.visible)
}
