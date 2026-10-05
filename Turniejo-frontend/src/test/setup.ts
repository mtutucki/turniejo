import "@testing-library/jest-dom/vitest"

import { cleanup } from "@testing-library/react"
import { MotionGlobalConfig } from "motion/react"
import { afterEach } from "vitest"

// Animacje wyłączone — test sprawdza stan po zmianie, nie klatki pośrednie.
MotionGlobalConfig.skipAnimations = true

afterEach(() => cleanup())
