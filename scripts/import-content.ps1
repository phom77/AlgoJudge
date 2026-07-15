param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [switch]$ValidateOnly,

    [switch]$Replace
)

$ErrorActionPreference = "Stop"

if ($ValidateOnly -and $Replace) {
    throw "-Replace cannot be combined with -ValidateOnly."
}

$command = if ($ValidateOnly) { "validate" } else { "import" }

if (-not $ValidateOnly) {
    . "$PSScriptRoot/load-env.ps1"
}

$toolArguments = @($command, $PackagePath)
if ($Replace) {
    $toolArguments += "--replace"
}

dotnet run --project src/AlgoJudge.ContentTool -- @toolArguments
exit $LASTEXITCODE
