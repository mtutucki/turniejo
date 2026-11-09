@echo off
start "Turniejo - Frontend" cmd /k "npm --prefix .\Turniejo-frontend run dev"
start "Turniejo - Backend" cmd /k "dotnet run --project .\Turniejo-backend\src\Turniejo.Api"