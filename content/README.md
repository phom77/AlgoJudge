# Problem Content

This directory is the local-development entry point for catalog-based
authoring workspaces and versioned ZIP problem packages. See
`docs/content-workspace.md` and `docs/problem-package-format.md`.

Do not commit production hidden tests to a public repository. Production
content should live in a private repository or private object storage and be
imported through `AlgoJudge.ContentTool`.

Private Function-problem authoring directories may use root `authoring.json`
with the source-based generator SDK. Build the two sandbox images and run
ContentTool `generate` before packaging; see `docs/problem-authoring.md`.

A workspace may instead use root `catalog.json` with reusable `templates/` and
lightweight `problems/`. Validate or inspect its effective definitions without
database access:

```powershell
dotnet run --project src/AlgoJudge.ContentTool -- workspace validate content/catalog.json
dotnet run --project src/AlgoJudge.ContentTool -- workspace resolve content/catalog.json
```

The `dev` directory contains explicitly non-production fixtures whose judge
cases are intentionally visible. Run `./scripts/seed-dev-content.ps1` to
package, validate, import, and publish the local Two Sum fixture.
