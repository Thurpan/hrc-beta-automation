[CmdletBinding()]
param(
    [string]$HrcInstallPath =
        'C:\Users\euanh\AppData\Local\Programs\HoldemResources\HRC Beta',
    [string]$JavacPath =
        'C:\Program Files\Android\Android Studio\jbr\bin\javac.exe',
    [string]$JavaPath =
        'C:\Program Files\Android\Android Studio\jbr\bin\java.exe',
    [string]$JarPath =
        'C:\Program Files\Android\Android Studio\jbr\bin\jar.exe'
)

$ErrorActionPreference = 'Stop'
$moduleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildMutex = [Threading.Mutex]::new(
    $false, 'Local\HrcBetaAutomation-HrcJobObserver-Build-v1')
$lockTaken = $false
$buildRoot = $null
try {
    try {
        $lockTaken = $buildMutex.WaitOne([TimeSpan]::FromMinutes(5))
    } catch [Threading.AbandonedMutexException] {
        $lockTaken = $true
    }
    if (-not $lockTaken) {
        throw 'Timed out waiting for the observer build lock.'
    }

    foreach ($tool in @($JavacPath, $JavaPath, $JarPath)) {
        if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
            throw "Required Java tool was not found: $tool"
        }
    }

    $plugins = Join-Path $HrcInstallPath 'plugins'
    $dependencies = @(
        @{
            Key = 'Jobs'
            Name = 'org.eclipse.core.jobs_3.15.500.v20250204-0817.jar'
            Hash = '189199CD46A284220B7B97FD59218B533FE9FD8E0AD22258F674A3F2DF4DE7C9'
        }
        @{
            Key = 'Common'
            Name = 'org.eclipse.equinox.common_3.20.0.v20250129-1348.jar'
            Hash = '617C5D7E759276B7E9ED363C56A6714B7F21D4A812D533FCB90E48723CC4C001'
        }
        @{
            Key = 'Osgi'
            Name = 'org.eclipse.osgi_3.23.0.v20250228-0640.jar'
            Hash = '1AC113541A19F0C72C0421FB24058DEFCA7E3C6F282E5EE73F14D2768A9AE653'
        }
        @{
            Key = 'CoreRuntime'
            Name = 'org.eclipse.core.runtime_3.33.0.v20250206-0919.jar'
            Hash = 'FF59EFB6FB7D610D819D44777BD306860EC7926CD31AC95419E729EDFB38CC02'
        }
        @{
            Key = 'ContentType'
            Name = 'org.eclipse.core.contenttype_3.9.600.v20241001-1711.jar'
            Hash = 'D8A2974F5EC3D7CFB8E3E177AA7303BABED0A1565DBE5416084A751044255002'
        }
        @{
            Key = 'App'
            Name = 'org.eclipse.equinox.app_1.7.300.v20250130-0528.jar'
            Hash = 'CA5D75F9228510F19250EF947E340A7A2CDEBD1A888EFDF13A3F3A4B114D4D2E'
        }
        @{
            Key = 'Preferences'
            Name = 'org.eclipse.equinox.preferences_3.11.300.v20250130-0533.jar'
            Hash = '7F8B452EE5F9D836DB8534C6BD1A29A2662352D868FF94856B6B54BC8032A999'
        }
        @{
            Key = 'Registry'
            Name = 'org.eclipse.equinox.registry_3.12.300.v20250129-1129.jar'
            Hash = 'E2145418FF639B44FF50E83B66848F40AE38C869DB6B8F95044BBB5D0D652722'
        }
        @{
            Key = 'PrefsService'
            Name = 'org.osgi.service.prefs_1.1.2.202109301733.jar'
            Hash = '43C7C870710E363405D422DA653CCE0D798A4537F76E4930F79BCEADD3A55345'
        }
    )
    $dependencyPaths = @{}
    foreach ($dependency in $dependencies) {
        $path = Join-Path $plugins $dependency.Name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required Eclipse dependency was not found: $path"
        }
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash `
                -cne $dependency.Hash) {
            throw "Eclipse dependency hash mismatch: $($dependency.Name)"
        }
        $dependencyPaths[$dependency.Key] = $path
    }

    $sourceRoot = Join-Path $moduleRoot 'src'
    $testRoot = Join-Path $moduleRoot 'test'
    $observerSourceRoot = Join-Path $moduleRoot 'bundle-src\observer'
    $producerSourceRoot = Join-Path $moduleRoot 'bundle-src\producer'
    $mainSources = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse `
        -Filter '*.java' | Sort-Object FullName |
        Select-Object -ExpandProperty FullName)
    $testSources = @(Get-ChildItem -LiteralPath $testRoot -Recurse `
        -Filter '*.java' | Sort-Object FullName |
        Select-Object -ExpandProperty FullName)
    $observerSources = @(Get-ChildItem -LiteralPath $observerSourceRoot -Recurse `
        -Filter '*.java' | Sort-Object FullName |
        Select-Object -ExpandProperty FullName)
    $producerSources = @(Get-ChildItem -LiteralPath $producerSourceRoot -Recurse `
        -Filter '*.java' | Sort-Object FullName |
        Select-Object -ExpandProperty FullName)
    if ($mainSources.Count -eq 0 -or $testSources.Count -eq 0 `
            -or $observerSources.Count -eq 0 -or $producerSources.Count -eq 0) {
        throw 'Fixture main, test, observer, or producer Java sources were not found.'
    }

    $forbiddenInternal = Get-ChildItem -LiteralPath $moduleRoot -Recurse `
        -Filter '*.java' | Select-String -Pattern 'org\.eclipse\..*\.internal'
    if ($forbiddenInternal) {
        throw 'Fixture source must not import internal Eclipse APIs.'
    }
    $forbiddenSourceArtefacts = Get-ChildItem -LiteralPath $moduleRoot -Recurse `
        -File | Where-Object {
            $_.Name -match '^(MANIFEST\.MF|plugin\.xml)$' `
                -or $_.Extension -in @('.jar', '.class')
        }
    if ($forbiddenSourceArtefacts) {
        throw 'Fixture source contains a generated or deployable artefact.'
    }

    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $buildRoot = [IO.Path]::GetFullPath((Join-Path $temporaryRoot (
        'hrc-job-observer-startlevel-fixture-' + [Guid]::NewGuid().ToString('N'))))
    $requiredPrefix = $temporaryRoot + [IO.Path]::DirectorySeparatorChar +
        'hrc-job-observer-startlevel-fixture-'
    if (-not $buildRoot.StartsWith(
            $requiredPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to use an unexpected start-level fixture directory.'
    }

    $mainClasses = Join-Path $buildRoot 'main-classes'
    $testClasses = Join-Path $buildRoot 'test-classes'
    $observerClasses = Join-Path $buildRoot 'observer-classes'
    $producerClasses = Join-Path $buildRoot 'producer-classes'
    $bundleRoot = Join-Path $buildRoot 'bundles'
    New-Item -ItemType Directory -Path `
        $mainClasses,$testClasses,$observerClasses,$producerClasses,$bundleRoot |
        Out-Null

    $providerClassPath = [string]::Join(
        [IO.Path]::PathSeparator,
        @(
            $dependencyPaths.Jobs,
            $dependencyPaths.Common,
            $dependencyPaths.Osgi
        ))
    & $JavacPath --release 17 -proc:none -Xlint:all -Werror `
        -encoding UTF-8 -cp $providerClassPath -d $mainClasses $mainSources
    if ($LASTEXITCODE -ne 0) {
        throw "start-level fixture main javac failed with exit code $LASTEXITCODE"
    }

    $testClassPath = $providerClassPath + [IO.Path]::PathSeparator + $mainClasses
    & $JavacPath --release 17 -proc:none -Xlint:all -Werror `
        -encoding UTF-8 -cp $testClassPath -d $testClasses $testSources
    if ($LASTEXITCODE -ne 0) {
        throw "start-level fixture test javac failed with exit code $LASTEXITCODE"
    }
    & $JavacPath --release 17 -proc:none -Xlint:all -Werror `
        -encoding UTF-8 -cp $testClassPath -d $observerClasses $observerSources
    if ($LASTEXITCODE -ne 0) {
        throw "start-level fixture observer javac failed with exit code $LASTEXITCODE"
    }
    & $JavacPath --release 17 -proc:none -Xlint:all -Werror `
        -encoding UTF-8 -cp $testClassPath -d $producerClasses $producerSources
    if ($LASTEXITCODE -ne 0) {
        throw "start-level fixture producer javac failed with exit code $LASTEXITCODE"
    }

    $observerManifest = Join-Path $buildRoot 'observer-manifest.mf'
    $producerManifest = Join-Path $buildRoot 'producer-manifest.mf'
    $utf8NoBom = [Text.UTF8Encoding]::new($false)
    $observerManifestLines = @(
        'Manifest-Version: 1.0',
        'Bundle-ManifestVersion: 2',
        'Bundle-SymbolicName: net.hrcautomation.jobobserver.startlevelfixture.observer',
        'Bundle-Version: 0.0.1',
        'Bundle-Activator: net.hrcautomation.jobobserver.startlevelfixture.observer.ObserverActivator',
        'Bundle-RequiredExecutionEnvironment: JavaSE-17',
        'Import-Package: net.hrcautomation.jobobserver.startlevelfixture,org.eclipse.core.runtime.jobs,org.osgi.framework,org.osgi.framework.startlevel',
        ''
    ) -join "`r`n"
    $producerManifestLines = @(
        'Manifest-Version: 1.0',
        'Bundle-ManifestVersion: 2',
        'Bundle-SymbolicName: net.hrcautomation.jobobserver.startlevelfixture.producer',
        'Bundle-Version: 0.0.1',
        'Bundle-Activator: net.hrcautomation.jobobserver.startlevelfixture.producer.ProducerActivator',
        'Bundle-RequiredExecutionEnvironment: JavaSE-17',
        'Import-Package: net.hrcautomation.jobobserver.startlevelfixture,org.eclipse.core.runtime;common=split,org.eclipse.core.runtime.jobs,org.osgi.framework,org.osgi.framework.startlevel',
        ''
    ) -join "`r`n"
    [IO.File]::WriteAllText($observerManifest, $observerManifestLines, $utf8NoBom)
    [IO.File]::WriteAllText($producerManifest, $producerManifestLines, $utf8NoBom)

    $observerBundle = Join-Path $bundleRoot 'fixture-observer.jar'
    $producerBundle = Join-Path $bundleRoot 'fixture-producer.jar'
    & $JarPath --create --file $observerBundle --manifest $observerManifest `
        -C $observerClasses .
    if ($LASTEXITCODE -ne 0) {
        throw "observer test Bundle creation failed with exit code $LASTEXITCODE"
    }
    & $JarPath --create --file $producerBundle --manifest $producerManifest `
        -C $producerClasses .
    if ($LASTEXITCODE -ne 0) {
        throw "producer test Bundle creation failed with exit code $LASTEXITCODE"
    }

    $runtimeClassPath = [string]::Join(
        [IO.Path]::PathSeparator,
        @($mainClasses, $testClasses, $dependencyPaths.Osgi))
    foreach ($scenario in @(
            'prerequisite-success',
            'recorded-provider-rows',
            'observer-failure')) {
        $storage = Join-Path $buildRoot ('storage-' + $scenario)
        & $JavaPath -ea -cp $runtimeClassPath `
            net.hrcautomation.jobobserver.startlevelfixture.EquinoxStartLevelFixtureTest `
            $scenario $storage $dependencyPaths.Common $dependencyPaths.Jobs `
            $dependencyPaths.CoreRuntime $dependencyPaths.ContentType `
            $dependencyPaths.App $dependencyPaths.Preferences `
            $dependencyPaths.Registry $dependencyPaths.PrefsService `
            $observerBundle $producerBundle
        if ($LASTEXITCODE -ne 0) {
            throw "start-level fixture $scenario failed with exit code $LASTEXITCODE"
        }
    }

    $forbiddenRepositoryArtefacts = Get-ChildItem -LiteralPath $moduleRoot `
        -Recurse -File | Where-Object {
            $_.Name -match '^(MANIFEST\.MF|plugin\.xml)$' `
                -or $_.Extension -in @('.jar', '.class')
        }
    if ($forbiddenRepositoryArtefacts) {
        throw 'Fixture left a generated or deployable artefact in the repository.'
    }
} finally {
    if ($null -ne $buildRoot -and (Test-Path -LiteralPath $buildRoot)) {
        $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd(
            [IO.Path]::DirectorySeparatorChar,
            [IO.Path]::AltDirectorySeparatorChar)
        $requiredPrefix = $temporaryRoot + [IO.Path]::DirectorySeparatorChar +
            'hrc-job-observer-startlevel-fixture-'
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
