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

## Entry rules

- The three root files are required and their names are case-sensitive.
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
| File entries | 1,100 |
| Public samples | 20 |
| Private judge cases | 500 |
| Time limit | 100-10,000 ms |
| Memory limit | 16 MiB-1 GiB |

Deployments can override these values in ContentTool configuration. Import
always validates the complete package before writing any database row.

## Creating an archive

From a directory containing the schema-version-1 layout, create the ZIP so the
three required files remain at its root:

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

## Offline authoring extensions

A private authoring directory may also contain `generator/` and `reference/`
content. These directories are not schema-version-1 package entries and are
excluded by `scripts/build-problem-package.ps1`:

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
