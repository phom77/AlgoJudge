$envFile = Join-Path (Split-Path $PSScriptRoot -Parent) ".env"

if (-not (Test-Path $envFile)) {
    throw "Missing .env. Copy .env.example to .env and set local secrets first."
}

Get-Content $envFile | ForEach-Object {
    $line = $_.Trim()
    if ($line.Length -eq 0 -or $line.StartsWith("#")) {
        return
    }

    $parts = $line.Split("=", 2)
    if ($parts.Length -eq 2) {
        [Environment]::SetEnvironmentVariable(
            $parts[0].Trim(),
            $parts[1].Trim(),
            [EnvironmentVariableTarget]::Process)
    }
}
