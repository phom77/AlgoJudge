# ADR-0017: Enforce problem suite quality gates from immutable snapshots

Status: Accepted
Date: 2026-07-25

## Context

Source-authored problem suites previously exposed group counts and surviving
wrong solutions only as review metadata. A maintainer could publish a small
candidate or one that failed to distinguish a declared incorrect solution.
This did not provide a reliable path from the current small presets to the
larger, deliberately distributed hidden suites expected of a practice judge.

Quality requirements must not become a mutable setting on `Problem`, because a
published system suite and submissions pinned to it need reproducible
provenance. They also must not reveal hidden testcase contents in an authoring
error or normal logs.

## Decision

Store a `SuiteQualityPolicy` inside each `ProblemAuthoringDefinition`. The
policy has a bounded minimum total case count, bounded minima per supported
semantic group, and a flag requiring every declared wrong solution to be
killed. A missing policy uses the compatibility default of one total and one
handwritten case, with declared wrong solutions required to be killed.

The content worker and offline ContentTool evaluate the policy after reference
outputs and differential coverage are complete. A violation rejects the
candidate with a safe quality-gate failure; it does not retain a Ready suite.
The repository repeats the same evaluation inside the publish transaction as a
defence against stale or malformed persisted candidates. Updating the policy
invalidates a Ready candidate, and the serialized policy contributes to
candidate provenance.

## Consequences

- Maintainers can intentionally scale a suite and require coverage across
  handwritten, edge, random, adversarial, and stress groups.
- A declared wrong solution becomes an enforceable acceptance criterion when
  the policy requires it, rather than a review-only warning.
- Existing stored definitions remain readable through the conservative default;
  no system-suite schema migration is required because policy is authoring
  snapshot data.
- The policy does not alter learner verdict semantics, output checker behavior,
  or public API contracts.

## Alternatives considered

- Enforce one global fixed minimum for every problem: rejected because problem
  complexity and generation strategy vary, and it would make small exercises
  artificially expensive.
- Keep survivor and group data review-only: rejected because it permits a
  maintainer to accidentally publish an inadequate candidate.
- Run gates only in the worker: rejected because a stale or manipulated Ready
  candidate could then bypass the policy at publication time.
