# ADR-0020: Batch content reference and wrong-solution execution

Status: Accepted
Date: 2026-07-26

## Context

Source-based generation produces as many as 5,000 candidate cases. The content
worker previously started a Docker runtime container for each reference case,
then repeated the same compile-and-run sequence for determinism. Every declared
wrong solution added another container per case. For a 1,000-case suite with two
wrong solutions, container orchestration could therefore dominate more than
4,000 short C++ executions.

ADR-0019 introduced a hardened runtime batch supervisor for learner
submissions. Content generation needs the same amortized container startup, but
wrong-solution analysis has a different failure policy: a Runtime Error, Time
Limit Exceeded, Memory Limit Exceeded, or output-limit failure kills that case
without making coverage for later cases irrelevant.

## Decision

Reuse the pinned C++17 batch supervisor for source-authored reference and
wrong-solution execution.

Compile the reference harness once. Execute the complete ordered input suite in
one stopping batch, then execute a second batch from the same compiled artifact
for byte-level determinism comparison. Compile each declared wrong solution
once and execute the complete suite in one continuing batch.

The continuing mode emits a framed result for every testcase even after a
per-case sandbox failure. The content worker treats every non-success or output
mismatch as a killed case and retains exact ordinal coverage. An incomplete,
malformed, or infrastructure-failed batch rejects the generation job rather
than inventing coverage.

Both modes stream length-framed inputs through container stdin. Every testcase
still runs as a fresh solution process under the native runner with independent
time, memory, stdout, and stderr evidence. Generator and validator execution
remains in the separately pinned .NET sandbox.

## Consequences

- A 1,000-case definition uses two reference runtime containers plus one
  runtime container per wrong solution instead of one container per execution.
- Reference determinism no longer recompiles identical source between its two
  executions.
- Wrong-solution coverage remains complete when individual cases fail.
- Compile failures, reference execution failures, malformed protocols, and
  incomplete continuing batches remain safe generation failures.
- The C++17 image version changes because the trusted batch supervisor gains a
  continuing execution mode.
- Suite inputs, outputs, source, and diagnostics remain private and excluded
  from normal logs and public contracts.

## Alternatives considered

- Keep one container per testcase: rejected because Docker orchestration
  dominates large-suite generation latency.
- Stop wrong-solution batches after their first failure: rejected because it
  would silently truncate per-case differential coverage.
- Reuse one submitted C++ process for all cases: rejected because mutable state
  could leak across cases and invalidate reference determinism or coverage.
- Run reference and wrong solutions in the .NET generator container: rejected
  because it would merge trust boundaries and toolchain responsibilities.
