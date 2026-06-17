# Runs Unity Edit Mode tests for Harmonic Engine (requires Unity Hub editor on PATH).
param(
    [string]$UnityPath = "",
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$ResultsPath = (Join-Path $PSScriptRoot ".." "TestResults" "editmode-results.xml")
)

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $hubEditors = "${env:ProgramFiles}\Unity\Hub\Editor"
    if (Test-Path $hubEditors) {
        $latest = Get-ChildItem $hubEditors -Directory | Sort-Object Name -Descending | Select-Object -First 1
        if ($latest) {
            $UnityPath = Join-Path $latest.FullName "Editor\Unity.exe"
        }
    }
}

if (-not (Test-Path $UnityPath)) {
    Write-Error "Unity.exe not found. Pass -UnityPath or install Unity Hub."
    exit 1
}

$resultsDir = Split-Path $ResultsPath -Parent
if (-not (Test-Path $resultsDir)) {
    New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null
}

& $UnityPath `
    -batchmode `
    -nographics `
    -projectPath $ProjectPath `
    -runTests `
    -testPlatform editmode `
    -testResults $ResultsPath `
    -logFile (Join-Path $resultsDir "editmode.log")

exit $LASTEXITCODE
