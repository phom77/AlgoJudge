param(
    [string]$Image = "algojudge/content-generator-dotnet:10.0.203-v1"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repositoryRoot
try {
    docker build `
        --file infra/docker/content-generator-dotnet.Dockerfile `
        --tag $Image `
        infra/docker
}
finally {
    Pop-Location
}

Write-Host "Built content generator image: $Image"
