import type { NextConfig } from "next"

const nextConfig: NextConfig = {
  // Backend nie ma skonfigurowanego CORS, więc przeglądarka rozmawia z nim przez Next.js
  // (ten sam origin). Prefiks /backend zamiast /api, bo /api/auth/* należy do Auth.js.
  async rewrites() {
    return [{ source: "/backend/:path*", destination: `${process.env.API_URL}/:path*` }]
  },
}

export default nextConfig
