"use client"

import { Monitor, Moon, Sun } from "lucide-react"
import { useSession } from "next-auth/react"
import { useTheme } from "next-themes"
import { useSyncExternalStore } from "react"

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { type ThemeChoice, useSetMyTheme } from "@/features/account/api"
import { cn } from "@/lib/utils"

const OPTIONS: { value: ThemeChoice; label: string; icon: typeof Sun }[] = [
  { value: "system", label: "Jak w systemie", icon: Monitor },
  { value: "light", label: "Jasny", icon: Sun },
  { value: "dark", label: "Ciemny", icon: Moon },
]

const noopSubscribe = () => () => {}

/**
 * Przełącznik motywu na wierzchu każdej strony (panel, portal, mecz na żywo, logowanie, pomoc), nie w ustawieniach.
 * Zalogowany zapisuje wybór też na koncie, kibic bez konta tylko w przeglądarce.
 */
export function ThemeToggle({ className }: { className?: string }) {
  const { theme, resolvedTheme, setTheme } = useTheme()
  const { status } = useSession()
  const saveOnAccount = useSetMyTheme()
  // Motyw znamy dopiero w przeglądarce, więc do hydratacji ikona jest neutralna.
  const mounted = useSyncExternalStore(
    noopSubscribe,
    () => true,
    () => false,
  )

  const Icon = !mounted ? Monitor : resolvedTheme === "dark" ? Moon : Sun

  const choose = (value: ThemeChoice) => {
    setTheme(value)
    if (status === "authenticated" && value !== theme) saveOnAccount.mutate(value)
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label="Motyw"
        title="Motyw"
        className={cn(
          "focus-visible:ring-ring flex size-10 items-center justify-center rounded-full transition-colors outline-none hover:bg-white/10 focus-visible:ring-2",
          className,
        )}
      >
        <Icon className="size-5" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-48">
        <DropdownMenuLabel>Motyw</DropdownMenuLabel>
        <DropdownMenuRadioGroup
          value={mounted ? theme : undefined}
          onValueChange={(value) => choose(value as ThemeChoice)}
        >
          {OPTIONS.map(({ value, label, icon: OptionIcon }) => (
            <DropdownMenuRadioItem key={value} value={value}>
              <OptionIcon />
              {label}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
