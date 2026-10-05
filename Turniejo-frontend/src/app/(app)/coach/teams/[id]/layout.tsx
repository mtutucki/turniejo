import { TeamShell } from "./team-shell"

export default function CoachTeamLayout({ children }: LayoutProps<"/coach/teams/[id]">) {
  return <TeamShell>{children}</TeamShell>
}
