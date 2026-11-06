# Obraz produkcyjny API. Build z katalogu Turniejo-backend:
#   docker build -t turniejo-api .
# Testy nie wchodzą do obrazu — .dockerignore wycina tests/, więc restore/publish dotyczy tylko Turniejo.Api
# i jego zależności (moduły + SharedKernel).

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/ src/
RUN dotnet restore src/Turniejo.Api/Turniejo.Api.csproj
RUN dotnet publish src/Turniejo.Api/Turniejo.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Katalog na wgrane herby (wolumen w docker-compose.prod.yml). Nowy wolumen przejmuje właściciela
# z obrazu, więc API pisze tam bez roota.
RUN mkdir -p /data/uploads && chown "$APP_UID" /data/uploads

# Obrazy .NET 8+ mają wbudowanego usera bez uprawnień roota (APP_UID). Poza katalogiem na herby
# proces API nie pisze nic na dysk, więc nie ma powodu, żeby działał jako root w kontenerze.
USER $APP_UID

# Workstation GC zamiast serwerowego — na małym VPS serwerowy GC rezerwuje sterty per rdzeń
# i zjada kilkaset MB RAM-u, którego potrzebują też Postgres i Keycloak.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0 \
    Uploads__Path=/data/uploads

COPY --from=build /app .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Turniejo.Api.dll"]
