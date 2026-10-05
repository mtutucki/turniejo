import { TournamentShell } from "./tournament-shell"

export default function TournamentLayout({ children }: LayoutProps<"/tournaments/[id]">) {
  return <TournamentShell>{children}</TournamentShell>
}
