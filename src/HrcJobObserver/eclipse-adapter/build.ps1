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

$adapterRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$componentRoot = Split-Path -Parent $adapterRoot
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

foreach ($tool in @($JavacPath, $JavaPath)) {
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
        throw "Required Java tool was not found: $tool"
    }
}
if (-not (Test-Path -LiteralPath $plugins -PathType Container)) {
    throw "HRC plug-ins directory was not found: $plugins"
}

$dependencyPaths = foreach ($dependency in $dependencies) {
    $path = Join-Path $plugins $dependency.Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required Eclipse dependency was not found: $path"
    }
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
    if ($actual -cne $dependency.Hash) {
        throw "Eclipse dependency hash mismatch: $($dependency.Name)"
    }
    $path
}

$forbiddenImports = Get-ChildItem -LiteralPath (Join-Path $adapterRoot 'src') `
    -Recurse -Filter '*.java' | Select-String -Pattern 'org\.eclipse\..*\.internal'
if ($forbiddenImports) {
    throw 'Adapter source must not import internal Eclipse APIs.'
}
$forbiddenBoundary = Get-ChildItem -LiteralPath (Join-Path $adapterRoot 'src') `
    -Recurse -Filter '*.java' | Select-String -Pattern `
        'java\.(io|net)\.|java\.nio\.file\.|BundleActivator|addJobChangeListener|removeJobChangeListener'
if ($forbiddenBoundary) {
    throw 'Offline adapter source crossed its no-I/O, no-network, or no-registration boundary.'
}

& (Join-Path $componentRoot 'build.ps1') `
    -JavacPath $JavacPath -JavaPath $JavaPath -BuildLockHeld
if ($LASTEXITCODE -ne 0) {
    throw "Observer core validation failed with exit code $LASTEXITCODE"
}

$mainClasses = Join-Path $componentRoot 'build\main-classes'
$adapterMain = Join-Path $adapterRoot 'build\main-classes'
$adapterTest = Join-Path $adapterRoot 'build\test-classes'
$buildRoot = [IO.Path]::GetFullPath((Join-Path $adapterRoot 'build'))
$adapterRootFull = [IO.Path]::GetFullPath($adapterRoot)
$expectedBuildRoot = $adapterRootFull + [IO.Path]::DirectorySeparatorChar + 'build'
if (-not $buildRoot.Equals($expectedBuildRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean an unexpected adapter build directory: $buildRoot"
}
if (Test-Path -LiteralPath $buildRoot) {
    Remove-Item -LiteralPath $buildRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $adapterMain,$adapterTest | Out-Null

$mainSources = @(
    Get-ChildItem -LiteralPath (Join-Path $adapterRoot 'src') -Recurse -Filter '*.java'
) | Sort-Object FullName | Select-Object -ExpandProperty FullName
$testSources = @(
    Get-ChildItem -LiteralPath (Join-Path $adapterRoot 'test') -Recurse -Filter '*.java'
) | Sort-Object FullName | Select-Object -ExpandProperty FullName
if ($mainSources.Count -eq 0 -or $testSources.Count -eq 0) {
    throw 'Adapter main or test Java sources were not found.'
}

$dependencyClassPath = [string]::Join([IO.Path]::PathSeparator, $dependencyPaths)
$mainClassPath = $mainClasses + [IO.Path]::PathSeparator + $dependencyClassPath
& $JavacPath --release 17 -proc:none -Xlint:all -Werror `
    -cp $mainClassPath -d $adapterMain $mainSources
if ($LASTEXITCODE -ne 0) {
    throw "adapter main javac failed with exit code $LASTEXITCODE"
}

$testClassPath = [string]::Join(
    [IO.Path]::PathSeparator,
    @($mainClasses, $adapterMain, $dependencyClassPath))
& $JavacPath --release 17 -proc:none -Xlint:all -Werror `
    -cp $testClassPath -d $adapterTest $testSources
if ($LASTEXITCODE -ne 0) {
    throw "adapter test javac failed with exit code $LASTEXITCODE"
}

$runClassPath = $testClassPath + [IO.Path]::PathSeparator + $adapterTest
& $JavaPath -ea -cp $runClassPath `
    net.hrcautomation.jobobserver.EclipseJobsAdapterTest
if ($LASTEXITCODE -ne 0) {
    throw "EclipseJobsAdapterTest failed with exit code $LASTEXITCODE"
}

$forbiddenArtifacts = Get-ChildItem -LiteralPath $adapterMain -Recurse -File |
    Where-Object { $_.Name -match 'Test|Activator|MANIFEST|plugin\.xml' }
if ($forbiddenArtifacts) {
    throw 'Adapter main output contains a test, activator, or packaging artefact.'
}
} finally {
    if ($lockTaken) {
        $buildMutex.ReleaseMutex()
    }
    $buildMutex.Dispose()
}
