param(
    [string]$Image = "algojudge/judge-cpp17:14.3.0-v1"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repositoryRoot
try {
    docker build `
        --file infra/docker/judge-cpp17.Dockerfile `
        --tag $Image `
        infra/docker
}
finally {
    Pop-Location
}

Write-Host "Built judge image: $Image"
