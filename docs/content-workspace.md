# Content Workspace Contract

This document defines schema version 1 of the internal catalog-based authoring
workspace approved by [ADR-0022](adr/0022-use-catalog-based-content-workspaces.md).
It sits before generation and import. Resolving a workspace does not access
PostgreSQL, compile or execute source, create a job, or publish content.

The machine-readable contracts are:

- [`catalog.schema.json`](schemas/content-workspace/catalog.schema.json)
- [`problem.schema.json`](schemas/content-workspace/problem.schema.json)
- [`template.schema.json`](schemas/content-workspace/template.schema.json)

Unknown and duplicate JSON properties are rejected at every level.

## Layout

```text
content/
|-- catalog.json
|-- templates/
|   `-- int-array-function/
|       |-- template.json
|       |-- generator.cs
|       `-- validator.cs
`-- problems/
    |-- maximum-subarray/
    |   |-- problem.json
    |   `-- reference.cpp
    `-- special-problem/
        |-- problem.json
        |-- reference.cpp
        |-- generator.cs
        |-- validator.cs
        `-- wrong-solutions/
            `-- returns-zero.cpp
```

All JSON and source files must be valid UTF-8 and fit the configured
ContentTool entry limit. File and directory names are case-sensitive contract
names even when the host filesystem is not.

## `catalog.json`

```json
{
  "schemaVersion": 1,
  "problems": [
    {
      "path": "problems/maximum-subarray",
      "action": "create",
      "enabled": true
    },
    {
      "path": "problems/two-sum",
      "action": "new-revision",
      "enabled": true
    }
  ]
}
```

`path` is relative to the directory containing `catalog.json` and must use
forward slashes. Absolute paths, backslashes, empty segments, `.`, `..`, paths
outside the workspace, and symbolic-link escapes are rejected.

Supported actions are:

| Action | Intended import behavior |
|---|---|
| `create` | Create only when the slug does not exist. |
| `update-draft` | Replace only an editable Draft revision. |
| `new-revision` | Create a new revision for an existing problem. |
| `skip` | Do not import the item. |

Branch-1 resolution validates and resolves every enabled entry, including a
`skip` entry, but does not perform the action. Disabled entries remain listed
in the catalog and are not resolved. Enabled problem paths and slugs must be
unique.

## `template.json`

A template owns technical defaults, generator parameter validation, and the
default generator and validator source:

```json
{
  "schemaVersion": 1,
  "executionMode": "Function",
  "language": "cpp17",
  "generatorSdkVersion": 1,
  "timeLimitMs": 1000,
  "memoryLimitKb": 262144,
  "qualityPolicy": {
    "minimumTestCaseCount": 500,
    "minimumCasesByGroup": [
      { "group": "handwritten", "minimumCaseCount": 1 },
      { "group": "random", "minimumCaseCount": 350 }
    ],
    "requireEachDeclaredWrongSolutionKilled": true
  },
  "generatorParametersSchema": {
    "type": "object",
    "properties": {
      "minimumLength": {
        "type": "integer",
        "minimum": 1,
        "maximum": 100000
      },
      "caseCount": {
        "type": "integer",
        "minimum": 1,
        "maximum": 1000,
        "default": 500
      }
    },
    "required": ["minimumLength"],
    "additionalProperties": false
  }
}
```

`executionMode`, `language`, `generatorSdkVersion`, resource limits, and
`qualityPolicy` are optional. Their version-1 platform defaults are:

| Setting | Platform default |
|---|---|
| Execution mode | `Function` |
| Learner/reference language | `cpp17` |
| Generator SDK | `1` |
| Time limit | `1000` ms |
| Memory limit | `262144` KiB |
| Output checker | `JsonExact` |
| Quality policy | One total and one `handwritten` case; kill every declared wrong solution |

If supplied, execution mode, language, and SDK must still be `Function`,
`cpp17`, and `1`. Version 1 does not support template inheritance.

The generator parameter schema supports an object with
`additionalProperties: false`. A property rule supports `integer`, `number`,
`string`, `boolean`, or `array`, plus the applicable `minimum`, `maximum`,
`minLength`, `maxLength`, `minItems`, `maxItems`, `items`, `enum`, and `default`
keywords. Required names must refer to declared properties. Defaults are
materialized before hashing.

The sibling `generator.cs` and `validator.cs` files are required. Template
source is ordinary source for the pinned generator SDK; the resolver reads it
but never compiles or executes it.

## `problem.json`

An ordinary problem needs only `problem.json` and `reference.cpp`:

```json
{
  "schemaVersion": 1,
  "template": "int-array-function",
  "slug": "maximum-subarray",
  "title": "Maximum Subarray",
  "difficulty": "Medium",
  "tags": ["array", "dynamic-programming"],
  "statement": "Given an integer array nums...",
  "constraints": [
    "1 <= nums.length <= 100000",
    "-10000 <= nums[i] <= 10000"
  ],
  "timeLimitMs": 1000,
  "memoryLimitKb": 262144,
  "signature": {
    "className": "Solution",
    "methodName": "maxSubArray",
    "returnType": "Int32",
    "parameters": [
      { "name": "nums", "type": "Int32Array" }
    ]
  },
  "samples": [
    {
      "arguments": {
        "nums": [-2, 1, -3, 4, -1, 2, 1, -5, 4]
      },
      "expected": 6,
      "explanation": "The subarray [4,-1,2,1] has the largest sum."
    }
  ],
  "generatorParameters": {
    "minimumLength": 1,
    "caseCount": 500
  }
}
```

The following problem-owned values are required and cannot come from a
template: slug, title, difficulty, tags, statement, constraints, signature,
samples, generator parameters, and `reference.cpp`. Sample argument keys and
values must exactly match the signature; `expected` must match its return type.

`timeLimitMs`, `memoryLimitKb`, and `qualityPolicy` are optional problem
overrides. Resolution order is:

```text
problem override -> selected template -> platform default
```

A problem-level `generator.cs` or `validator.cs` replaces the matching template
source. Top-level `wrong-solutions/*.cpp` files are discovered automatically,
sorted by file name, and materialized into the definition. Each file stem must
be a unique lowercase kebab-case identifier.

## Resolution and content hash

For every enabled catalog entry ContentTool produces:

- effective metadata and public samples;
- generator parameters with template defaults materialized;
- one canonical `ProblemAuthoringDefinition` containing signature,
  handwritten sample arguments, effective generator and validator source,
  reference source, wrong solutions, and quality policy;
- source-origin paths for review; and
- a lowercase SHA-256 `contentHash`.

The hash is calculated from canonical JSON of the effective metadata,
generator parameters, and materialized definition. Object property order,
JSON whitespace, catalog action/path, and source-origin paths are excluded.
Meaningful metadata, parameter, policy, or source changes alter the hash.

## Commands

Validate without displaying private source:

```powershell
dotnet run --project src/AlgoJudge.ContentTool -- `
    workspace validate content/catalog.json
```

Resolve and print the complete internal definition as JSON:

```powershell
dotnet run --project src/AlgoJudge.ContentTool -- `
    workspace resolve content/catalog.json
```

The resolve output contains private authoring source and is intended only for a
trusted maintainer terminal. Neither command generates hidden testcases.

## Batch import

After review, a maintainer can create and start a durable batch through the
internal Admin API:

```powershell
$env:ALGOJUDGE_ADMIN_ACCESS_TOKEN = "<short-lived Admin bearer token>"

dotnet run --project src/AlgoJudge.ContentTool -- `
    workspace import content/catalog.json `
    --api-base-url http://localhost:5016
```

The token is read only from the environment and is never passed as a command
argument or printed. Import first performs the same complete local resolution,
then sends the materialized definitions to the API. The API snapshots and
enqueues them; only ContentWorker compiles or executes source.

The repeatable wrapper is:

```powershell
./scripts/process-problem-catalog.ps1 `
    -CatalogPath ./content/catalog.json `
    -Import
```

Use `-Validate` or `-Resolve` instead of `-Import` for the read-only modes.
Resolution output contains private source and must be handled as confidential.
See [ADR-0023](adr/0023-use-audited-content-batch-operations.md) for lifecycle,
retry, audit, and publication decisions.
