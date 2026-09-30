// Mock backendu dla E2E: handlery MSW wystawione jako prawdziwy serwer HTTP (Next.js woła go
// z serwera — Auth.js/Keycloak, SSR portalu — i przez rewrite /backend z przeglądarki), plus
// WebSockety SignalR. Uruchamiany przez Playwright (webServer), bez .NET i Keycloaka.
import { createMiddleware } from "@mswjs/http-middleware"
import express from "express"

import { handlers } from "./handlers"
import { attachSignalR } from "./signalr"

const PORT = Number(process.env.MOCK_PORT ?? 4010)

const app = express()
app.use(createMiddleware(...handlers))
// Brak handlera = błąd testu, nie cicha pusta odpowiedź — widać go w logu Playwrighta.
app.use((req, res) => {
  console.warn(`[mock] brak handlera: ${req.method} ${req.url}`)
  res.status(404).json({ status: 404, title: "Mock not found", detail: `${req.method} ${req.url}` })
})

const server = app.listen(PORT, () =>
  console.log(`[mock] backend + keycloak na http://localhost:${PORT}`),
)
attachSignalR(server)
