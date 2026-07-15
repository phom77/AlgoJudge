# AlgoJudge

AlgoJudge is a focused online judge for practising algorithmic problems. The
MVP accepts a solution only when it passes every testcase; it has no numeric
scoring or Teacher role.

## Repository layout

- `src` — .NET API, worker, application, domain, infrastructure, and content CLI
- `web` — browser application scaffold
- `tests` — backend test-suite boundaries
- `content` — local problem-package entry point
- `infra` — Compose, container images, database operations, and monitoring
- `docs` — product, domain, judge, architecture, and roadmap documents
- `scripts` — repeatable local development commands

See [docs/repository-structure.md](docs/repository-structure.md) for ownership
rules and dependency direction.

## Current prerequisites

- .NET SDK 10
- Docker Desktop or a compatible Docker Engine
- PowerShell for the provided local scripts

## Local backend setup

1. Copy `.env.example` to `.env` and replace the local secrets.
2. Run `./scripts/dev.ps1` to start PostgreSQL.
3. Apply EF Core migrations.
4. Run `./scripts/run-api.ps1`.
5. Run `./scripts/run-worker.ps1` in another terminal.

The frontend will use Angular and will be scaffolded only after the backend API
and judge contract are stable. The production queue implementation is not
selected yet and requires a separate ADR.

The MVP scope reset replaced the original migration history. A local database
created from the pre-reset migrations must be recreated before applying the new
`InitialCreate` migration.
