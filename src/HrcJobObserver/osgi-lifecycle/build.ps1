[CmdletBinding()]
param(
    [string]$HrcInstallPath =
        'C:\Users\euanh\AppData\Local\Programs\HoldemResources\HRC Beta',
    [string]$JavacPath =
        'C:\Program Files\Android\Android Studio\jbr\bin\javac.exe',
    [string]$JavaPath =
        'C:\Program Files\Android\Android Studio\jbr\bin\java.exe'
)

$ErrorActionPreference = 'Stop'
$lifecycleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$componentRoot = Split-Path -Parent $lifecycleRoot
$runtimeRoot = Join-Path $componentRoot 'runtime-assembly'

foreach ($tool in @($JavacPath, $JavaPath)) {
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
        throw "Required Java tool was not found: $tool"
    }
}

& (Join-Path $runtimeRoot 'build.ps1') `
    -HrcInstallPath $HrcInstallPath -JavacPath $JavacPath -JavaPath $JavaPath
if ($LASTEXITCODE -ne 0) {
    throw "Runtime assembly validation failed with exit code $LASTEXITCODE"
}

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

    $plugins = Join-Path $HrcInstallPath 'plugins'
    $dependencies = @(
        @{
            Name = 'org.eclipse.core.jobs_3.15.500.v20250204-0817.jar'
            Hash = '189199CD46A284220B7B97FD59218B533FE9FD8E0AD22258F674A3F2DF4DE7C9'
        }
        @{
            Name = 'org.eclipse.equinox.common_3.20.0.v20250129-1348.jar'
            Hash = '617C5D7E759276B7E9ED363C56A6714B7F21D4A812D533FCB90E48723CC4C001'
        }
        @{
            Name = 'org.eclipse.osgi_3.23.0.v20250228-0640.jar'
            Hash = '1AC113541A19F0C72C0421FB24058DEFCA7E3C6F282E5EE73F14D2768A9AE653'
        }
    )
    $dependencyPaths = foreach ($dependency in $dependencies) {
        $path = Join-Path $plugins $dependency.Name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required Eclipse dependency was not found: $path"
        }
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash `
                -cne $dependency.Hash) {
            throw "Eclipse dependency hash mismatch: $($dependency.Name)"
        }
        $path
    }

    $sourceRoot = Join-Path $lifecycleRoot 'src'
    $testRoot = Join-Path $lifecycleRoot 'test'
    $mainSources = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse `
        -Filter '*.java') | Sort-Object FullName |
        Select-Object -ExpandProperty FullName
    $testSources = @(Get-ChildItem -LiteralPath $testRoot -Recurse `
        -Filter '*.java') | Sort-Object FullName |
        Select-Object -ExpandProperty FullName
    if ($mainSources.Count -eq 0 -or $testSources.Count -eq 0) {
        throw 'Lifecycle main or test Java sources were not found.'
    }

    $forbiddenInternal = Get-ChildItem -LiteralPath $sourceRoot -Recurse `
        -Filter '*.java' | Select-String -Pattern 'org\.eclipse\..*\.internal'
    if ($forbiddenInternal) {
        throw 'Lifecycle source must not import internal Eclipse APIs.'
    }
    $forbiddenIo = Get-ChildItem -LiteralPath $sourceRoot -Recurse `
        -Filter '*.java' | Select-String -Pattern `
            'java\.(io|net)\.|java\.nio\.file\.'
    if ($forbiddenIo) {
        throw 'Lifecycle source crossed its no-file-I/O and no-direct-network boundary.'
    }
    $packagingArtifacts = Get-ChildItem -LiteralPath $lifecycleRoot -Recurse `
        -File | Where-Object { $_.Name -match 'MANIFEST\.MF|plugin\.xml' }
    if ($packagingArtifacts) {
        throw 'Lifecycle directory contains a forbidden packaging artefact.'
    }

    $publicTypes = Get-ChildItem -LiteralPath $sourceRoot -Recurse `
        -Filter '*.java' | Select-String -Pattern `
            '^public (?:final )?(?:class|interface|record|enum) '
    if ($publicTypes.Count -ne 1 `
            -or $publicTypes.Line -notmatch 'HrcJobObserverActivator') {
        throw 'Only the Bundle activator may be a public lifecycle type.'
    }

    $buildRoot = [IO.Path]::GetFullPath((Join-Path $lifecycleRoot 'build'))
    $expectedRoot = [IO.Path]::GetFullPath($lifecycleRoot) +
        [IO.Path]::DirectorySeparatorChar + 'build'
    if (-not $buildRoot.Equals(
            $expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean an unexpected lifecycle build directory: $buildRoot"
    }
    if (Test-Path -LiteralPath $buildRoot) {
        Remove-Item -LiteralPath $buildRoot -Recurse -Force
    }
    $mainOutput = Join-Path $buildRoot 'main-classes'
    $testOutput = Join-Path $buildRoot 'test-classes'
    New-Item -ItemType Directory -Force -Path $mainOutput,$testOutput | Out-Null

    $classPath = [string]::Join(
        [IO.Path]::PathSeparator,
        @(
            (Join-Path $componentRoot 'build\main-classes'),
            (Join-Path $componentRoot 'eclipse-adapter\build\main-classes'),
            (Join-Path $componentRoot 'local-transport\build\main-classes'),
            (Join-Path $runtimeRoot 'build\main-classes')
        ) + $dependencyPaths)
    & $JavacPath --release 17 -proc:none -Xlint:all -Werror `
        -cp $classPath -d $mainOutput $mainSources
    if ($LASTEXITCODE -ne 0) {
        throw "lifecycle main javac failed with exit code $LASTEXITCODE"
    }

    $testClassPath = $classPath + [IO.Path]::PathSeparator + $mainOutput
    & $JavacPath --release 17 -proc:none -Xlint:all -Werror `
        -cp $testClassPath -d $testOutput $testSources
    if ($LASTEXITCODE -ne 0) {
        throw "lifecycle test javac failed with exit code $LASTEXITCODE"
    }
    & $JavaPath -ea `
        -cp ($testClassPath + [IO.Path]::PathSeparator + $testOutput) `
        net.hrcautomation.jobobserver.ObserverOsgiLifecycleTest
    if ($LASTEXITCODE -ne 0) {
        throw "ObserverOsgiLifecycleTest failed with exit code $LASTEXITCODE"
    }

    $forbiddenOutput = Get-ChildItem -LiteralPath $mainOutput -Recurse -File |
        Where-Object { $_.Name -match 'Test|MANIFEST|plugin\.xml' }
    if ($forbiddenOutput) {
        throw 'Lifecycle main output contains a test or packaging artefact.'
    }
} finally {
    if ($lockTaken) {
        $buildMutex.ReleaseMutex()
    }
    $buildMutex.Dispose()
}
