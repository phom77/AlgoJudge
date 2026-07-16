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

## Documentation

- [Product requirements](docs/requirements.md)
- [Domain model](docs/domain-model.md)
- [Problem Catalogue API](docs/problem-catalog-api.md)
- [Authentication and Submission API](docs/auth-submission-api.md)
- [Problem package format](docs/problem-package-format.md)
- [Judge specification](docs/judge-spec.md)
- [Backend operations baseline](docs/backend-operations.md)
- [Delivery roadmap](docs/roadmap.md)

## Current prerequisites

- .NET SDK 10
- Docker Desktop or a compatible Docker Engine
- PowerShell for the provided local scripts

## Local backend setup

1. Copy `.env.example` to `.env` and replace the local secrets.
2. Run `./scripts/dev.ps1` to start PostgreSQL.
3. Apply EF Core migrations.
4. Run `./scripts/build-judge-image.ps1` once to build the pinned C++17 judge
   image.
5. Run `./scripts/run-api.ps1`.
6. Run `./scripts/run-worker.ps1` in another terminal.

Run `./scripts/test-backend-e2e.ps1` to exercise the complete backend acceptance
flow against an ephemeral PostgreSQL database and the pinned Docker judge image.

The frontend will use Angular and will be scaffolded only after the backend API
and judge contract are stable. The MVP worker uses PostgreSQL as its durable
submission queue, with atomic claims, renewable leases, and bounded retries;
Redis and RabbitMQ are not required at this stage. See
[ADR-0007](docs/adr/0007-use-postgresql-submission-queue.md).

The MVP scope reset replaced the original migration history. A local database
created from the pre-reset migrations must be recreated before applying the new
`InitialCreate` migration.
