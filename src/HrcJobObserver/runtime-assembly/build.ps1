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

    $runtimeRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $componentRoot = Split-Path -Parent $runtimeRoot
    $adapterRoot = Join-Path $componentRoot 'eclipse-adapter'
    $transportRoot = Join-Path $componentRoot 'local-transport'
    foreach ($tool in @($JavacPath, $JavaPath)) {
        if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
            throw "Required Java tool was not found: $tool"
        }
    }

    & (Join-Path $componentRoot 'build.ps1') `
        -JavacPath $JavacPath -JavaPath $JavaPath -BuildLockHeld
    if ($LASTEXITCODE -ne 0) {
        throw "Observer core validation failed with exit code $LASTEXITCODE"
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

    $coreMain = Join-Path $componentRoot 'build\main-classes'
    $adapterMain = Join-Path $adapterRoot 'build\main-classes'
    $transportMain = Join-Path $transportRoot 'build\main-classes'
    $runtimeMain = Join-Path $runtimeRoot 'build\main-classes'
    $runtimeTest = Join-Path $runtimeRoot 'build\test-classes'
    $buildRoot = [IO.Path]::GetFullPath((Join-Path $runtimeRoot 'build'))
    $expectedRoot = [IO.Path]::GetFullPath($runtimeRoot) +
        [IO.Path]::DirectorySeparatorChar + 'build'
    if (-not $buildRoot.Equals(
            $expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean an unexpected runtime build directory: $buildRoot"
    }
    if (Test-Path -LiteralPath $buildRoot) {
        Remove-Item -LiteralPath $buildRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path `
        $adapterMain,$transportMain,$runtimeMain,$runtimeTest | Out-Null

    $adapterSources = @(Get-ChildItem -LiteralPath (Join-Path $adapterRoot 'src') `
        -Recurse -Filter '*.java') | Sort-Object FullName |
        Select-Object -ExpandProperty FullName
    $transportSources = @(Get-ChildItem -LiteralPath (Join-Path $transportRoot 'src') `
        -Recurse -Filter '*.java') | Sort-Object FullName |
        Select-Object -ExpandProperty FullName
    $runtimeSources = @(Get-ChildItem -LiteralPath (Join-Path $runtimeRoot 'src') `
        -Recurse -Filter '*.java') | Sort-Object FullName |
        Select-Object -ExpandProperty FullName
    $testSources = @(Get-ChildItem -LiteralPath (Join-Path $runtimeRoot 'test') `
        -Recurse -Filter '*.java') | Sort-Object FullName |
        Select-Object -ExpandProperty FullName
    if ($adapterSources.Count -eq 0 -or $transportSources.Count -eq 0 `
            -or $runtimeSources.Count -eq 0 -or $testSources.Count -eq 0) {
        throw 'Runtime assembly sources were not found.'
    }

    $dependencyClassPath = [string]::Join(
        [IO.Path]::PathSeparator, $dependencyPaths)
    & $JavacPath --release 17 -proc:none -Xlint:all -Werror `
        -cp ($coreMain + [IO.Path]::PathSeparator + $dependencyClassPath) `
        -d $adapterMain $adapterSources
    if ($LASTEXITCODE -ne 0) {
        throw "adapter main javac failed with exit code $LASTEXITCODE"
    }
    & $JavacPath --release 17 -proc:none -Xlint:all -Werror `
        -cp $coreMain -d $transportMain $transportSources
    if ($LASTEXITCODE -ne 0) {
        throw "transport main javac failed with exit code $LASTEXITCODE"
    }

    $runtimeClassPath = [string]::Join(
        [IO.Path]::PathSeparator,
        @($coreMain, $adapterMain, $transportMain, $dependencyClassPath))
    & $JavacPath --release 17 -proc:none -Xlint:all -Werror `
        -cp $runtimeClassPath -d $runtimeMain $runtimeSources
    if ($LASTEXITCODE -ne 0) {
        throw "runtime main javac failed with exit code $LASTEXITCODE"
    }
    $testClassPath = $runtimeClassPath + [IO.Path]::PathSeparator + $runtimeMain
    & $JavacPath --release 17 -proc:none -Xlint:all -Werror `
        -cp $testClassPath -d $runtimeTest $testSources
    if ($LASTEXITCODE -ne 0) {
        throw "runtime test javac failed with exit code $LASTEXITCODE"
    }
    & $JavaPath -ea `
        -cp ($testClassPath + [IO.Path]::PathSeparator + $runtimeTest) `
        net.hrcautomation.jobobserver.ObserverRuntimeAssemblyTest
    if ($LASTEXITCODE -ne 0) {
        throw "ObserverRuntimeAssemblyTest failed with exit code $LASTEXITCODE"
    }

    $forbiddenArtifacts = Get-ChildItem -LiteralPath $runtimeMain -Recurse -File |
        Where-Object { $_.Name -match 'Test|Activator|MANIFEST|plugin\.xml' }
    if ($forbiddenArtifacts) {
        throw 'Runtime main output contains a test, activator, or packaging artefact.'
    }
} finally {
    if ($lockTaken) {
        $buildMutex.ReleaseMutex()
    }
    $buildMutex.Dispose()
}
