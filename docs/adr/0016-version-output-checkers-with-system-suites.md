# ADR-0016: Version output checkers with system suites

Status: Accepted
Date: 2026-07-25

## Context

The original judge compared trimmed output strings despite the token-based
contract in the judge specification. Function authoring records JSON comparator
provenance, but the selected comparator was not persisted or used by grading.
Changing a problem's comparison rule in place would also make a queued or
retried submission pinned to an older hidden suite non-reproducible.

Some otherwise ordinary problems require JSON structural output or finite
floating-point tolerance. Arbitrary checker source would have to run in the
grading worker, weakening the boundary that keeps authoring source out of the
API and judge process.

## Decision

Persist one `SystemTestSuite` record per `(problemId, version)`. It owns one
platform implementation and optional numeric tolerances. `JudgeTestCase` rows
reference the suite by that composite key, and the suite provider returns its
checker together with ordered private cases.

The initial closed checker set is `TokenExact`, `JsonExact`, and
`FloatingPoint`. Token comparison uses Unicode whitespace. JSON comparison is
structural. Floating-point comparison accepts only finite
invariant-culture tokens and uses absolute-or-relative tolerance. Invalid
checker configuration is rejected by domain validation and database checks.

Schema-version-3 packages explicitly choose a checker. Schema-version-1 and
2 packages, and every existing suite backfilled by the migration, use
`TokenExact`. Version-1 source-authored Function publication selects
`JsonExact`. Publishing a new suite version is required to change a checker.

## Consequences

- Comparison semantics are reproducible for pinned submissions and remain
  private with hidden testcase data.
- The worker only invokes platform code over bounded strings; it does not
  compile or execute checker source.
- Content packages can express well-defined numeric tolerance without changing
  public APIs.
- Adding a checker requires a code release, tests, documentation, and a new
  enum/database constraint migration if the persisted set changes.

## Alternatives considered

- Store a mutable checker on `Problem`: rejected because pinned submissions
  could observe changed verdict semantics.
- Run maintainer-supplied checker source in the grading worker: rejected
  because it violates the authoring and judge trust boundary.
- Keep trimmed string equality: rejected because it conflicts with the token
  comparison contract and cannot support structural JSON or tolerance.
