# Admin content-batch UI demo workspace

This local-only workspace exercises the real Admin API and ContentWorker. Its
test data is intentionally public and must not be reused as production hidden
content.

`catalog.json` creates seven items. After ContentWorker finishes, the expected
result is four Ready items, two Failed items, and one Skipped item. The failures
are intentional: one reference solution does not compile and one suite misses
its quality policy.

The follow-up catalogs support these scenarios:

- `catalog-fix-failures.json`: updates both failed Draft revisions with valid
  content; both should become Ready.
- `catalog-unchanged.json`: requests new revisions for the four original
  successful problems without changing effective content; after those problems
  are Published, every item should be Skipped by content hash.
- `catalog-new-revision.json`: creates a changed revision of
  `ui-template-only`; the existing revision must be Published first.

Validate any catalog before import:

```powershell
./scripts/process-problem-catalog.ps1 `
    -CatalogPath ./content/ui-batch-demo/catalog.json `
    -Validate
```

Import requires `ALGOJUDGE_ADMIN_ACCESS_TOKEN` and creates and starts the batch:

```powershell
$apiBaseUrl = "http://localhost:5016"
$apiOrigin = [Uri]$apiBaseUrl
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

Invoke-WebRequest `
    -Uri "$apiBaseUrl/api/auth/csrf" `
    -WebSession $session | Out-Null

$csrf = ($session.Cookies.GetCookies($apiOrigin) |
    Where-Object Name -eq "XSRF-TOKEN").Value

$loginBody = @{
    userName = "YOUR_ADMIN_USERNAME"
    password = "YOUR_ADMIN_PASSWORD"
} | ConvertTo-Json

$login = Invoke-RestMethod `
    -Uri "$apiBaseUrl/api/auth/login" `
    -Method Post `
    -WebSession $session `
    -Headers @{ "X-XSRF-TOKEN" = $csrf } `
    -ContentType "application/json" `
    -Body $loginBody

if (-not $login.isAdmin) {
    throw "The account is not an Admin."
}

$env:ALGOJUDGE_ADMIN_ACCESS_TOKEN = ($session.Cookies.GetCookies($apiOrigin) |
    Where-Object Name -eq "algojudge-access").Value

./scripts/process-problem-catalog.ps1 `
    -CatalogPath ./content/ui-batch-demo/catalog.json `
    -Import
```

Open `http://localhost:4200/admin/content-batches`, select the newest batch, and
wait until it reaches Ready for review. Publish only explicitly selected Ready
items. Failed items can be retried safely, but the two intentional failures will
remain Failed until their definitions are replaced through
`catalog-fix-failures.json`.

## UI scenario order

Use a clean local database, or verify that no problem with an `ui-` slug exists.
Each import starts its batch automatically.

1. Import `catalog.json`. On the detail page expect `Ready = 4`, `Failed = 2`,
   and `Skipped = 1`. The failed categories are `reference_compile_error` and
   `quality_gate_failed`.
2. Search for `wrong`, then filter by `Ready`. Use an item Retry button or
   `Retry all failed`; both intentional failures are immutable snapshots and
   should fail safely again without creating duplicate revisions.
3. Select only two Ready rows and publish them. Confirm that exactly two become
   Published and the other two remain Ready. Select and publish the remaining
   two; the batch should become Completed.
4. Import `catalog-fix-failures.json`. It applies `update-draft` to the two
   failed revisions. Expect two Ready items, then publish both.
5. Import `catalog-unchanged.json`. Expect all four items to be Skipped because
   their effective content hashes match their latest Published revisions. A
   skipped-only batch currently remains Ready for review because it has nothing
   to publish.
6. Import `catalog-new-revision.json`. Expect one Ready item for revision 2 of
   `ui-template-only`; publish it and verify the public catalogue shows the new
   title and sample.

To test worker-unavailable guidance, stop ContentWorker before an import, open
the active batch detail page, and leave it unchanged for about 30 seconds (12
polls at 2.5 seconds). Restart ContentWorker and use Resume if the batch still
has Pending items. Do not stop PostgreSQL or the API.

To test authorization, sign in with a regular User and open
`http://localhost:4200/admin/content-batches`. The route must redirect to
`/forbidden`; a signed-out browser must redirect to `/login`.

Clear the bearer token after the imports:

```powershell
Remove-Item Env:ALGOJUDGE_ADMIN_ACCESS_TOKEN
```
