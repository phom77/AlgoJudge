# Problem Package Format

Problem packages are private, versioned ZIP archives consumed by
`AlgoJudge.ContentTool`. Production hidden tests must not be committed to a
public repository.

## Schema version 1

```text
two-sum.zip
|-- problem.json
|-- statement.md
|-- constraints.md
|-- samples
|   |-- 01.in
|   |-- 01.out
|   `-- 01.md        # optional explanation
`-- tests
    |-- 001.in
    |-- 001.out
    |-- 002.in
    `-- 002.out
```

All file content must be valid UTF-8. Inputs and outputs are stored exactly as
provided; ContentTool does not normalize whitespace or line endings.

Schema version 1 remains supported and always imports as the `StdinStdout`
execution mode. It cannot declare `executionMode` or contain `function/` files.

### `problem.json`

```json
{
  "schemaVersion": 1,
  "slug": "two-sum",
  "title": "Two Sum",
  "difficulty": "Easy",
  "timeLimitMs": 1000,
  "memoryLimitKb": 262144,
  "tags": [
    { "slug": "array", "name": "Array" },
    { "slug": "hash-table", "name": "Hash Table" }
  ]
}
```

- Slugs use lowercase ASCII letters, digits, and single hyphen separators.
- Difficulty is `Easy`, `Medium`, or `Hard`.
- Tag slugs must be unique within the package.
- Unknown or duplicate JSON properties are rejected.

## Schema version 2

Schema version 2 adds an explicit execution mode. A stdin/stdout package uses
the version-1 layout and adds these properties to `problem.json`:

```json
{
  "schemaVersion": 2,
  "executionMode": "StdinStdout"
}
```

A function package additionally contains its public signature and private C++
adapter template:

```text
two-sum-function.zip
|-- problem.json
|-- statement.md
|-- constraints.md
|-- samples/
|-- tests/
`-- function/
    |-- signature.json
    `-- adapter-template.cpp
```

`problem.json` declares `"executionMode": "Function"`. The signature format is:

```json
{
  "className": "Solution",
  "methodName": "twoSum",
  "returnType": "Int32Array",
  "parameters": [
    { "name": "nums", "type": "Int32Array" },
    { "name": "target", "type": "Int32" }
  ]
}
```

Class, method, and parameter names must be non-keyword C++ identifiers.
Supported value types are `Int32`, `Int64`, `Double`, `Boolean`, `String`, and
the one-dimensional array form of each type. A signature accepts at most 16
parameters and parameter names are unique.

Function sample and hidden-test inputs are JSON objects keyed by parameter
name. They must contain every declared parameter exactly once and no unknown
arguments. Expected output is one JSON value matching `returnType`. Duplicate
JSON properties, out-of-range integers, and non-finite numbers are rejected.
For example:

```json
{"nums":[2,7,11,15],"target":9}
```

The adapter is trusted private content executed only inside the C++17 sandbox.
It reads the normalized JSON testcase from stdin, invokes the solution, and
writes one JSON result. It must contain each of these placeholders exactly once
and cannot declare other `{{...}}` placeholders:

- `{{USER_SOURCE}}`
- `{{CLASS_NAME}}`
- `{{METHOD_NAME}}`

ContentTool validates and persists the adapter, but it is never returned by a
public API.

## Entry rules

- The three root files are required and their names are case-sensitive.
- Schema version 2 requires `executionMode`.
- `Function` requires both fixed function files; `StdinStdout` forbids them.
- Sample and test names use a positive numeric ordinal of 2-4 digits.
- Every `.in` file has exactly one matching `.out` file.
- A sample may include one matching `.md` explanation.
- Judge tests cannot contain explanation files.
- Unexpected files, absolute paths, `..` segments, backslashes, and duplicate
  names are rejected.
- At least one sample and one private judge case are required.

## Default safety limits

| Limit | Default |
|---|---:|
| ZIP file size | 20 MiB |
| Total uncompressed content | 100 MiB |
| Individual entry | 8 MiB |
| Function signature | 64 KiB |
| Function adapter | 256 KiB |
| File entries | 1,100 |
| Public samples | 20 |
| Private judge cases | 500 |
| Time limit | 100-10,000 ms |
| Memory limit | 16 MiB-1 GiB |

Deployments can override these values in ContentTool configuration. Import
always validates the complete package before writing any database row.

## Creating an archive

From a directory containing a supported versioned layout, create the ZIP so
the three required files and optional schema-version-2 `function/` files remain
at their defined paths:

```powershell
./scripts/build-problem-package.ps1 `
    -SourcePath ./two-sum `
    -PackagePath ./content/two-sum.zip
