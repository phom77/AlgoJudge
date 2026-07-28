# ADR-0022: Use catalog-based content workspaces

Status: Accepted
Date: 2026-07-28

## Context

ADR-0014 established source-based problem authoring, but its transitional
`authoring.json` contract repeats complete generator, validator, signature, and
source definitions for every problem. Maintaining tens or hundreds of similar
Function problems this way makes safe bulk validation and review difficult.

The generation pipeline needs a deterministic input snapshot before any
database import or background job is created. That snapshot must not keep a
published revision dependent on mutable template files.

## Decision

Adopt a versioned content workspace rooted at one `catalog.json`:

```text
content/
|-- catalog.json
|-- templates/
|   `-- <template-name>/
|       |-- template.json
|       |-- generator.cs
|       `-- validator.cs
`-- problems/
    `-- <problem-name>/
        |-- problem.json
        |-- reference.cpp
        |-- generator.cs            # optional override
        |-- validator.cs            # optional override
        `-- wrong-solutions/        # optional
            `-- <name>.cpp
```

Version 1 resolves values in this order:

```text
problem override -> selected template -> platform default
```

Problem identity and content-specific metadata are never inherited: slug,
title, statement, constraints, signature, samples, and `reference.cpp` remain
problem-owned. Generator and validator source come from the selected template
unless the problem supplies the convention-based override file. Wrong
solutions are discovered from top-level `wrong-solutions/*.cpp` files and use
their file stem as the stable identifier.

The resolver materializes one canonical
`ProblemAuthoringDefinition`, resolved metadata, generator parameters, and
source-origin summary per enabled catalog entry. It computes a SHA-256 content
hash over canonical JSON containing only the effective metadata, parameters,
and materialized definition. Catalog paths, actions, source origins, and
formatting do not change the hash.

All three JSON documents use schema version 1, reject unknown and duplicate
properties, and are specified under `docs/schemas/content-workspace`. Template
generator parameters use the documented JSON Schema subset. Template
inheritance is not supported.

Every workspace path must be a forward-slash relative path. Absolute paths,
backslashes, empty/dot/parent segments, lexical escapes, and symbolic-link
escapes are rejected before source is read.

ContentTool exposes read-only commands:

```powershell
dotnet run --project src/AlgoJudge.ContentTool -- workspace validate content/catalog.json
dotnet run --project src/AlgoJudge.ContentTool -- workspace resolve content/catalog.json
```

`validate` reports only success or validation diagnostics. `resolve` writes the
complete resolved internal definition as JSON for maintainer review. Neither
command writes PostgreSQL, creates a generation job, compiles source, executes
source, or publishes content.

## Consequences

- Ordinary problems need only `problem.json` and `reference.cpp`.
- Template changes affect future resolution hashes but cannot mutate an
  imported revision after its sources have been materialized.
- A whole enabled catalog can be validated before persistence.
- Generator parameters, defaults, and bounds are explicit and deterministic.
- The resolver remains an internal ContentTool concern; it does not add a
  public authoring role or API.
- Batch persistence, retries, generation jobs, audit, and publishing are
  separate decisions and are outside this ADR.

## Alternatives considered

- Keep one complete `authoring.json` per problem: rejected because it duplicates
  common source and policy and is cumbersome at catalog scale.
- Resolve templates during judging: rejected because published suites must be
  immutable and independent of authoring files.
- Allow templates to inherit other templates: rejected because cycles and
  multi-level precedence make review and hashing harder.
- Put generator strategy selection in platform defaults: rejected because the
  platform cannot infer problem-specific algorithms or valid data.
