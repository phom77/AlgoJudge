# ADR-0018: Scale the default problem-suite capacity to one thousand cases

Status: Accepted
Date: 2026-07-25

## Context

The original authoring preset generated 84 cases and both the ContentWorker and
ContentTool defaulted to 500 private cases. That budget is too small for the
intended LeetCode-like practice workflow, where a problem commonly needs broad
random, adversarial, and stress coverage in addition to handwritten regressions.

The source generator already enforces a bounded request and the content worker
accepts a configurable maximum up to 5,000. Package imports must be able to
materialize the same suite size, including one input and one output file per
case, without weakening archive-size or sandbox constraints.

## Decision

Set the default source-authoring and package private-suite capacity to 1,000
cases. Set the default package entry limit to 2,100, which accommodates 1,000
testcase pairs together with root metadata, samples, and optional Function
files. Retain the existing compressed, uncompressed, individual-entry, process,
memory, output, and sandbox limits.

The built-in Two Sum authoring profile produces 999 generated cases and one
handwritten case. Its quality policy requires 1,000 total cases, with minima of
1 handwritten, 100 edge, 700 random, 149 adversarial, and 50 stress; every
declared wrong solution must be killed.

## Consequences

- New maintainer drafts begin with a substantially stronger editable profile.
- Existing definitions and deployed installations can retain smaller suites by
  using their existing policy or overriding the configured capacity.
- Generation and judging perform more sandbox executions; operators must size
  content-worker and judge capacity accordingly, without relaxing per-case
  resource limits.
- Private package archives remain validated before persistence and never expose
  testcase data through public APIs or normal logs.

## Alternatives considered

- Keep the 500-case platform default and only document a larger policy:
  rejected because a 1,000-case source preset could not run or import under the
  default configuration.
- Raise the default to the 5,000-case hard ceiling: rejected because it would
  create an excessive baseline cost for local and MVP deployments.
- Make a larger suite a public scoring feature: rejected because the MVP still
  accepts only when every hidden testcase passes and has no numeric score.
