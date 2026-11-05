# Obraz produkcyjny frontendu. Build z katalogu Turniejo-frontend:
#   docker build --build-arg API_URL=http://api:8080 -t turniejo-frontend .
#
# Build argi to wartości wpiekane w `next build` (rewrite /backend i zmienne NEXT_PUBLIC_*) — nie są
# sekretami. Sekrety (NEXTAUTH_SECRET, KEYCLOAK_*) idą wyłącznie jako zmienne środowiskowe przy starcie.

FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

FROM node:22-alpine AS build
WORKDIR /app
ARG API_URL
ARG NEXT_PUBLIC_UMAMI_SCRIPT_URL=""
ARG NEXT_PUBLIC_UMAMI_WEBSITE_ID=""
ARG NEXT_PUBLIC_DEMO_MODE=""
ENV API_URL=$API_URL \
    NEXT_PUBLIC_UMAMI_SCRIPT_URL=$NEXT_PUBLIC_UMAMI_SCRIPT_URL \
    NEXT_PUBLIC_UMAMI_WEBSITE_ID=$NEXT_PUBLIC_UMAMI_WEBSITE_ID \
    NEXT_PUBLIC_DEMO_MODE=$NEXT_PUBLIC_DEMO_MODE \
    NEXT_OUTPUT=standalone \
    NEXT_TELEMETRY_DISABLED=1
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

# Obraz node ma wbudowanego usera "node" — serwer Next nie potrzebuje roota. Cache optymalizatora
# obrazków (.next/cache) musi być zapisywalny dla tego usera.
COPY --from=build --chown=node:node /app/.next/standalone ./
COPY --from=build --chown=node:node /app/.next/static ./.next/static
COPY --from=build --chown=node:node /app/public ./public
USER node

EXPOSE 3000
CMD ["node", "server.js"]
