[CmdletBinding()]
param(
    [string]$JavacPath =
        'C:\Program Files\Android\Android Studio\jbr\bin\javac.exe',
    [string]$JavaPath =
        'C:\Program Files\Android\Android Studio\jbr\bin\java.exe'
)

$ErrorActionPreference = 'Stop'
$buildMutex = [Threading.Mutex]::new(
    $false, 'Local\HrcBetaAutomation-OfflineOsgiPackaging-Build-v1')
$lockTaken = $false
$buildRoot = $null
try {
    try {
        $lockTaken = $buildMutex.WaitOne([TimeSpan]::FromMinutes(5))
    } catch [Threading.AbandonedMutexException] {
        $lockTaken = $true
    }
    if (-not $lockTaken) {
        throw 'Timed out waiting for the offline packaging build lock.'
    }

    foreach ($tool in @($JavacPath, $JavaPath)) {
        if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
            throw "Required Java tool was not found: $tool"
        }
    }

    $moduleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $sourceRoot = Join-Path $moduleRoot 'src'
    $testRoot = Join-Path $moduleRoot 'test'
    $mainSources = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.java' |
        Sort-Object FullName | Select-Object -ExpandProperty FullName)
    $testSources = @(Get-ChildItem -LiteralPath $testRoot -Recurse -Filter '*.java' |
        Sort-Object FullName | Select-Object -ExpandProperty FullName)
    if ($mainSources.Count -eq 0 -or $testSources.Count -eq 0) {
        throw 'Offline packaging main or test sources were not found.'
    }

    $forbiddenSource = Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.java' |
        Select-String -Pattern @(
            'java\.nio\.file\.',
            'java\.(io|net)\.',
            'org\.osgi\.',
            'ProcessBuilder',
            'Runtime\.getRuntime',
            'BundleActivator',
            'addJobChangeListener',
            'removeJobChangeListener'
        )
    if ($forbiddenSource) {
        throw 'Offline packaging source crossed its no-I/O, no-process, or no-activation boundary.'
    }

    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $buildRoot = [IO.Path]::GetFullPath((Join-Path $temporaryRoot (
        'hrc-job-observer-offline-packaging-' + [Guid]::NewGuid().ToString('N'))))
    $requiredPrefix = $temporaryRoot + [IO.Path]::DirectorySeparatorChar +
        'hrc-job-observer-offline-packaging-'
    if (-not $buildRoot.StartsWith(
            $requiredPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to use an unexpected offline packaging build directory.'
    }

    $mainClasses = Join-Path $buildRoot 'main-classes'
    $testClasses = Join-Path $buildRoot 'test-classes'
    New-Item -ItemType Directory -Path $mainClasses,$testClasses | Out-Null

    & $JavacPath --release 17 -proc:none -Xlint:all -Werror `
        -encoding UTF-8 -d $mainClasses $mainSources
    if ($LASTEXITCODE -ne 0) {
        throw "offline packaging main javac failed with exit code $LASTEXITCODE"
    }
    & $JavacPath --release 17 -proc:none -Xlint:all -Werror `
        -encoding UTF-8 -cp $mainClasses -d $testClasses $testSources
    if ($LASTEXITCODE -ne 0) {
        throw "offline packaging test javac failed with exit code $LASTEXITCODE"
    }
    & $JavaPath -ea `
        -cp ($mainClasses + [IO.Path]::PathSeparator + $testClasses) `
        net.hrcautomation.jobobserver.packaging.SimpleConfiguratorPackagingTest
    if ($LASTEXITCODE -ne 0) {
        throw "SimpleConfiguratorPackagingTest failed with exit code $LASTEXITCODE"
    }

    $forbiddenArtefacts = Get-ChildItem -LiteralPath $moduleRoot -Recurse -File |
        Where-Object { $_.Name -match '^(MANIFEST\.MF|plugin\.xml)$|\.jar$' }
    if ($forbiddenArtefacts) {
        throw 'Offline packaging module contains an activatable or installable artefact.'
    }
} finally {
    if ($null -ne $buildRoot -and (Test-Path -LiteralPath $buildRoot)) {
        $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd(
            [IO.Path]::DirectorySeparatorChar,
            [IO.Path]::AltDirectorySeparatorChar)
        $requiredPrefix = $temporaryRoot + [IO.Path]::DirectorySeparatorChar +
            'hrc-job-observer-offline-packaging-'
        if ($buildRoot.StartsWith(
                $requiredPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $buildRoot -Recurse -Force
        }
    }
    if ($lockTaken) {
        $buildMutex.ReleaseMutex()
    }
    $buildMutex.Dispose()
}
