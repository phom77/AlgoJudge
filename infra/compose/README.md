# Compose Environments

`compose.dev.yml` starts PostgreSQL by default and can build the API through the
optional `app` profile. The grading worker currently runs on the host so it can
reach the developer's Docker Engine without mounting the Docker socket into a
container.

`compose.test.yml` provides an ephemeral PostgreSQL instance for integration
tests.

The production worker-to-sandbox integration requires a dedicated security ADR
before the worker is enabled as a Compose service. Mounting the host Docker
socket directly would grant excessive host control and is not the default.
