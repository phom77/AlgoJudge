# ADR-0010: Generate system tests offline

Status: Accepted
Date: 2026-07-18

## Context

Large system suites are expensive and error-prone to author as individual
input/output pairs. Test generation must remain reproducible and auditable,
must not expose hidden data, and must not add work or variability to submission
grading. ADR-0006 already defines the imported artifact as a strict ZIP whose
schema-version-1 tests are complete `.in`/`.out` pairs.

## Decision

ContentTool supports an authoring-only generation pipeline. A strict manifest
declares trusted .NET implementations of the Application-owned
`ITestCaseGenerator` and
`IInputValidator`, deterministic seed groups, and one C++17 reference solution.
ContentTool derives every case seed using the versioned SHA-256 derivation
algorithm, invokes each generator case twice to detect immediate
non-determinism, validates every input, and then compiles the reference solution
once and runs it for all inputs in the pinned Docker judge sandbox.

Generation writes `tests/*.in` and `tests/*.out` only after the entire pipeline
succeeds. It records SHA-256 hashes for the generator manifest and assemblies,
validator assembly, reference solution, every input/output pair, and the whole
suite. Validation regenerates and compares the exact content and hashes.

Generator and validator assemblies are trusted maintainer code and execute in
the ContentTool process. Reference C++ executes in the hardened sandbox.
ContentTool refuses to overwrite a non-empty test directory without its own
generated-suite manifest. The configured package limits, including the default
maximum of 500 private cases, apply during generation.

Generator files, reference source, and generation manifests are authoring
inputs, not package entries. The package build script includes only the
versioned root metadata, sample/test files, and schema-version-2 function files.
Worker and API processes do
not load generators or run reference solutions; submission grading consumes
only published test pairs.

## Consequences

- The same inputs, tools, and seeds produce an auditable suite with stable
  hashes.
- A generation or reference failure cannot leave a partially updated suite.
- Private content repositories must build generator assemblies before running
  ContentTool and retain the exact binaries used for a suite.
- Maintainers must review generator assemblies as trusted code.
- Schema-version-1 import and the public API remain unchanged.

## Alternatives considered

- Generate during Submit: rejected because it makes grading slower,
  non-reproducible, and harder to audit.
- Execute generator assemblies in the judge sandbox: deferred because the
  current pinned image is intentionally C++17-only; generator code is trusted
  maintainer tooling, not submitted code.
- Store only seeds and regenerate after import: rejected because published
  suites must remain immutable and available without authoring dependencies.
- Commit production generated tests publicly: rejected because system tests are
  private security-sensitive content.
