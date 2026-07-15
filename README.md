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

The frontend framework and production queue implementation are intentionally
not selected yet. Those decisions will be recorded as ADRs.
