# ADR-0013: Pin submissions to system-suite versions

Status: Accepted
Date: 2026-07-18

## Context

Official submissions must execute the complete published hidden suite and be
auditable after content evolves. Selecting testcases only by problem ID makes
the suite implicit and could allow a queued or retried submission to observe a
different set of cases. Custom runs must remain separate and must never select
hidden data.

## Decision

Every hidden testcase is tagged with a positive system-suite version. Creating
a submission copies the published problem's `JudgeVersion` into the immutable
submission as `SystemTestSuiteVersion`. The worker requests exactly that
problem/version pair through the Application-owned `ITestSuiteProvider`, runs
the cases in stable ordinal order, and finalizes Accepted only after every case
passes.

The PostgreSQL provider is the only production implementation. It returns no
suite when the exact version is unavailable; the grader records a safe
operational Runtime Error without logging or returning hidden content. Existing
claim tokens, renewable leases, bounded retries, and conditional finalization
remain unchanged. Work directories include the claim token so a reclaimed
attempt cannot share files with a stale worker.

Generator code and reference solutions remain offline ContentTool concerns
under ADR-0010. The worker consumes only imported input/output pairs and never
generates expected output during Submit.

## Consequences

- Submission detail exposes the non-sensitive suite version used for auditing,
  but never testcase IDs, inputs, expected outputs, or contestant output.
- Queue retry and lease recovery continue using the same pinned suite version.
- Content import tags every private case with the problem's current judge
  version. Future content lifecycle work may retain multiple published suite
  versions without changing the grader contract.
- Existing rows are backfilled from their problem's current judge version.

## Alternatives considered

- Select the problem's current tests at grading time: rejected because queued
  work would not be reproducible after content changes.
- Copy hidden cases into each submission: rejected because it duplicates
  sensitive data and expands the privacy surface.
- Generate system tests in the worker: rejected by ADR-0010 because it is slow,
  non-deterministic operationally, and requires authoring dependencies.
