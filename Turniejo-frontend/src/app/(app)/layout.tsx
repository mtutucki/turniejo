import { AppSidebar } from "@/components/layout/app-sidebar"
import { BottomNav } from "@/components/layout/bottom-nav"
import { TournamentHeader } from "@/components/layout/tournament-header"

// < 1024px: jedna kolumna + dolna nawigacja.
// ≥ 1024px: sidebar 240px + główny feed.
export default function AppLayout({ children }: LayoutProps<"/">) {
  return (
    <div className="flex min-h-full flex-1 flex-col">
      <AppSidebar />
      <div className="flex min-w-0 flex-1 flex-col lg:pl-60">
        <TournamentHeader />
        {/* pb-24: miejsce na dolną nawigację na mobile. */}
        <main className="min-w-0 flex-1 p-4 pb-24 lg:p-6">{children}</main>
      </div>
      <BottomNav />
    </div>
  )
}
