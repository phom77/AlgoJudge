# ADR-0003: Organize the product as a structured monorepo

Status: Accepted
Date: 2026-07-15

## Context

The original repository stored four backend projects at the root. The planned
product also needs a browser application, a separately deployable grading
worker, test suites, problem content tooling, and local infrastructure.

## Decision

Use a single repository with these top-level boundaries: `src`, `web`, `tests`,
`content`, `infra`, `docs`, and `scripts`.

The API and Worker are separate executables but continue sharing the
Application, Domain, and Infrastructure projects. External services are
configured under `infra`; they are not represented as application source
folders.

## Consequences

- Backend namespaces can remain stable while project paths move under `src`.
- Frontend and backend can use independent toolchains in one repository.
- CI can run only the workflows affected by a change.
- The worker can scale and fail independently from HTTP request handling.
- Additional infrastructure does not blur application ownership.

## Alternatives considered

- Separate frontend and backend repositories: deferred because it adds release
  coordination overhead before the product has independent teams.
- Split each backend layer into a service: rejected because these layers are
  code boundaries, not independently valuable network services.
