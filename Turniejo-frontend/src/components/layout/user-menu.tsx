"use client"

import { LogOut, UserRound } from "lucide-react"
import Link from "next/link"
import { signOut, useSession } from "next-auth/react"

import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { cn } from "@/lib/utils"

function initials(name: string | null | undefined, email: string | null | undefined) {
  const source = name?.trim() || email || "?"
  const parts = source.split(/[\s.@]+/).filter(Boolean)
  return parts
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join("")
}

function logout() {
  void signOut({ redirectTo: "/login" })
}

export function SignOutButton() {
  return (
    <button
      type="button"
      onClick={logout}
      className="text-muted-foreground hover:bg-accent hover:text-accent-foreground flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium"
    >
      <LogOut className="size-4" />
      Wyloguj się
    </button>
  )
}

/** Avatar z menu konta. `showDetails` — pełny wiersz z imieniem i emailem (stopka sidebaru). */
export function UserMenu({ showDetails = false }: { showDetails?: boolean }) {
  const { data: session } = useSession()
  const user = session?.user

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label="Menu użytkownika"
        className={cn(
          "focus-visible:ring-ring flex items-center gap-3 rounded-lg text-left outline-none focus-visible:ring-2",
          showDetails && "hover:bg-accent w-full p-2",
        )}
      >
        <Avatar className="size-8">
          <AvatarFallback className="bg-primary/25 text-foreground text-xs font-semibold">
            {initials(user?.name, user?.email)}
          </AvatarFallback>
        </Avatar>
        {showDetails && (
          <span className="min-w-0 flex-1">
            <span className="block truncate text-sm font-medium">{user?.name ?? "Użytkownik"}</span>
            <span className="text-muted-foreground block truncate text-xs">{user?.email}</span>
          </span>
        )}
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel className="flex flex-col gap-0.5">
          <span className="truncate text-sm font-medium">{user?.name ?? "Użytkownik"}</span>
          <span className="text-muted-foreground truncate text-xs font-normal">{user?.email}</span>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link href="/account">
            <UserRound />
            Moje konto
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem onSelect={logout}>
          <LogOut />
          Wyloguj się
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
