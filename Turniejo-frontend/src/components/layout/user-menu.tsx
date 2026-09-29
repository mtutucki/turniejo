"use client"

import { LogOut } from "lucide-react"
import { signOut, useSession } from "next-auth/react"

import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"

function initials(name: string | null | undefined, email: string | null | undefined) {
  const source = name?.trim() || email || "?"
  const parts = source.split(/[\s.@]+/).filter(Boolean)
  return parts
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join("")
}

export function UserMenu() {
  const { data: session } = useSession()
  const user = session?.user

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label="Menu użytkownika"
        className="focus-visible:ring-ring rounded-full outline-none focus-visible:ring-2"
      >
        <Avatar>
          <AvatarFallback>{initials(user?.name, user?.email)}</AvatarFallback>
        </Avatar>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuGroup>
          <DropdownMenuLabel className="flex flex-col gap-0.5">
            <span className="text-foreground truncate text-sm font-medium">
              {user?.name ?? "Użytkownik"}
            </span>
            <span className="truncate text-xs font-normal">{user?.email}</span>
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={() => signOut({ redirectTo: "/login" })}>
          <LogOut />
          Wyloguj się
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
