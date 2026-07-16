$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$previousUpdateValue = $env:UPDATE_OPENAPI_SNAPSHOT

Push-Location $repositoryRoot
try {
    $env:UPDATE_OPENAPI_SNAPSHOT = "1"
    dotnet test `
        tests/AlgoJudge.Api.IntegrationTests/AlgoJudge.Api.IntegrationTests.csproj `
        --configuration Release `
        --filter "FullyQualifiedName=AlgoJudge.Api.IntegrationTests.ApiContractTests.OpenApiV1MatchesApprovedSnapshot"

    if ($LASTEXITCODE -ne 0) {
        throw "OpenAPI snapshot generation failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:UPDATE_OPENAPI_SNAPSHOT = $previousUpdateValue
    Pop-Location
}

Write-Host "Updated tests/AlgoJudge.Api.IntegrationTests/Snapshots/openapi-v1.json"
