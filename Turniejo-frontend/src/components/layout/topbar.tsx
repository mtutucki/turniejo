import { Logo } from "./logo"
import { UserMenu } from "./user-menu"

export function Topbar() {
  return (
    <header className="bg-background/95 sticky top-0 z-20 flex h-14 items-center justify-between border-b px-4 backdrop-blur md:justify-end md:px-6">
      {/* Na desktopie logo jest w sidebarze. */}
      <div className="md:hidden">
        <Logo />
      </div>
      <UserMenu />
    </header>
  )
}
