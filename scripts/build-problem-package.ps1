param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression

$sourceDirectory = (Resolve-Path -LiteralPath $SourcePath -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $sourceDirectory -PathType Container)) {
    throw "Problem package source must be a directory: $sourceDirectory"
}

$destinationPath = [IO.Path]::GetFullPath($PackagePath)
if ([IO.Path]::GetExtension($destinationPath) -ne ".zip") {
    throw "Problem package destination must use the .zip extension."
}

$sourcePrefix = $sourceDirectory.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if ($destinationPath.StartsWith($sourcePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Problem package destination cannot be inside its source directory."
}

$destinationDirectory = Split-Path -Parent $destinationPath
New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null

$archiveStream = [IO.File]::Open(
    $destinationPath,
    [IO.FileMode]::Create,
    [IO.FileAccess]::Write,
    [IO.FileShare]::None)
try {
    $archive = [IO.Compression.ZipArchive]::new(
        $archiveStream,
        [IO.Compression.ZipArchiveMode]::Create,
        $false)
    try {
        # Authoring-only generator/reference files stay outside the schema-v1 ZIP.
        $sourceFiles = Get-ChildItem -LiteralPath $sourceDirectory -Recurse -File |
            Where-Object {
                $relativePath = $_.FullName.Substring($sourcePrefix.Length).Replace("\", "/")
                $relativePath -in @("problem.json", "statement.md", "constraints.md") -or
                    $relativePath -match "^(samples|tests)/[0-9]{2,4}\.(in|out|md)$"
            }
        foreach ($sourceFile in $sourceFiles) {
            $relativePath = $sourceFile.FullName.Substring($sourcePrefix.Length).Replace("\", "/")
            $entry = $archive.CreateEntry(
                $relativePath,
                [IO.Compression.CompressionLevel]::Optimal)
            $entryStream = $entry.Open()
            $fileStream = [IO.File]::OpenRead($sourceFile.FullName)
            try {
                $fileStream.CopyTo($entryStream)
            }
            finally {
                $fileStream.Dispose()
                $entryStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $archiveStream.Dispose()
}

Write-Host "Built problem package: $destinationPath"
