# Compose environments

`compose.dev.yml` runs the API and PostgreSQL together. Copy `.env.example` to
`.env`, set a database password and a JWT secret of at least 32 characters, then
run:

```powershell
docker compose --env-file .env -f infra/compose/compose.dev.yml up --build
```

The API is available at `http://localhost:5016` by default. The Compose-only
`Database:MigrateOnStartup` switch applies migrations after PostgreSQL becomes
healthy. Keep this switch disabled in multi-instance production deployments and
run migrations as a separate release step instead.

`compose.test.yml` provides an ephemeral PostgreSQL instance for local
integration tests.

The grading worker intentionally runs outside Compose so it can use the local
Docker Engine without mounting the Docker socket into an application container.
Build the pinned judge image, then run `./scripts/run-worker.ps1`. The worker
health endpoints listen on `http://localhost:5017/health/live` and
`http://localhost:5017/health/ready` by default.
