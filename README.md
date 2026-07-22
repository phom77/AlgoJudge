# AlgoJudge

AlgoJudge is a focused online judge for practising algorithmic problems. The
MVP accepts a solution only when it passes every testcase; it has no numeric
scoring or Teacher role.

## Repository layout

- `src` — .NET API, grading/content workers, application, domain, infrastructure, and content CLI
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
- [Internal problem authoring API](docs/problem-authoring-api.md)
- [Judge specification](docs/judge-spec.md)
- [Backend operations baseline](docs/backend-operations.md)
- [Delivery roadmap](docs/roadmap.md)

## Current prerequisites

- .NET SDK 10
- Node.js 20.20.2 and npm 10.8.2 for the Angular client
- Docker Desktop or a compatible Docker Engine
- PowerShell for the provided local scripts

## Local backend setup

1. Copy `.env.example` to `.env` and replace the local secrets.
2. Run `./scripts/dev.ps1` to start PostgreSQL.
3. Apply EF Core migrations.
4. Run `./scripts/build-judge-image.ps1` once to build the pinned C++17 judge
   image.
5. Run `./scripts/seed-dev-content.ps1` once to import and publish the local
   Two Sum fixture.
6. Run `./scripts/run-api.ps1`.
7. Run `./scripts/run-worker.ps1` in another terminal.
8. For problem authoring, configure a maintainer user UUID and run
   `./scripts/run-content-worker.ps1` in a third terminal.

Run `./scripts/test-backend-e2e.ps1` to exercise the complete backend acceptance
flow against an ephemeral PostgreSQL database and the pinned Docker judge image.

## Local frontend setup

1. Start the API on `http://localhost:5016`.
2. Run `npm ci` from `web`.
3. Run `npm start` from `web` and open `http://localhost:4200`.

The Angular development proxy keeps `/api` calls and secure browser sessions on
one origin. See [web/README.md](web/README.md) for frontend commands and
[web/AGENTS.md](web/AGENTS.md) for architecture and security rules.

The MVP worker uses PostgreSQL as its durable
submission queue, with atomic claims, renewable leases, and bounded retries;
Redis and RabbitMQ are not required at this stage. See
[ADR-0007](docs/adr/0007-use-postgresql-submission-queue.md).

The MVP scope reset replaced the original migration history. A local database
created from the pre-reset migrations must be recreated before applying the new
`InitialCreate` migration.
