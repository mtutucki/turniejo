// Minimalny serwer SignalR (protokół JSON po WebSocketach) — tyle, ile używa frontend:
// negotiate → handshake → invoke Join*Group (completion) → push zdarzeń + pingi.
import type { IncomingMessage, Server } from "node:http"
import { randomUUID } from "node:crypto"

import { WebSocket, WebSocketServer } from "ws"

const RS = "\u001e" // separator rekordów SignalR

type Connection = { hub: string; socket: WebSocket; groups: Set<string>; ready: boolean }
const connections = new Set<Connection>()

/** Odpowiedź na POST /hubs/:hub/negotiate?negotiateVersion=1. */
export function negotiateResponse() {
  const id = randomUUID()
  return {
    negotiateVersion: 1,
    connectionId: id,
    connectionToken: id,
    availableTransports: [{ transport: "WebSockets", transferFormats: ["Text", "Binary"] }],
  }
}

const send = (socket: WebSocket, message: object) => socket.send(JSON.stringify(message) + RS)

export function attachSignalR(server: Server) {
  const wss = new WebSocketServer({ noServer: true })

  server.on("upgrade", (request: IncomingMessage, socket, head) => {
    const hub = new URL(request.url ?? "/", "http://mock").pathname.match(/^\/hubs\/([a-z]+)/)?.[1]
    if (!hub) return socket.destroy()
    wss.handleUpgrade(request, socket, head, (ws) => {
      const connection: Connection = { hub, socket: ws, groups: new Set(), ready: false }
      connections.add(connection)
      const ping = setInterval(() => connection.ready && send(ws, { type: 6 }), 5000)

      ws.on("message", (data) => {
        for (const raw of data.toString().split(RS).filter(Boolean)) {
          const message = JSON.parse(raw)
          if (!connection.ready && message.protocol) {
            connection.ready = true
            ws.send("{}" + RS) // handshake OK
            continue
          }
          if (message.type === 1) {
            // Invocation (JoinMatchGroup/JoinTournamentGroup) — zapamiętaj grupę, odeślij completion.
            const [groupId] = message.arguments ?? []
            if (groupId) connection.groups.add(String(groupId).toLowerCase())
            if (message.invocationId)
              send(ws, { type: 3, invocationId: message.invocationId, result: null })
          }
          if (message.type === 7) ws.close()
        }
      })
      ws.on("close", () => {
        clearInterval(ping)
        connections.delete(connection)
      })
    })
  })
}

/** Wysyła zdarzenie do połączeń huba w danej grupie (id turnieju/meczu). */
export function publish(hub: string, group: string, target: string, payload: unknown) {
  let delivered = 0
  for (const c of connections) {
    if (c.hub !== hub || !c.ready || !c.groups.has(group.toLowerCase())) continue
    send(c.socket, { type: 1, target, arguments: [payload] })
    delivered++
  }
  return delivered
}

/** Połączenia, które dołączyły do grupy — dopiero one dostają zdarzenia (publish). */
export const connectionCount = (hub: string) =>
  [...connections].filter((c) => c.hub === hub && c.ready && c.groups.size > 0).length

/** Grupy (id turnieju/meczu), do których dołączyło któreś połączenie huba. */
export const joinedGroups = (hub: string) => [
  ...new Set(
    [...connections].filter((c) => c.hub === hub && c.ready).flatMap((c) => [...c.groups]),
  ),
]

/** Reset między testami — zamyka połączenia poprzedniej strony, żeby liczniki dotyczyły bieżącej. */
export function closeAll() {
  for (const c of connections) c.socket.terminate()
  connections.clear()
}
