param(
    [string]$Image = "algojudge/judge-cpp17:14.3.0-v1",
    [switch]$SkipImageBuild
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repositoryRoot "infra/compose/compose.test.yml"
$testProject = Join-Path $repositoryRoot "tests/AlgoJudge.Backend.EndToEndTests/AlgoJudge.Backend.EndToEndTests.csproj"
$previousPostgreSqlConnection = $env:TEST_POSTGRES_CONNECTION
$previousDockerImage = $env:TEST_DOCKER_JUDGE_IMAGE
$postgresWasRunning = $false
$postgresStartedByScript = $false

Push-Location $repositoryRoot
try {
    docker info *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Engine is not available. Start Docker before running backend E2E acceptance."
    }

    $runningContainer = docker compose -f $composeFile ps -q postgres-test
    $postgresWasRunning = -not [string]::IsNullOrWhiteSpace($runningContainer)
    docker compose -f $composeFile up -d --wait postgres-test
    if ($LASTEXITCODE -ne 0) {
        throw "Could not start the PostgreSQL acceptance-test service."
    }
    $postgresStartedByScript = -not $postgresWasRunning

    if (-not $SkipImageBuild) {
        & (Join-Path $PSScriptRoot "build-judge-image.ps1") -Image $Image
        if ($LASTEXITCODE -ne 0) {
            throw "Could not build the pinned C++17 judge image."
        }
    }

    $env:TEST_POSTGRES_CONNECTION =
        "Host=localhost;Port=55432;Database=AlgoJudgeTestDb;Username=algojudge_test;Password=algojudge_test"
    $env:TEST_DOCKER_JUDGE_IMAGE = $Image

    dotnet test $testProject --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Backend E2E acceptance failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:TEST_POSTGRES_CONNECTION = $previousPostgreSqlConnection
    $env:TEST_DOCKER_JUDGE_IMAGE = $previousDockerImage
    if ($postgresStartedByScript) {
        docker compose -f $composeFile down --volumes
    }
    Pop-Location
}
