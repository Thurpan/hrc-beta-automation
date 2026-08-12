[CmdletBinding()]
param(
    [string]$JavacPath = 'C:\Program Files\Android\Android Studio\jbr\bin\javac.exe',
    [string]$JavaPath = 'C:\Program Files\Android\Android Studio\jbr\bin\java.exe'
)

$ErrorActionPreference = 'Stop'
$componentRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$classes = Join-Path $componentRoot 'build\classes'

foreach ($tool in @($JavacPath, $JavaPath)) {
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
        throw "Required Java tool was not found: $tool"
    }
}

$sources = @(
    Get-ChildItem -LiteralPath (Join-Path $componentRoot 'src') -Recurse -Filter '*.java'
    Get-ChildItem -LiteralPath (Join-Path $componentRoot 'test') -Recurse -Filter '*.java'
) | Sort-Object FullName | Select-Object -ExpandProperty FullName

if ($sources.Count -eq 0) {
    throw 'No observer Java sources were found.'
}

$buildRoot = [IO.Path]::GetFullPath((Join-Path $componentRoot 'build'))
$classesFull = [IO.Path]::GetFullPath($classes)
$expectedPrefix = $buildRoot + [IO.Path]::DirectorySeparatorChar
if (-not $classesFull.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean classes outside the observer build directory: $classesFull"
}
if (Test-Path -LiteralPath $classesFull) {
    Remove-Item -LiteralPath $classesFull -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $classesFull | Out-Null

& $JavacPath --release 17 -Xlint:all -Werror -d $classesFull $sources
if ($LASTEXITCODE -ne 0) {
    throw "javac failed with exit code $LASTEXITCODE"
}

& $JavaPath -ea -cp $classesFull net.hrcautomation.jobobserver.ObserverCoreTest
if ($LASTEXITCODE -ne 0) {
    throw "ObserverCoreTest failed with exit code $LASTEXITCODE"
}
