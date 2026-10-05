import { fileURLToPath } from "node:url"

import { defineConfig } from "vitest/config"

// Testy komponentów (jsdom). E2E (Playwright) leżą w e2e/ i mają własny runner.
export default defineConfig({
  resolve: {
    alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) },
  },
  oxc: { jsx: { runtime: "automatic" } },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    include: ["src/**/*.test.{ts,tsx}"],
    css: false,
  },
})