```

## Commands

```powershell
dotnet run --project src/AlgoJudge.ContentTool -- validate path/to/problem.zip
dotnet run --project src/AlgoJudge.ContentTool -- import path/to/problem.zip
dotnet run --project src/AlgoJudge.ContentTool -- import path/to/problem.zip --replace
dotnet run --project src/AlgoJudge.ContentTool -- publish two-sum
dotnet run --project src/AlgoJudge.ContentTool -- unpublish two-sum
```

`import` creates a Draft. Duplicate slugs fail unless `--replace` is supplied,
and replacement is allowed only while the existing problem is Draft.
`publish` verifies that the Draft has complete statement metadata, at least one
public sample, and at least one private judge case before making it public.
`unpublish` returns a Published problem to Draft without deleting its content.

For local development only, package, import, and publish the checked-in fixture
with `./scripts/seed-dev-content.ps1`. Its judge cases are public development
data and must never be reused as production hidden tests.

## Legacy offline authoring extensions

The current ContentTool accepts a private authoring directory containing
`generator/` and `reference/` content. This is the legacy authoring path. These
directories are not schema-version-1 package entries and are excluded by
`scripts/build-problem-package.ps1`:

```text
problem-authoring/
|-- problem.json
|-- statement.md
|-- constraints.md
|-- samples/
|-- tests/                       # generated .in/.out pairs
|-- generator/
|   |-- manifest.json
|   |-- ProblemGenerator.dll
|   `-- generated-tests.json     # source and suite hashes
`-- reference/
    `-- solution.cpp
```

Run generation before creating the ZIP:

```powershell
dotnet run --project src/AlgoJudge.ContentTool -- generate path/to/problem-authoring
dotnet run --project src/AlgoJudge.ContentTool -- validate-generated path/to/problem-authoring
./scripts/build-problem-package.ps1 `
    -SourcePath path/to/problem-authoring `
    -PackagePath path/to/problem.zip
```

The manifest format and generator contracts are documented in the ContentTool
README. Generation is deterministic by declared group seed, validates every
input before execution, runs the reference solution offline in the pinned
C++17 sandbox, and refuses to replace manually-authored tests. Production
hidden tests and authoring inputs remain in a private content repository.

## Source-based authoring transition

The approved replacement is the source-based contract in
[`problem-authoring.md`](problem-authoring.md) and
[ADR-0014](adr/0014-use-source-based-problem-authoring.md). It lets a maintainer
store generator and validator source, a Function signature, a reference
class/method, and optional wrong solutions without supplying a project, DLL,
full stdin/stdout executable, or adapter.

That definition is not a ZIP member and does not create package schema version
3. ContentTool recognizes a root `authoring.json`, generates complete
`.in`/`.out` pairs through the sandboxed source pipeline, and records version-2
generation provenance. The later persistence/API work will version its storage
contract. During migration, tooling may materialize a platform-generated
private adapter into an otherwise valid schema-version-2 package.

Compatibility rules are:

- schema-version-1 and schema-version-2 packages remain valid;
- existing private schema-version-2 adapters remain accepted and executable;
- imported and published suite versions are never rewritten;
- the DLL/manifest workflow remains available as a legacy CLI input until a
  separately approved removal; and
- the future Admin API accepts source-based definitions, not DLL uploads.

For a source-authored directory, build both pinned images and run the same
commands used by the legacy workflow:

```powershell
./scripts/build-content-generator-image.ps1
./scripts/build-judge-image.ps1
dotnet run --project src/AlgoJudge.ContentTool -- generate path/to/problem-authoring
dotnet run --project src/AlgoJudge.ContentTool -- validate-generated path/to/problem-authoring
```

When `authoring.json` is present, it takes precedence over the legacy
`generator/manifest.json` path. Generator and validator C# source run only in
the .NET content-generation sandbox. Reference and wrong C++17 methods run only
through the generic harness in the judge sandbox. The complete pipeline is
repeated for deterministic validation before package creation.
