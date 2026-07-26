# ADR-0019: Batch testcase execution in one judge sandbox

Status: Accepted
Date: 2026-07-26

## Context

The original judge started one Docker runtime container for every hidden
testcase. A 1,000-case suite therefore paid Docker create, start, inspect, and
remove overhead 1,000 times even when each C++ process completed in
milliseconds. This made grading latency dominated by container orchestration
rather than submitted code.

Reusing the submitted solution process would be faster but would allow mutable
global state, leaked file descriptors, heap state, and prior input to affect
later testcases. Mounting a complete hidden suite into the container would also
expand the exposure surface for private testcase data.

## Decision

Compile in a dedicated container as before, then execute one system-suite
submission in one hardened runtime container. The pinned C++17 image contains a
trusted native batch supervisor. It receives length-framed hidden inputs through
container stdin and invokes the existing native runner once per testcase in
stable ordinal order.

The existing runner continues to fork and execute a fresh solution process,
enforce the per-case time and output limits, and measure per-case elapsed time
and peak memory. The batch supervisor emits concatenated length-framed result
records and stops after the first sandbox-level failure. The host applies the
suite's immutable output checker to successful results in ordinal order and
selects the first verdict failure.

Because Wrong Answer is determined by the host after framed results return,
later process-success cases may already have executed before an earlier output
mismatch is known. This affects only wasted work inside the current batch, not
verdict order or correctness. Compile Error and custom-run behaviour are
unchanged.

## Consequences

- Docker runtime startup is amortized across as many as 5,000 bounded
  testcases, substantially reducing large-suite grading latency.
- Every testcase retains a fresh solution process and independent time, memory,
  stdout, and stderr evidence.
- Hidden inputs are streamed through stdin and are not persisted or mounted as
  testcase files.
- The aggregate transport is bounded, validated as a complete framed stream,
  and treated as System Error when malformed or incomplete.
- Runtime image changes must build and test both native supervisors.
- Wrong Answer does not guarantee sandbox execution stopped at that exact case,
  although the reported verdict remains the first mismatch in stable order.

## Alternatives considered

- Keep one Docker container per testcase: rejected because orchestration
  overhead dominates 1,000-case suites.
- Invoke all testcases in one submitted solution process: rejected because
  process state could leak between cases and invalidate isolation semantics.
- Mount the hidden suite into the runtime container: rejected because the
  contestant process would gain a path to private testcase data.
- Start multiple runtime containers in parallel: deferred because it increases
  host contention and does not remove per-container startup overhead.
