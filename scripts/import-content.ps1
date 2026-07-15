param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = "Stop"

dotnet run --project src/AlgoJudge.ContentTool -- validate $PackagePath
