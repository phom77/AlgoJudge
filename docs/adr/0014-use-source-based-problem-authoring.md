# ADR-0014: Use source-based problem authoring in a separate content sandbox

Status: Accepted
Date: 2026-07-21

## Context

ADR-0010 made offline generation deterministic, but requires maintainers to
build trusted .NET class libraries and executes them inside ContentTool.
ADR-0011 supports Function problems, but requires every package to supply a
private C++ adapter template. Those choices proved the generation and judging
pipeline, but impose build-project, DLL, manifest, adapter, and full-program
ceremony on every problem.

Maintainers need to express problem-specific generation logic without being
limited to a fixed strategy registry. At the same time, all supplied source is
potentially hostile and must not execute in the API, grading worker, or content
tool process. Published system suites must remain deterministic, auditable,
private, immutable, and pinned by submissions.

## Decision

Adopt the source-based `ProblemAuthoringDefinition` specified in
[`problem-authoring.md`](../problem-authoring.md). Version 1 contains a
language-neutral Function signature, handwritten arguments, problem-specific
C# generator and validator source using a versioned SDK, a C++17 reference
class/method, and optional C++17 wrong solutions.

Generator helpers are an extensible SDK rather than a closed strategy
registry. The platform owns deterministic seed derivation, grouping, generic
Function harness generation, canonical JSON conversion, repeat execution,
differential checks, provenance hashes, and immutable suite creation.
Maintainers provide no project, DLL, executable entry point, parser,
serializer, or adapter.

All authoring source is compiled and executed in pinned, isolated
content-generation containers. A separate content worker will orchestrate
background jobs; the CLI may orchestrate the same sandboxed engine during the
transition. Neither path may load generator assemblies into its own process.
The API and grading worker never execute authoring source. Submission grading
continues to consume only immutable published input/output pairs.

An authoring revision follows `Draft -> Generating -> Ready -> Published`.
Failures return it to Draft with safe attempt diagnostics. Generation runs from
an immutable snapshot, Ready identifies one verified candidate, and publishing
atomically assigns a positive system-suite version. Later edits create a new
Draft and cannot mutate a published suite.

Schema-version-1 and schema-version-2 packages, existing private adapters, and
published suites remain supported. The DLL/manifest generator path becomes a
legacy transition input. This decision does not add a ZIP schema version,
database tables, Admin API, or runtime implementation.

This ADR supersedes ADR-0010 and ADR-0011. It preserves their execution modes,
value-type whitelist, sandbox requirements, offline generation rule, and
legacy compatibility, while replacing trusted in-process generator DLLs and
maintainer-authored per-problem adapters for the new authoring path.

## Consequences

- Maintainers can write arbitrary problem-specific generator logic through a
  small SDK without waiting for a platform strategy to be added.
- Generator and reference source have the same hostile-code trust posture as
  learner submissions and require separately scalable sandbox capacity.
- Determinism depends on pinned SDK/toolchain identities, platform-owned seeds,
  canonical serialization, and repeat execution, all of which become suite
  provenance.
- Generic harness correctness becomes shared infrastructure for learner and
  reference solutions and requires exhaustive type and diagnostic tests.
- Generation jobs need durable leases, fencing, bounded retries, private
  artifacts, and safe diagnostics before an Admin API can expose them.
- Existing packages continue to work while new authoring can be implemented in
  staged branches without rewriting published suites.

## Alternatives considered

- Keep trusted generator DLLs: rejected because it retains per-problem project
  ceremony and allows authoring code to execute in a privileged tool process.
- Use only a fixed registry of generator strategies: rejected because novel
  problem structures would require platform releases and the registry would
  become an authoring limitation.
- Require complete stdin/stdout reference programs and private adapters:
  rejected because it duplicates platform parsing and serialization logic for
  every Function problem.
- Generate tests during Submit: rejected because it makes verdicts slower and
  non-reproducible and breaks immutable suite pinning.
- Execute authoring code in the grading worker: rejected because content
  generation has different trust, scaling, retry, and artifact lifecycles.

