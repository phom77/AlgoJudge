# ADR-0008: Use a native runner inside the C++17 judge image

Status: Accepted
Date: 2026-07-16

## Context

Measuring the `docker` CLI process includes container startup and does not
represent solution execution time. Reading host cgroup paths after an
auto-removed container is unreliable across Linux, WSL, and Docker Desktop.
Unbounded redirected output also lets hostile code consume worker memory.

## Decision

Build a small native runner into the pinned C++17 judge image. The runner starts
a solution as a child process, measures elapsed time with `CLOCK_MONOTONIC`,
collects peak resident memory with `wait4/getrusage`, and captures stdout and
stderr through independent bounded pipes. It returns a length-framed protocol
that contestant output cannot impersonate.

The worker manages containers through create, start, inspect, and remove steps.
Docker OOM state is inspected before cleanup so a hard memory kill maps to
Memory Limit Exceeded. Compile and runtime containers use separate mount and
security policies.

## Consequences

- Reported execution time excludes Docker startup.
- Successful and failed executions report measured peak memory when the runner
  survives; Docker OOM remains detectable if the whole container is killed.
- Contestant output is bounded before it reaches worker-managed memory.
- The runner is Linux-specific and must be built and tested as part of the
  judge image.
- Updating GCC or the base image digest is an explicit reviewed change.

## Alternatives considered

- Host stopwatch and host cgroup paths: rejected because timing includes Docker
  startup and cgroup layouts differ across hosts.
- GNU `time` plus a text marker: rejected because contestant output can make
  an unframed marker ambiguous and output still needs a bounded supervisor.
- Docker stats polling: rejected because sampling can miss short memory peaks.
