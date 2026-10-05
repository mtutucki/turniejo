#!/bin/sh
# Uruchamiane przez obraz postgres tylko raz — przy pierwszym starcie z pustym wolumenem danych.
# Osobne bazy i userzy: wyciek hasła jednej usługi nie daje dostępu do danych drugiej.
set -eu

psql -v ON_ERROR_STOP=1 \
    --username "$POSTGRES_USER" \
    --dbname postgres \
    -v app_password="$APP_DB_PASSWORD" \
    -v keycloak_password="$KEYCLOAK_DB_PASSWORD" <<'SQL'
CREATE ROLE turniejo_app LOGIN PASSWORD :'app_password';
CREATE DATABASE turniejo OWNER turniejo_app;
REVOKE CONNECT ON DATABASE turniejo FROM PUBLIC;

CREATE ROLE keycloak LOGIN PASSWORD :'keycloak_password';
CREATE DATABASE keycloak OWNER keycloak;
REVOKE CONNECT ON DATABASE keycloak FROM PUBLIC;
SQL
