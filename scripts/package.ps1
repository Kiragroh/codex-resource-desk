param([string]$OutputDirectory = 'artifacts/ResourceDesk-0.1.2-win-x64')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    dotnet run --project tests/ResourceDesk.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Fixture tests failed.' }
    dotnet publish src/ResourceDesk.App/ResourceDesk.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item README.md, LICENSE, THIRD-PARTY-NOTICES.md -Destination $OutputDirectory
    Copy-Item licenses -Destination $OutputDirectory -Recurse -Force
    $out = (Resolve-Path -LiteralPath $OutputDirectory).Path
    $lines = Get-ChildItem -LiteralPath $out -Recurse -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object FullName | ForEach-Object {
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetRelativePath($out, $_.FullName)
    }
    $lines | Set-Content -LiteralPath (Join-Path $out 'SHA256SUMS.txt') -Encoding utf8
    $zip = $out + '.zip'
    if (Test-Path -LiteralPath $zip) { throw "ZIP already exists: $zip. Choose a new output directory to retain the previous release." }
    Compress-Archive -LiteralPath $out -DestinationPath $zip -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    ($hash + '  ' + [IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath ($zip + '.sha256') -Encoding utf8
    Write-Output "Portable package: $zip"
} finally { Pop-Location }
