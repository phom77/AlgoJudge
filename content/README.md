# Problem Content

This directory is the local-development entry point for versioned ZIP problem
packages. See `docs/problem-package-format.md` for schema version 1.

Do not commit production hidden tests to a public repository. Production
content should live in a private repository or private object storage and be
imported through `AlgoJudge.ContentTool`.

Private Function-problem authoring directories may use root `authoring.json`
with the source-based generator SDK. Build the two sandbox images and run
ContentTool `generate` before packaging; see `docs/problem-authoring.md`.

The `dev` directory contains explicitly non-production fixtures whose judge
cases are intentionally visible. Run `./scripts/seed-dev-content.ps1` to
package, validate, import, and publish the local Two Sum fixture.
