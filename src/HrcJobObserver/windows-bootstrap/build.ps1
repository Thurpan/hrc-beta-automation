[CmdletBinding()]
param(
    [string]$DotnetPath = 'C:\Program Files\dotnet\dotnet.exe'
)

$ErrorActionPreference = 'Stop'
$moduleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not (Test-Path -LiteralPath $DotnetPath -PathType Leaf)) {
    throw "Required .NET tool was not found: $DotnetPath"
}

$buildMutex = [Threading.Mutex]::new(
    $false, 'Local\HrcBetaAutomation-WindowsBootstrap-Build-v1')
$lockTaken = $false
try {
    try {
        $lockTaken = $buildMutex.WaitOne([TimeSpan]::FromMinutes(5))
    } catch [Threading.AbandonedMutexException] {
        $lockTaken = $true
    }
    if (-not $lockTaken) {
        throw 'Timed out waiting for the Windows bootstrap build lock.'
    }

    $buildRoot = [IO.Path]::GetFullPath((Join-Path $moduleRoot 'build'))
    $expectedRoot = [IO.Path]::GetFullPath($moduleRoot) +
        [IO.Path]::DirectorySeparatorChar + 'build'
    if (-not $buildRoot.Equals(
            $expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean an unexpected build directory: $buildRoot"
    }
    if (Test-Path -LiteralPath $buildRoot) {
        Remove-Item -LiteralPath $buildRoot -Recurse -Force
    }

    $sources = Get-ChildItem -LiteralPath @(
        (Join-Path $moduleRoot 'src'),
        (Join-Path $moduleRoot 'test')) -Recurse -Filter '*.cs'
    $forbidden = $sources | Select-String -Pattern `
        'System\.Net\.|HttpClient|Environment\.GetEnvironmentVariable|Console\.(Write|Error)|ProcessStartInfo|Process\.Start|Microsoft\.Win32\.Registry|HRC Beta|HoldemResources'
    if ($forbidden) {
        throw 'Windows bootstrap source crossed its offline or data boundary.'
    }

    $project = Join-Path $moduleRoot `
        'HrcJobObserver.WindowsBootstrap.TestHarness.csproj'
    & $DotnetPath restore $project --configfile `
        (Join-Path $moduleRoot 'NuGet.Config')
    if ($LASTEXITCODE -ne 0) {
        throw "Windows bootstrap restore failed with exit code $LASTEXITCODE"
    }
    & $DotnetPath run --project $project -c Release `
        --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Windows bootstrap validation failed with exit code $LASTEXITCODE"
    }

    $assets = Get-ChildItem -LiteralPath (Join-Path $buildRoot 'obj') `
        -Recurse -Filter 'project.assets.json'
    $assetParents = @($assets.Directory.Name | Sort-Object -Unique)
    if ($assets.Count -ne 2 -or $assetParents.Count -ne 2) {
        throw 'The library and harness did not use isolated intermediate outputs.'
    }
} finally {
    if ($lockTaken) {
        $buildMutex.ReleaseMutex()
    }
    $buildMutex.Dispose()
}
