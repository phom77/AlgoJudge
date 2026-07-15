# AlgoJudge ContentTool

This internal command-line application validates and imports versioned ZIP
problem packages. It is intentionally separate from the public API.

Implemented commands:

- `validate <package-path>`
- `import <package-path>`
- `import <package-path> --replace`

Import creates Draft content and never publishes implicitly. Publish and
unpublish commands remain outside PR3. See `docs/problem-package-format.md` and
ADR-0006 for the package contract and safety rules.

From the repository root, the PowerShell wrapper imports by default:

```powershell
./scripts/import-content.ps1 -PackagePath ./content/two-sum.zip
./scripts/import-content.ps1 -PackagePath ./content/two-sum.zip -ValidateOnly
./scripts/import-content.ps1 -PackagePath ./content/two-sum.zip -Replace
```

Database import reads `ConnectionStrings__DefaultConnection` from the process
environment. The wrapper loads it from the repository `.env` file. Validation
does not require a database or `.env` file.
