# Repository Structure

## Top-level ownership

| Path | Responsibility |
|---|---|
| `src` | Deployable .NET applications and their shared backend layers. |
| `web` | Independent browser application and frontend tests. |
| `tests` | Backend unit and integration test projects. |
| `content` | Local/private problem packages consumed by ContentTool. |
| `infra` | Local runtime, container images, database operations, and monitoring. |
| `docs` | Product requirements and durable technical decisions. |
| `scripts` | Repeatable developer workflows; no business logic. |

## Backend dependency direction

```text
AlgoJudge.API ---------> AlgoJudge.Application ---------> AlgoJudge.Domain
       |                           ^
       v                           |
AlgoJudge.Infrastructure ---------+

AlgoJudge.Worker ------> AlgoJudge.Application
       |
       v
AlgoJudge.Infrastructure
```

- API owns HTTP concerns and authentication middleware.
- Worker owns polling/queue consumption and process lifetime.
- Application owns use cases and ports/interfaces.
- Domain owns business concepts and invariants.
- Infrastructure owns EF Core, PostgreSQL repositories, queue implementations,
  and sandbox adapters.
- ContentTool is an internal executable and must not become a public admin API.

## External services

PostgreSQL is part of the default development environment. Redis and RabbitMQ
must not be added as implicit dependencies. Introduce either service only after
an accepted ADR identifies its owner, failure behaviour, local configuration,
production configuration, and removal strategy.

Optional external services belong under `infra` and should use opt-in Compose
profiles so a normal developer setup remains small.
