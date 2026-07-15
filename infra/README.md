# Infrastructure

Infrastructure definitions are kept separate from application source code.

- `compose` contains local and test environments.
- `docker` contains deployable API/worker images and judge runtime images.
- `postgres` contains database operations documentation and optional init data.
- `monitoring` will contain dashboards and alert definitions.

PostgreSQL is the only external data service required by the MVP. Redis and
RabbitMQ are not enabled by default. If load testing establishes a need for
distributed caching/rate limiting or a message broker, add the service through
an ADR and an opt-in Compose profile.
