param(
    [Parameter(Mandatory = $true)]
    [string]$CatalogPath,

    [switch]$Validate,

    [switch]$Resolve,

    [switch]$Import,

    [string]$ApiBaseUrl = "http://localhost:5016"
)

$ErrorActionPreference = "Stop"

$modeCount = @($Validate, $Resolve, $Import).Where({ $_ }).Count
if ($modeCount -ne 1) {
    throw "Specify exactly one of -Validate, -Resolve, or -Import."
}

$command = if ($Validate) {
    "validate"
} elseif ($Resolve) {
    "resolve"
} else {
    "import"
}

$toolArguments = @("workspace", $command, $CatalogPath)
if ($Import) {
    $toolArguments += @("--api-base-url", $ApiBaseUrl)
}

dotnet run --project src/AlgoJudge.ContentTool -- @toolArguments
exit $LASTEXITCODE
