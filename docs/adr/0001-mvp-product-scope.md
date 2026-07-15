# ADR-0001: Adopt a focused online-judge MVP

Status: Proposed
Date: 2026-07-15

## Context

The existing backend began as a teacher/student exercise platform with numeric
problem scores and a leaderboard. The intended product is now a LeetCode-like
practice site where a user receives Accepted only by passing all test cases.

## Decision

The MVP will:

- expose a single regular user account type;
- support curated, published problems only;
- support C++17 submissions only;
- use pass/fail verdicts without numeric scoring or score leaderboard; and
- keep content authoring and testcase import outside the public API/UI.

## Consequences

- `Role`, `Score`, teacher-only endpoints, and the score leaderboard are
  removed or replaced during the backend scope reset.
- Content operations require an internal import/publishing workflow.
- The implementation effort focuses on judge correctness and the learner
  practice flow before broader social or competition features.

## Alternatives considered

- Retain teacher/student roles: rejected because they do not serve the initial
  public practice product and widen the authorization surface.
- Keep scoring and ranking: rejected because they add rules and data semantics
  without improving the core Accepted verdict.
- Support many languages immediately: deferred until the C++17 judge is proven.
