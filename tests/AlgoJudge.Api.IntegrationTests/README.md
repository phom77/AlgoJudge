# API integration tests

The HTTP integration suite boots the real API host and uses a temporary,
migrated PostgreSQL database. Set `TEST_POSTGRES_CONNECTION` to an administrative
test connection string before running the suite. Each PostgreSQL test creates
and drops its own database.

The suite verifies liveness/readiness, PostgreSQL-backed catalogue access,
Problem Details, authentication challenges, rate limiting, and the versioned
OpenAPI contract. Tests that require PostgreSQL are skipped when the environment
variable is absent; CI provides it through the PostgreSQL service container.
