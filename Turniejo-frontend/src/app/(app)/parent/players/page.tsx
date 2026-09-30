import type { Metadata } from "next"

import { MyChildrenView } from "./my-children"

export const metadata: Metadata = { title: "Moje dzieci" }

export default function MyChildrenPage() {
  return <MyChildrenView />
}
