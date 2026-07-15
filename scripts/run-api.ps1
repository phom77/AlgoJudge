$ErrorActionPreference = "Stop"

. "$PSScriptRoot/load-env.ps1"

dotnet run --project src/AlgoJudge.API
