# Practice problem catalog

This local-development catalog contains ten complete C++17 Function problems.
Every problem overrides the template generator and validator, declares two
public samples, generates exactly 500 additional deterministic cases across
edge, random, and adversarial groups, and includes a known-wrong solution that
the quality gate must kill. Each completed revision therefore contains 502
test cases: 2 handwritten samples, 20 edge cases, 450 random cases, and 30
adversarial cases.

The catalog deliberately excludes the existing `two-sum` fixture and all
`ui-*` demonstration slugs.

Validate locally:

```powershell
./scripts/process-problem-catalog.ps1 `
    -CatalogPath ./content/practice-catalog/catalog.json `
    -Validate
```

Import into the running Admin API and ContentWorker:

```powershell
./scripts/process-problem-catalog.ps1 `
    -CatalogPath ./content/practice-catalog/catalog.json `
    -Import
```

`catalog.json` uses `create` for a clean database. If the earlier 30-case
revisions have already been imported, first publish those nine Ready revisions,
then import the upgrade catalog so immutable Published content receives a new
502-case revision and `single-number` is created:

```powershell
./scripts/process-problem-catalog.ps1 `
    -CatalogPath ./content/practice-catalog/catalog-upgrade-from-30.json `
    -Import
```

Do not use the upgrade catalog while those nine revisions are still Ready:
`new-revision` deliberately accepts only a Published predecessor. For a local
environment where the 30-case revisions must never be published, reset the
local development database and import `catalog.json` instead.

Review all ten Ready revisions in `/admin/content-batches` before explicitly
selecting them for publication. These fixtures are suitable for local product
testing, but generated hidden cases in a production deployment must remain in
a private content repository.
