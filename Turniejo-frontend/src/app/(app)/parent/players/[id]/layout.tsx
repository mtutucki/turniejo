import { ChildShell } from "./child-shell"

export default function ChildLayout({ children }: LayoutProps<"/parent/players/[id]">) {
  return <ChildShell>{children}</ChildShell>
}
