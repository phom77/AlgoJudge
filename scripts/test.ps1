$ErrorActionPreference = "Stop"

dotnet restore AlgoJudge.slnx
dotnet build AlgoJudge.slnx --no-restore
dotnet test AlgoJudge.slnx --no-build
