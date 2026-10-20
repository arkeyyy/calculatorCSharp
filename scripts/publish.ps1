param(
    [ValidateSet('win-x64', 'win-arm64', 'win-x86')][string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$workspace = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$destination = Join-Path $workspace "artifacts/publish/$Runtime"
Push-Location -LiteralPath $workspace
try {
    & dotnet publish Calculator/Calculator.csproj --configuration Release --runtime $Runtime --self-contained true --output $destination -p:PublishSingleFile=false -p:DebugType=None -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained publication failed.' }
    # Runtime pack license files are not automatically included by dotnet publish.
    $assets = Get-Content -LiteralPath 'Calculator/obj/project.assets.json' -Raw | ConvertFrom-Json
    $runtimeNames = @("Microsoft.NETCore.App.Runtime.$Runtime", "Microsoft.WindowsDesktop.App.Runtime.$Runtime")
    $dependencies = @($assets.project.frameworks.PSObject.Properties | ForEach-Object { $_.Value.downloadDependencies })
    $noticeDirectory = Join-Path $destination 'licenses'
    New-Item -ItemType Directory -Path $noticeDirectory -Force | Out-Null
    foreach ($runtimeName in $runtimeNames) {
        $dependency = $dependencies | Where-Object { $_.name -eq $runtimeName } | Select-Object -First 1
        if (-not $dependency) { throw "Runtime metadata was not found for $runtimeName." }
        $version = ($dependency.version -replace '^\[', '' -split ',')[0].Trim()
        $packageDirectory = $null
        foreach ($packageRoot in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate = Join-Path (Join-Path $packageRoot $runtimeName.ToLowerInvariant()) $version
            if (Test-Path -LiteralPath $candidate) { $packageDirectory = $candidate; break }
        }
        if (-not $packageDirectory) { throw "Runtime package was not found for $runtimeName $version." }
        $notices = @(Get-ChildItem -LiteralPath $packageDirectory -File | Where-Object { $_.Name -match '^(LICENSE(\.TXT)?|THIRD-PARTY-NOTICES\.TXT)$' })
        if ($notices.Count -eq 0) { throw "Runtime notices were not found for $runtimeName." }
        foreach ($notice in $notices) {
            Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $noticeDirectory "$runtimeName-$($notice.Name)")
        }
    }
    Write-Output "Published Calculator.exe and its runtime to $destination"
} finally { Pop-Location }
