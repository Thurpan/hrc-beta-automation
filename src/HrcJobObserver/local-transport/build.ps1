[CmdletBinding()]
param(
    [string]$JavacPath =
        'C:\Program Files\Android\Android Studio\jbr\bin\javac.exe',
    [string]$JavaPath =
        'C:\Program Files\Android\Android Studio\jbr\bin\java.exe'
)

$ErrorActionPreference = 'Stop'
$buildMutex = [Threading.Mutex]::new(
    $false, 'Local\HrcBetaAutomation-HrcJobObserver-Build-v1')
$lockTaken = $false
try {
    try {
        $lockTaken = $buildMutex.WaitOne([TimeSpan]::FromMinutes(5))
    } catch [Threading.AbandonedMutexException] {
        $lockTaken = $true
    }
    if (-not $lockTaken) {
        throw 'Timed out waiting for the observer build lock.'
    }

    $transportRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $componentRoot = Split-Path -Parent $transportRoot
    foreach ($tool in @($JavacPath, $JavaPath)) {
        if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
            throw "Required Java tool was not found: $tool"
        }
    }

    $forbiddenBoundary = Get-ChildItem -LiteralPath (Join-Path $transportRoot 'src') `
        -Recurse -Filter '*.java' | Select-String -Pattern `
            'org\.eclipse\.|BundleActivator|addJobChangeListener|removeJobChangeListener|java\.nio\.file\.|java\.io\.(File|FileInputStream|FileOutputStream|FileReader|FileWriter|RandomAccessFile)'
    if ($forbiddenBoundary) {
        throw 'Offline transport source crossed its targeted no-Eclipse or no-file-I/O boundary.'
    }

    & (Join-Path $componentRoot 'build.ps1') `
        -JavacPath $JavacPath -JavaPath $JavaPath -BuildLockHeld
    if ($LASTEXITCODE -ne 0) {
        throw "Observer core validation failed with exit code $LASTEXITCODE"
    }

    $coreMain = Join-Path $componentRoot 'build\main-classes'
    $transportMain = Join-Path $transportRoot 'build\main-classes'
    $transportTest = Join-Path $transportRoot 'build\test-classes'
    $buildRoot = [IO.Path]::GetFullPath((Join-Path $transportRoot 'build'))
    $expectedRoot = [IO.Path]::GetFullPath($transportRoot) +
        [IO.Path]::DirectorySeparatorChar + 'build'
    if (-not $buildRoot.Equals($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean an unexpected transport build directory: $buildRoot"
    }
    if (Test-Path -LiteralPath $buildRoot) {
        Remove-Item -LiteralPath $buildRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $transportMain,$transportTest | Out-Null

    $mainSources = @(
        Get-ChildItem -LiteralPath (Join-Path $transportRoot 'src') `
            -Recurse -Filter '*.java'
    ) | Sort-Object FullName | Select-Object -ExpandProperty FullName
    $testSources = @(
        Get-ChildItem -LiteralPath (Join-Path $transportRoot 'test') `
            -Recurse -Filter '*.java'
    ) | Sort-Object FullName | Select-Object -ExpandProperty FullName
    if ($mainSources.Count -eq 0 -or $testSources.Count -eq 0) {
        throw 'Transport main or test Java sources were not found.'
    }

    & $JavacPath --release 17 -proc:none -Xlint:all -Werror `
        -cp $coreMain -d $transportMain $mainSources
    if ($LASTEXITCODE -ne 0) {
        throw "transport main javac failed with exit code $LASTEXITCODE"
    }
    $testClassPath = $coreMain + [IO.Path]::PathSeparator + $transportMain
    & $JavacPath --release 17 -proc:none -Xlint:all -Werror `
        -cp $testClassPath -d $transportTest $testSources
    if ($LASTEXITCODE -ne 0) {
        throw "transport test javac failed with exit code $LASTEXITCODE"
    }
    $runClassPath = $testClassPath + [IO.Path]::PathSeparator + $transportTest
    & $JavaPath -ea -cp $runClassPath `
        net.hrcautomation.jobobserver.LocalTransportTest
    if ($LASTEXITCODE -ne 0) {
        throw "LocalTransportTest failed with exit code $LASTEXITCODE"
    }

    $forbiddenArtifacts = Get-ChildItem -LiteralPath $transportMain -Recurse -File |
        Where-Object { $_.Name -match 'Test|Activator|MANIFEST|plugin\.xml' }
    if ($forbiddenArtifacts) {
        throw 'Transport main output contains a test, activator, or packaging artefact.'
    }
} finally {
    if ($lockTaken) {
        $buildMutex.ReleaseMutex()
    }
    $buildMutex.Dispose()
}
