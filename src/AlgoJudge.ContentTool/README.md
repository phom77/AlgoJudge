# AlgoJudge ContentTool

This internal command-line application validates and imports versioned ZIP
problem packages. It is intentionally separate from the public API.

Implemented commands:

- `generate <problem-directory>`
- `validate-generated <problem-directory>`
- `validate <package-path>`
- `import <package-path>`
- `import <package-path> --replace`
- `publish <slug>`
- `unpublish <slug>`

Import creates Draft content and never publishes implicitly. Publishing is a
separate explicit operation that revalidates the problem's required public and
private judge content. See `docs/problem-package-format.md` and ADR-0006 for the
package contract and safety rules.

Package schema version 1 remains compatible and imports as `StdinStdout`.
Schema version 2 explicitly supports `StdinStdout` and `Function`; Function
packages add a validated signature and private C++17 adapter template. See
`docs/problem-package-format.md` and ADR-0011 for the complete contract.

From the repository root, the PowerShell wrapper imports by default:

```powershell
./scripts/import-content.ps1 -PackagePath ./content/two-sum.zip
./scripts/import-content.ps1 -PackagePath ./content/two-sum.zip -ValidateOnly
./scripts/import-content.ps1 -PackagePath ./content/two-sum.zip -Replace
```

Bootstrap the checked-in development-only Two Sum fixture with:

```powershell
./scripts/seed-dev-content.ps1
```

Database import reads `ConnectionStrings__DefaultConnection` from the process
environment. The wrapper loads it from the repository `.env` file. Validation
does not require a database or `.env` file.

## Offline generated tests

`generate` loads the trusted .NET generator and input validator declared in
`generator/manifest.json`, derives deterministic per-case seeds, validates every
input, and uses the pinned C++17 Docker sandbox to compile and run the reference
solution. It writes complete pairs to `tests/` only after every case succeeds.
It refuses to overwrite a non-empty manually-authored `tests/` directory.

`validate-generated` repeats generation and reference execution and verifies
the exact file contents plus the hashes in `generator/generated-tests.json`.
Neither command accesses PostgreSQL. Generator code runs in the ContentTool
process and must therefore be trusted maintainer code; reference C++ runs in the
same hardened sandbox used by the judge.

Generator assemblies reference `AlgoJudge.Application` and implement the public
`ITestCaseGenerator` and `IInputValidator` contracts. A manifest has this shape:

```json
{
  "schemaVersion": 1,
  "generator": {
    "type": "dotnet",
    "assembly": "generator/ProblemGenerator.dll",
    "entry": "Problems.TwoSumGenerator"
  },
  "inputValidator": {
    "type": "dotnet",
    "assembly": "generator/ProblemGenerator.dll",
    "entry": "Problems.TwoSumInputValidator"
  },
  "groups": [
    { "name": "edge", "seed": 101, "count": 10 },
    { "name": "random", "seed": 202, "count": 100 },
    { "name": "stress", "seed": 303, "count": 10 }
  ],
  "referenceSolution": {
    "type": "cpp17",
    "path": "reference/solution.cpp"
  }
}
```

The total group count cannot exceed the configured private-case limit (500 by
default). `scripts/build-problem-package.ps1` includes only schema-v1 package
members, so generator binaries, source, manifests, and the reference solution
do not enter the import ZIP.
