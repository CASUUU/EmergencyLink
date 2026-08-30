$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "src\EmergencyLink"
$outDir = Join-Path $root "dist"
$outFile = Join-Path $outDir "EmergencyLink.exe"
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $csc)) {
    throw "C# compiler not found: $csc"
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sources = @()
$sources += Get-ChildItem -LiteralPath $src -Filter *.cs | ForEach-Object { $_.FullName }
$sources += Get-ChildItem -LiteralPath (Join-Path $src "Forms") -Filter *.cs | ForEach-Object { $_.FullName }

& $csc `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /optimize+ `
    /codepage:65001 `
    /out:$outFile `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $sources

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

Write-Host "Built $outFile"
