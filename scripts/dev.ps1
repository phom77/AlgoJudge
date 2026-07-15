$ErrorActionPreference = "Stop"

if (-not (Test-Path ".env")) {
    throw "Missing .env. Copy .env.example to .env and set local secrets first."
}

docker compose --env-file .env -f infra/compose/compose.dev.yml up -d postgres

Write-Host "PostgreSQL is starting. Run the API and worker in separate terminals:"
Write-Host "./scripts/run-api.ps1"
Write-Host "./scripts/run-worker.ps1"
