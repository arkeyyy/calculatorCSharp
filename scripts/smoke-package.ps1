param(
    [ValidateSet('win-x64', 'win-arm64', 'win-x86')][string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$workspace = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$package = Join-Path $workspace "artifacts/publish/$Runtime"
$executable = Join-Path $package 'Calculator.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Publish the application first: $executable"
}

if (-not ('CalculatorPackageSmoke.NativeWindow' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CalculatorPackageSmoke
{
    public sealed class WindowBounds
    {
        public uint Dpi;
        public int Width;
        public int Height;
    }

    public static class NativeWindow
    {
        private delegate bool EnumWindowsCallback(IntPtr handle, IntPtr parameter);
        [StructLayout(LayoutKind.Sequential)]
        private struct Rect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr handle, StringBuilder text, int capacity);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr handle, out Rect rectangle);
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr handle);
        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

        public static IntPtr Find(uint processId)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr handle, IntPtr parameter)
            {
                uint owner;
                GetWindowThreadProcessId(handle, out owner);
                if (owner != processId) return true;
                var title = new StringBuilder(256);
                GetWindowText(handle, title, title.Capacity);
                if (title.ToString() != "Calculator") return true;
                found = handle;
                return false;
            }, IntPtr.Zero);
            return found;
        }

        public static WindowBounds Measure(IntPtr handle)
        {
            IntPtr previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
            if (previous == IntPtr.Zero)
                throw new InvalidOperationException("Cannot measure with per-monitor DPI awareness.");
            try
            {
                Rect rectangle;
                uint dpi = GetDpiForWindow(handle);
                if (dpi == 0 || !GetWindowRect(handle, out rectangle))
                    throw new InvalidOperationException("Cannot read the Calculator window bounds.");
                return new WindowBounds {
                    Dpi = dpi,
                    Width = rectangle.Right - rectangle.Left,
                    Height = rectangle.Bottom - rectangle.Top
                };
            }
            finally { SetThreadDpiAwarenessContext(previous); }
        }

        public static bool Close(IntPtr handle, uint processId)
        {
            uint owner;
            GetWindowThreadProcessId(handle, out owner);
            return owner == processId && PostMessage(handle, 0x0010, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
'@
}

function Get-ProfileFingerprint([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return 'absent' }
    $item = Get-Item -LiteralPath $Path
    return '{0}:{1}:{2}' -f $item.Length, $item.LastWriteTimeUtc.Ticks, (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

$normalSettings = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Calculator/settings.json'
$originalProfile = Get-ProfileFingerprint $normalSettings
$runDirectory = Join-Path $workspace ('artifacts/smoke/{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), [Guid]::NewGuid().ToString('N'))
[void][System.IO.Directory]::CreateDirectory($runDirectory)
$results = @()

foreach ($mode in @('STANDARD', 'GRAPHING')) {
    $dataDirectory = Join-Path $runDirectory $mode
    [void][System.IO.Directory]::CreateDirectory($dataDirectory)
    $settingsPath = Join-Path $dataDirectory 'settings.json'
    @{
        Version = 1
        LegacyImportCompleted = $true
        Values = @{ mode = $mode; theme = 'light'; accent = 'BLUE' }
    } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $settingsPath -Encoding UTF8

    $process = $null
    $window = [IntPtr]::Zero
    try {
        $previousDataDirectory = $env:CALCULATOR_DATA_DIR
        $previousImport = $env:CALCULATOR_SKIP_LEGACY_IMPORT
        try {
            $env:CALCULATOR_DATA_DIR = $dataDirectory
            $env:CALCULATOR_SKIP_LEGACY_IMPORT = '1'
            $process = Start-Process -FilePath $executable -WorkingDirectory $package -WindowStyle Hidden -PassThru
        }
        finally {
            $env:CALCULATOR_DATA_DIR = $previousDataDirectory
            $env:CALCULATOR_SKIP_LEGACY_IMPORT = $previousImport
        }

        $timer = [Diagnostics.Stopwatch]::StartNew()
        $stableSamples = 0
        $lastSize = ''
        $bounds = $null
        while ($timer.Elapsed.TotalSeconds -lt 20) {
            $process.Refresh()
            if ($process.HasExited) { throw "$mode exited before its window was ready." }
            $window = [CalculatorPackageSmoke.NativeWindow]::Find([uint32]$process.Id)
            if ($window -ne [IntPtr]::Zero) {
                $bounds = [CalculatorPackageSmoke.NativeWindow]::Measure($window)
                $size = '{0}x{1}@{2}' -f $bounds.Width, $bounds.Height, $bounds.Dpi
                if ($size -eq $lastSize) { $stableSamples++ } else { $stableSamples = 0 }
                $lastSize = $size
                if ($stableSamples -ge 3) { break }
            }
            Start-Sleep -Milliseconds 200
        }
        if ($stableSamples -lt 3 -or $null -eq $bounds) {
            throw "$mode did not produce a stable native window within 20 seconds."
        }

        $logicalWidth = $bounds.Width * 96.0 / $bounds.Dpi
        $logicalHeight = $bounds.Height * 96.0 / $bounds.Dpi
        if ($mode -eq 'STANDARD') {
            if ([Math]::Abs($logicalWidth - 400) -gt 1 -or [Math]::Abs($logicalHeight - 600) -gt 1) {
                throw "STANDARD launch bounds are $logicalWidth x $logicalHeight logical pixels; expected 400 x 600."
            }
        }
        elseif ($logicalWidth -lt 799 -or $logicalHeight -lt 639) {
            throw "GRAPHING launch bounds are $logicalWidth x $logicalHeight logical pixels; expected at least 800 x 640."
        }

        if (-not [CalculatorPackageSmoke.NativeWindow]::Close($window, [uint32]$process.Id)) {
            throw "Could not request a normal close of the owned $mode process."
        }
        if (-not $process.WaitForExit(10000)) { throw "$mode did not close normally within 10 seconds." }
        $process.Refresh()
        if ($process.ExitCode -ne 0) { throw "$mode exited with code $($process.ExitCode)." }

        $savedSettings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
        if ($savedSettings.Values.mode -ne $mode) { throw "$mode did not preserve its isolated saved mode." }
        if ((Get-ProfileFingerprint $normalSettings) -ne $originalProfile) {
            throw 'The normal Calculator settings profile changed during isolated package verification.'
        }
        $result = [pscustomobject]@{
            Mode = $mode
            Dpi = $bounds.Dpi
            PhysicalWidth = $bounds.Width
            PhysicalHeight = $bounds.Height
            LogicalWidth = $logicalWidth
            LogicalHeight = $logicalHeight
            ExitCode = $process.ExitCode
            SettingsDirectory = $dataDirectory
        }
        $results += $result
        Write-Output ("{0}: {1} x {2} logical pixels at {3} DPI; clean exit." -f $mode, $logicalWidth, $logicalHeight, $bounds.Dpi)
    }
    finally {
        if ($null -ne $process) {
            $process.Refresh()
            if (-not $process.HasExited) {
                $window = [CalculatorPackageSmoke.NativeWindow]::Find([uint32]$process.Id)
                if ($window -ne [IntPtr]::Zero) {
                    [void][CalculatorPackageSmoke.NativeWindow]::Close($window, [uint32]$process.Id)
                }
                if (-not $process.WaitForExit(5000)) {
                    # Only the process created by this iteration is terminated after a failed smoke check.
                    $process.Kill()
                    $process.WaitForExit()
                }
            }
            $process.Dispose()
        }
    }
}

$report = Join-Path $runDirectory 'results.json'
$results | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $report -Encoding UTF8
Write-Output "Package smoke verification passed. Normal settings unchanged. Results: $report"
