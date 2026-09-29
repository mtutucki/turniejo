import { AppSidebar } from "@/components/layout/app-sidebar"
import { BottomNav } from "@/components/layout/bottom-nav"
import { Topbar } from "@/components/layout/topbar"

export default function AppLayout({ children }: LayoutProps<"/">) {
  return (
    <div className="flex min-h-full flex-1 flex-col">
      <AppSidebar />
      <div className="flex flex-1 flex-col md:pl-60">
        <Topbar />
        {/* pb-20: miejsce na dolną nawigację na mobile. */}
        <main className="mx-auto w-full max-w-6xl flex-1 p-4 pb-24 md:p-6">{children}</main>
      </div>
      <BottomNav />
    </div>
  )
}
