# AlgoJudge ContentTool

This internal command-line application validates and imports versioned ZIP
problem packages. It is intentionally separate from the public API.

Implemented commands:

- `validate <package-path>`
- `import <package-path>`
- `import <package-path> --replace`
- `publish <slug>`
- `unpublish <slug>`

Import creates Draft content and never publishes implicitly. Publishing is a
separate explicit operation that revalidates the problem's required public and
private judge content. See `docs/problem-package-format.md` and ADR-0006 for the
package contract and safety rules.

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
