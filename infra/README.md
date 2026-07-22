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

The C++17 judge image is built from `infra/docker/judge-cpp17.Dockerfile`. Its
GCC base is pinned by digest and includes the native `algojudge-runner` used for
bounded output, monotonic execution timing, and peak-memory reporting. Build it
locally with `./scripts/build-judge-image.ps1` before starting the worker.

The source-authoring pipeline also uses
`infra/docker/content-generator-dotnet.Dockerfile`. Its .NET 10 SDK base is
pinned by digest and is used only to compile and execute generator/validator
source under ContentTool orchestration. Build it with
`./scripts/build-content-generator-image.ps1`; it is not an API or grading
worker dependency.
