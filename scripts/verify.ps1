param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$SkipUi,
    [ValidateSet(0, 96, 120, 144)][int]$ExpectedDpi = 0
)

$ErrorActionPreference = 'Stop'
$workspace = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $workspace
try {
    & dotnet restore Calculator.sln
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
    & dotnet build Calculator.sln --no-restore --configuration $Configuration -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
    & dotnet test Calculator.Core.Tests/Calculator.Core.Tests.csproj --no-build --configuration $Configuration --logger 'trx;LogFileName=core.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
    if (-not $SkipUi) {
        $previousDpi = $env:CALCULATOR_EXPECT_DPI
        try {
            if ($ExpectedDpi -gt 0) { $env:CALCULATOR_EXPECT_DPI = $ExpectedDpi.ToString() }
            & dotnet test Calculator.Ui.Tests/Calculator.Ui.Tests.csproj --no-build --configuration $Configuration --logger 'trx;LogFileName=ui.trx'
            if ($LASTEXITCODE -ne 0) { throw 'UI tests failed.' }
        } finally { $env:CALCULATOR_EXPECT_DPI = $previousDpi }
    }
} finally { Pop-Location }
