param(
    [string]$Runtime = "win-x64",
    [string]$Output = "publish"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

Write-Host "Publishing $Runtime single-file build to $Output ..."

dotnet publish FrcResearchTest.csproj `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $Output

Copy-Item README.md $Output -Force

$exe = Join-Path $Output "FrcResearchTest.exe"
$config = Join-Path $Output "config.json"
$questions = Join-Path $Output "questions.json"

foreach ($required in @($exe, $config, $questions)) {
    if (-not (Test-Path $required)) {
        throw "Publish did not produce $required"
    }
}

Write-Host ""
Write-Host "Published: $exe"
Write-Host "Size:      $([math]::Round((Get-Item $exe).Length / 1MB, 1)) MB"
Write-Host "Verify:    $exe --selftest"
