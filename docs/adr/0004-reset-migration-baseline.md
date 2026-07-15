# ADR-0004: Reset the database migration baseline

Status: Accepted
Date: 2026-07-15

## Context

The repository has not reached production and has no database history that must
be preserved. Its original migrations introduced Teacher roles, problem
ownership, and numeric scoring, all of which have been removed from the MVP.

Keeping forward migrations that immediately drop those unused concepts would
make the new baseline harder to understand and maintain.

## Decision

Replace the existing EF Core migration history with one clean `InitialCreate`
migration generated from the accepted MVP scope.

No database command is executed as part of this decision. Developers using a
database created from the old migration history must recreate that local
database before applying the new baseline.

## Consequences

- The migration history contains no legacy Role, Score, Teacher, or problem
  ownership columns.
- Existing disposable local databases are incompatible with the new baseline
  and must be recreated.
- This reset must not be repeated after a shared or production database exists;
  future schema changes use forward-only migrations.

## Alternatives considered

- Add forward migrations to drop the legacy columns: rejected because there is
  no production data to preserve and the obsolete history would become the
  permanent project baseline.
