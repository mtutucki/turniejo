import { Trophy } from "lucide-react"

export default function AuthLayout({ children }: LayoutProps<"/">) {
  return (
    <main className="flex flex-1 flex-col items-center justify-center gap-6 p-4">
      <div className="flex items-center gap-2 text-lg font-semibold tracking-tight">
        <span className="bg-primary text-primary-foreground flex size-9 items-center justify-center rounded-lg">
          <Trophy className="size-5" />
        </span>
        Turniejo
      </div>
      <div className="w-full max-w-sm">{children}</div>
    </main>
  )
}
