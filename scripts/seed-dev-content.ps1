param(
    [string]$Slug = "two-sum"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repositoryRoot "content/dev/$Slug"
$generatedDirectory = Join-Path $repositoryRoot "content/.generated"
$packagePath = Join-Path $generatedDirectory "$Slug.zip"
$contentToolProject = Join-Path $repositoryRoot "src/AlgoJudge.ContentTool"

if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
    throw "Development problem fixture does not exist: $sourcePath"
}

. "$PSScriptRoot/load-env.ps1"

New-Item -ItemType Directory -Path $generatedDirectory -Force | Out-Null
& "$PSScriptRoot/build-problem-package.ps1" `
    -SourcePath $sourcePath `
    -PackagePath $packagePath

dotnet run --project $contentToolProject -- validate $packagePath
if ($LASTEXITCODE -ne 0) {
    throw "Development problem package validation failed."
}

dotnet run --project $contentToolProject -- import $packagePath
if ($LASTEXITCODE -ne 0) {
    throw "Development problem import failed."
}

dotnet run --project $contentToolProject -- publish $Slug
if ($LASTEXITCODE -ne 0) {
    throw "Development problem publication failed."
}

Write-Host "Development problem '$Slug' is published. Refresh the problem catalogue."
