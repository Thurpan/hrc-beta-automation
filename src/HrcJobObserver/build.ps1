[CmdletBinding()]
param(
    [string]$JavacPath = 'C:\Program Files\Android\Android Studio\jbr\bin\javac.exe',
    [string]$JavaPath = 'C:\Program Files\Android\Android Studio\jbr\bin\java.exe',
    [switch]$BuildLockHeld
)

$ErrorActionPreference = 'Stop'
$buildMutex = $null
$lockTaken = $false
if (-not $BuildLockHeld) {
    $buildMutex = [Threading.Mutex]::new(
        $false, 'Local\HrcBetaAutomation-HrcJobObserver-Build-v1')
    try {
        $lockTaken = $buildMutex.WaitOne([TimeSpan]::FromMinutes(5))
    } catch [Threading.AbandonedMutexException] {
        $lockTaken = $true
    }
    if (-not $lockTaken) {
        $buildMutex.Dispose()
        throw 'Timed out waiting for the observer build lock.'
    }
}

try {
$componentRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$mainClasses = Join-Path $componentRoot 'build\main-classes'
$testClasses = Join-Path $componentRoot 'build\test-classes'

foreach ($tool in @($JavacPath, $JavaPath)) {
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
        throw "Required Java tool was not found: $tool"
    }
}

$mainSources = @(
    Get-ChildItem -LiteralPath (Join-Path $componentRoot 'src') -Recurse -Filter '*.java'
) | Sort-Object FullName | Select-Object -ExpandProperty FullName
$testSources = @(
    Get-ChildItem -LiteralPath (Join-Path $componentRoot 'test') -Recurse -Filter '*.java'
) | Sort-Object FullName | Select-Object -ExpandProperty FullName

if ($mainSources.Count -eq 0 -or $testSources.Count -eq 0) {
    throw 'Observer main or test Java sources were not found.'
}

$buildRoot = [IO.Path]::GetFullPath((Join-Path $componentRoot 'build'))
$mainClassesFull = [IO.Path]::GetFullPath($mainClasses)
$testClassesFull = [IO.Path]::GetFullPath($testClasses)
foreach ($output in @($mainClassesFull, $testClassesFull)) {
    $expectedPrefix = $buildRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $output.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean classes outside the observer build directory: $output"
    }
}
if (Test-Path -LiteralPath $buildRoot) {
    Remove-Item -LiteralPath $buildRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $mainClassesFull,$testClassesFull | Out-Null

& $JavacPath --release 17 -proc:none -Xlint:all -Werror `
    -d $mainClassesFull $mainSources
if ($LASTEXITCODE -ne 0) {
    throw "main javac failed with exit code $LASTEXITCODE"
}

& $JavacPath --release 17 -proc:none -Xlint:all -Werror `
    -cp $mainClassesFull -d $testClassesFull $testSources
if ($LASTEXITCODE -ne 0) {
    throw "test javac failed with exit code $LASTEXITCODE"
}

$classPath = $mainClassesFull + [IO.Path]::PathSeparator + $testClassesFull
& $JavaPath -ea -cp $classPath net.hrcautomation.jobobserver.ObserverCoreTest
if ($LASTEXITCODE -ne 0) {
    throw "ObserverCoreTest failed with exit code $LASTEXITCODE"
}
} finally {
    if ($lockTaken) {
        $buildMutex.ReleaseMutex()
        $buildMutex.Dispose()
    }
}
