# ADR-0011: Support versioned problem execution modes

Status: Accepted
Date: 2026-07-18

## Context

AlgoJudge currently requires every C++17 submission to be a complete program
that reads stdin and writes stdout. LeetCode-style problems instead ask the
learner to implement a declared class method while the platform owns argument
parsing, invocation, and result serialization. This must not weaken sandboxing,
expose hidden data, or invalidate existing schema-version-1 packages.

## Decision

Problems have one persisted execution mode: `StdinStdout` or `Function`.
Existing rows and schema-version-1 packages default to `StdinStdout`. Package
schema version 2 requires an explicit execution mode.

Function packages contain `function/signature.json` and the private
`function/adapter-template.cpp`. Signatures use a bounded, language-neutral
value-type whitelist: signed 32-bit and 64-bit integers, finite doubles,
booleans, strings, and one-dimensional arrays of those types. C++ class,
method, and parameter names must be safe non-keyword identifiers. Function
test inputs are JSON objects keyed by parameter name and expected outputs are
JSON values matching the return type.

The adapter template contains exactly one placeholder each for user source,
class name, and method name. It owns JSON parsing, method invocation, and output
serialization. ContentTool validates the signature, adapter structure, and all
sample/hidden data before import and again before publication. Only the
signature may become public in a future additive API contract; the adapter and
hidden values remain private.

Application owns the function signature model and harness-builder port. The
C++17 adapter implementation belongs to Infrastructure. Problem persistence
stores the validated signature as PostgreSQL `jsonb` and the adapter as private
text, with check constraints enforcing that both are present only for Function
problems.

The judge compiles either the submitted source unchanged or the combined
function harness. Both modes use the same pinned C++17 image, resource limits,
output bounds, and sandbox isolation. Later system-suite work versions the
published suite and expands verdict regression coverage without changing this
mode-selection contract.

## Consequences

- Existing packages, rows, and stdin/stdout judging remain compatible.
- Function content has a strict, auditable contract before judge integration.
- Adapter authors are responsible for correct JSON conversion for the declared
  types; adapters are trusted private content but still execute in the sandbox.
- Nested containers, nullable values, custom classes, void/mutating return
  conventions, and tolerant numeric comparison require a later schema version
  or ADR.
- Public API and web contracts remain unchanged in this branch.

## Alternatives considered

- Infer the mode from source code or package files: rejected because judging
  and API behaviour must be explicit and versioned.
- Generate a universal C++ JSON adapter in the worker: rejected for this
  iteration because the pinned image has no JSON library and a handwritten
  universal parser would substantially increase the trusted judge surface.
- Allow arbitrary C++ type strings: rejected because they enable ambiguous or
  unsafe contracts that cannot be validated against JSON test data.
- Break schema version 1: rejected because existing private packages and
  imported problems must remain usable.
