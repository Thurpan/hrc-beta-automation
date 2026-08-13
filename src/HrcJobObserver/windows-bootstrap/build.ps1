[CmdletBinding()]
param(
    [string]$DotnetPath = 'C:\Program Files\dotnet\dotnet.exe',
    [string]$VisualCppRoot = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC\14.44.35207',
    [string]$WindowsSdkRoot = 'C:\Program Files (x86)\Windows Kits\10',
    [string]$WindowsSdkVersion = '10.0.26100.0'
)

$ErrorActionPreference = 'Stop'
$moduleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not (Test-Path -LiteralPath $DotnetPath -PathType Leaf)) {
    throw "Required .NET tool was not found: $DotnetPath"
}

function Invoke-ClosedNativeTool {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,
        [Parameter(Mandatory)]
        [string[]]$ArgumentList,
        [Parameter(Mandatory)]
        [string]$WorkingDirectory,
        [Parameter(Mandatory)]
        [string]$ToolPath,
        [Parameter(Mandatory)]
        [string]$TemporaryDirectory
    )

    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $FilePath
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.Environment.Clear()
    $start.Environment['SystemRoot'] = 'C:\Windows'
    $start.Environment['WINDIR'] = 'C:\Windows'
    $start.Environment['PATH'] = $ToolPath
    $start.Environment['TEMP'] = $TemporaryDirectory
    $start.Environment['TMP'] = $TemporaryDirectory
    foreach ($argument in $ArgumentList) {
        $start.ArgumentList.Add($argument)
    }

    $process = [Diagnostics.Process]::Start($start)
    if ($null -eq $process) {
        throw "Starting the pinned native tool failed: $FilePath"
    }

    try {
        $waitFailure = $null
        try {
            $exited = $process.WaitForExit(60000)
        } catch {
            $waitFailure = $_.Exception
            $exited = $false
        }
        if (-not $exited) {
            $primaryFailure = if ($null -ne $waitFailure) {
                $waitFailure
            } else {
                [TimeoutException]::new(
                    "The pinned native tool exceeded its 60-second limit: $FilePath")
            }
            $killFailure = $null
            try {
                $process.Kill($true)
            } catch {
                $killFailure = $_.Exception
            }
            $cleanupFailure = $null
            try {
                if (-not $process.WaitForExit(10000)) {
                    $cleanupFailure = [TimeoutException]::new(
                        "The failed native tool did not terminate within 10 seconds: $FilePath")
                }
            } catch {
                $cleanupFailure = $_.Exception
            }
            if ($null -ne $killFailure -or $null -ne $cleanupFailure) {
                $failures = [Collections.Generic.List[Exception]]::new()
                $failures.Add($primaryFailure)
                if ($null -ne $killFailure) {
                    $failures.Add($killFailure)
                }
                if ($null -ne $cleanupFailure) {
                    $failures.Add($cleanupFailure)
                }
                throw [AggregateException]::new(
                    "The pinned native tool failed and cleanup was indeterminate: $FilePath",
                    $failures)
            }
            throw $primaryFailure
        }
        return $process.ExitCode
    } finally {
        $process.Dispose()
    }
}

function Invoke-BoundedValidation {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,
        [Parameter(Mandatory)]
        [string[]]$ArgumentList,
        [Parameter(Mandatory)]
        [string]$WorkingDirectory,
        [Parameter(Mandatory)]
        [int]$TimeoutMilliseconds
    )

    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $FilePath
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    foreach ($argument in $ArgumentList) {
        $start.ArgumentList.Add($argument)
    }

    $process = [Diagnostics.Process]::Start($start)
    if ($null -eq $process) {
        throw "Starting the Windows bootstrap validation failed: $FilePath"
    }

    try {
        $waitFailure = $null
        try {
            $exited = $process.WaitForExit($TimeoutMilliseconds)
        } catch {
            $waitFailure = $_.Exception
            $exited = $false
        }
        if ($exited) {
            return $process.ExitCode
        }

        $primaryFailure = if ($null -ne $waitFailure) {
            $waitFailure
        } else {
            [TimeoutException]::new(
                "Windows bootstrap validation exceeded its process-level limit.")
        }
        $killFailure = $null
        try {
            $process.Kill($true)
        } catch {
            $killFailure = $_.Exception
        }

        $cleanupFailure = $null
        try {
            if (-not $process.WaitForExit(10000)) {
                $cleanupFailure = [TimeoutException]::new(
                    'The timed-out validation process tree did not terminate within 10 seconds.')
            }
        } catch {
            $cleanupFailure = $_.Exception
        }

        if ($null -ne $killFailure -or $null -ne $cleanupFailure) {
            $failures = [Collections.Generic.List[Exception]]::new()
            $failures.Add($primaryFailure)
            if ($null -ne $killFailure) {
                $failures.Add($killFailure)
            }
            if ($null -ne $cleanupFailure) {
                $failures.Add($cleanupFailure)
            }
            throw [AggregateException]::new(
                'Windows bootstrap validation timed out and cleanup was indeterminate.',
                $failures)
        }

        throw $primaryFailure
    } finally {
        $process.Dispose()
    }
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

    $nativeSourceRoot = Join-Path $moduleRoot 'native'
    $compiler = Join-Path $VisualCppRoot 'bin\Hostx64\x64\cl.exe'
    $linker = Join-Path $VisualCppRoot 'bin\Hostx64\x64\link.exe'
    $resourceCompiler = Join-Path $WindowsSdkRoot `
        "bin\$WindowsSdkVersion\x64\rc.exe"
    $manifestTool = Join-Path $WindowsSdkRoot `
        "bin\$WindowsSdkVersion\x64\mt.exe"
    $kernelLibrary = Join-Path $WindowsSdkRoot `
        "Lib\$WindowsSdkVersion\um\x64\kernel32.lib"
    $nativeTools = @(
        $compiler,
        $linker,
        $resourceCompiler,
        $manifestTool,
        $kernelLibrary)
    foreach ($tool in $nativeTools) {
        if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
            throw "Required pinned native tool input was not found: $tool"
        }
    }

    $nativeSource = Join-Path $nativeSourceRoot `
        'HrcJobObserver.NativeFixture.c'
    $nativeResource = Join-Path $nativeSourceRoot `
        'HrcJobObserver.NativeFixture.rc'
    $nativeManifest = Join-Path $nativeSourceRoot `
        'HrcJobObserver.NativeFixture.manifest'
    foreach ($input in @($nativeSource, $nativeResource, $nativeManifest)) {
        if (-not (Test-Path -LiteralPath $input -PathType Leaf)) {
            throw "Required native fixture input was not found: $input"
        }
    }

    $nativeText = Get-Content -LiteralPath $nativeSource -Raw
    $nativeGuardOptions =
        [Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
        [Text.RegularExpressions.RegexOptions]::CultureInvariant
    $nativeForbidden = [regex]::Match(
        $nativeText,
        'LoadLibrary|GetProcAddress|CreateFile|Reg(Open|Create|Query)|' +
        'WinHttp|WSA[A-Z]|socket\s*\(|Co(Create|Initialize)|' +
        'GetEnvironment|GetStdHandle|CreateProcess|ShellExecute|__asm|' +
        '#\s*include|malloc\s*\(|calloc\s*\(|realloc\s*\(|' +
        'free\s*\(|printf|puts\s*\(|fopen|system\s*\(',
        $nativeGuardOptions)
    $nativeImports = [regex]::Matches(
        $nativeText,
        '^NATIVE_IMPORT [^;]+;$',
        [Text.RegularExpressions.RegexOptions]::Multiline -bor
        [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if ($nativeForbidden.Success -or $nativeImports.Count -ne 3) {
        throw 'The native fixture source crossed its closed runtime boundary.'
    }

    $resourceText = Get-Content -LiteralPath $nativeResource -Raw
    if ($resourceText -notmatch `
            '(?s)\ALANGUAGE 0, 0\r?\n1 24 "HrcJobObserver\.NativeFixture\.manifest"\r?\n?\z') {
        throw 'The native fixture resource definition is not exact and neutral.'
    }
    $manifestText = Get-Content -LiteralPath $nativeManifest -Raw
    if ($manifestText -match '<dependency|<file\b' -or
        $manifestText -notmatch 'level="asInvoker" uiAccess="false"') {
        throw 'The native fixture manifest crossed its closed activation boundary.'
    }

    $manifestDocument = [Xml.XmlDocument]::new()
    $manifestDocument.PreserveWhitespace = $true
    $manifestDocument.XmlResolver = $null
    $manifestDocument.Load($nativeManifest)
    $manifestNamespaces = [Xml.XmlNamespaceManager]::new(
        $manifestDocument.NameTable)
    $manifestNamespaces.AddNamespace(
        'asmv1', 'urn:schemas-microsoft-com:asm.v1')
    $manifestNamespaces.AddNamespace(
        'asmv3', 'urn:schemas-microsoft-com:asm.v3')
    $manifestElements = @($manifestDocument.SelectNodes('//*'))
    $manifestIdentity = $manifestDocument.SelectSingleNode(
        '/asmv1:assembly/asmv1:assemblyIdentity', $manifestNamespaces)
    $executionLevel = $manifestDocument.SelectSingleNode(
        '/asmv1:assembly/asmv3:trustInfo/asmv3:security/' +
        'asmv3:requestedPrivileges/asmv3:requestedExecutionLevel',
        $manifestNamespaces)
    $expectedManifestElements = @(
        'assembly',
        'assemblyIdentity',
        'trustInfo',
        'security',
        'requestedPrivileges',
        'requestedExecutionLevel')
    $actualManifestElements = @($manifestElements | ForEach-Object {
            $_.LocalName
        })
    if ($null -eq $manifestIdentity -or $null -eq $executionLevel -or
        $actualManifestElements.Count -ne $expectedManifestElements.Count -or
        (Compare-Object $expectedManifestElements $actualManifestElements) -or
        $manifestIdentity.GetAttribute('name') -ne
            'HrcBetaAutomation.NativeFixture' -or
        $manifestIdentity.GetAttribute('processorArchitecture') -ne 'amd64' -or
        $manifestIdentity.GetAttribute('type') -ne 'win32' -or
        $manifestIdentity.GetAttribute('version') -ne '1.0.0.0' -or
        $manifestIdentity.Attributes.Count -ne 4 -or
        $executionLevel.GetAttribute('level') -ne 'asInvoker' -or
        $executionLevel.GetAttribute('uiAccess') -ne 'false' -or
        $executionLevel.Attributes.Count -ne 2) {
        throw 'The native fixture manifest XML topology is not exact.'
    }

    $closedToolPath = @(
        (Split-Path -Parent $compiler),
        (Split-Path -Parent $resourceCompiler),
        'C:\Windows\System32'
    ) -join [IO.Path]::PathSeparator
    $manifestValidationTemp = Join-Path $buildRoot 'manifest-validation-temp'
    New-Item -ItemType Directory -Path $manifestValidationTemp | Out-Null
    $exitCode = Invoke-ClosedNativeTool `
        -FilePath $manifestTool `
        -ArgumentList @('-nologo', '-manifest', $nativeManifest,
            '-validate_manifest') `
        -WorkingDirectory $nativeSourceRoot `
        -ToolPath $closedToolPath `
        -TemporaryDirectory $manifestValidationTemp
    if ($exitCode -ne 0) {
        throw "Native manifest validation failed with exit code $exitCode"
    }

    $nativeOutputs = @()
    foreach ($buildName in @('native-a', 'native-b')) {
        $nativeBuild = Join-Path $buildRoot $buildName
        New-Item -ItemType Directory -Path $nativeBuild | Out-Null
        $nativeToolTemp = Join-Path $nativeBuild 'temp'
        New-Item -ItemType Directory -Path $nativeToolTemp | Out-Null
        $object = Join-Path $nativeBuild 'HrcJobObserver.NativeFixture.obj'
        $resource = Join-Path $nativeBuild 'HrcJobObserver.NativeFixture.res'
        $executable = Join-Path $nativeBuild `
            'HrcJobObserver.NativeFixture.exe'

        $exitCode = Invoke-ClosedNativeTool `
            -FilePath $resourceCompiler `
            -ArgumentList @(
                '/nologo', '/l', '0x0000', '/fo', $resource, $nativeResource) `
            -WorkingDirectory $nativeSourceRoot `
            -ToolPath $closedToolPath `
            -TemporaryDirectory $nativeToolTemp
        if ($exitCode -ne 0) {
            throw "Native resource compilation failed with exit code $exitCode"
        }

        $exitCode = Invoke-ClosedNativeTool `
            -FilePath $compiler `
            -ArgumentList @(
                '/c', '/TC', '/nologo', '/W4', '/WX', '/O1', '/Ob1',
                '/Oi', '/Gy', '/Gw', '/GS', '/Zl', '/GL-',
                '/Brepro', "/Fo$object", $nativeSource) `
            -WorkingDirectory $nativeSourceRoot `
            -ToolPath $closedToolPath `
            -TemporaryDirectory $nativeToolTemp
        if ($exitCode -ne 0) {
            throw "Native fixture compilation failed with exit code $exitCode"
        }

        $exitCode = Invoke-ClosedNativeTool `
            -FilePath $linker `
            -ArgumentList @(
                '/NOLOGO', '/WX', '/NODEFAULTLIB',
                '/ENTRY:NativeRoleEntry', '/MACHINE:X64',
                '/SUBSYSTEM:WINDOWS,6.02', '/INCREMENTAL:NO', '/OPT:REF',
                '/OPT:ICF', '/DYNAMICBASE', '/HIGHENTROPYVA', '/NXCOMPAT',
                '/CETCOMPAT', '/DEPENDENTLOADFLAG:0x800',
                '/MANIFEST:NO', '/RELEASE', '/Brepro', "/OUT:$executable",
                $object, $resource, $kernelLibrary) `
            -WorkingDirectory $nativeBuild `
            -ToolPath $closedToolPath `
            -TemporaryDirectory $nativeToolTemp
        if ($exitCode -ne 0) {
            throw "Native fixture link failed with exit code $exitCode"
        }

        $exitCode = Invoke-ClosedNativeTool `
            -FilePath $executable `
            -ArgumentList @('--native-exit') `
            -WorkingDirectory $nativeBuild `
            -ToolPath $closedToolPath `
            -TemporaryDirectory $nativeToolTemp
        if ($exitCode -ne 0) {
            throw "Native fixture Exit role failed with exit code $exitCode"
        }

        $exitCode = Invoke-ClosedNativeTool `
            -FilePath $executable `
            -ArgumentList @('--native-invalid') `
            -WorkingDirectory $nativeBuild `
            -ToolPath $closedToolPath `
            -TemporaryDirectory $nativeToolTemp
        if ($exitCode -ne 87) {
            throw "Native fixture invalid role returned exit code $exitCode instead of 87"
        }

        $nativeOutputs += $executable
    }

    $firstNativeBytes = [IO.File]::ReadAllBytes($nativeOutputs[0])
    $secondNativeBytes = [IO.File]::ReadAllBytes($nativeOutputs[1])
    $nativeEqual = $firstNativeBytes.Length -eq $secondNativeBytes.Length
    for ($index = 0; $nativeEqual -and $index -lt $firstNativeBytes.Length;
            $index++) {
        $nativeEqual = $firstNativeBytes[$index] -eq $secondNativeBytes[$index]
    }
    if (-not $nativeEqual) {
        throw 'Independent native fixture builds were not byte-identical.'
    }

    $nativeFinalRoot = Join-Path $buildRoot 'native'
    New-Item -ItemType Directory -Path $nativeFinalRoot | Out-Null
    [IO.File]::WriteAllBytes(
        (Join-Path $nativeFinalRoot 'HrcJobObserver.NativeFixture.exe'),
        $firstNativeBytes)

    $sources = Get-ChildItem -LiteralPath @(
        (Join-Path $moduleRoot 'src'),
        (Join-Path $moduleRoot 'test')) -Recurse -Filter '*.cs'
    $productionSources = Get-ChildItem -LiteralPath `
        (Join-Path $moduleRoot 'src') -Recurse -Filter '*.cs'
    $forbidden = $sources | Select-String -Pattern `
        'System\.Net\.|HttpClient|Environment\.GetEnvironmentVariable|Console\.(Write|Error)|Microsoft\.Win32\.Registry|HRC Beta|HoldemResources'
    $productionLaunch = $productionSources | Select-String -Pattern `
        'ProcessStartInfo|Process\.Start'
    $nativeCreateProcessCalls = @($productionSources | Select-String -Pattern `
        'NativeMethods\.CreateProcess\(')
    $expectedNativeCreateProcessSources = @(
        [IO.Path]::GetFullPath((Join-Path $moduleRoot `
            'src\HrcJobObserver.WindowsBootstrap\ContainedHarnessProcess.cs')),
        [IO.Path]::GetFullPath((Join-Path $moduleRoot `
            'src\HrcJobObserver.WindowsBootstrap\ContainedAuditedNativeFixtureProcess.cs')))
    $nativeCreateProcessPerFileInvalid = $false
    foreach ($expectedSource in $expectedNativeCreateProcessSources) {
        $matchingCalls = @($nativeCreateProcessCalls | Where-Object {
            [IO.Path]::GetFullPath($_.Path).Equals(
                $expectedSource,
                [StringComparison]::OrdinalIgnoreCase)
        })
        if ($matchingCalls.Count -ne 1) {
            $nativeCreateProcessPerFileInvalid = $true
        }
    }
    $actualNativeCreateProcessSources = @(
        $nativeCreateProcessCalls | ForEach-Object {
            [IO.Path]::GetFullPath($_.Path)
        })
    $nativeMethodsContainmentSource = [IO.Path]::GetFullPath((Join-Path `
        $moduleRoot `
        'src\HrcJobObserver.WindowsBootstrap\NativeMethods.Containment.cs'))
    $nativeCreateProcessImports = @($productionSources | Select-String `
        -Pattern '^\s*EntryPoint = "CreateProcessW",$')
    $nativeCreateProcessDeclarations = @(Select-String -LiteralPath `
        $nativeMethodsContainmentSource -Pattern `
        'internal static unsafe partial int CreateProcess\(')
    $nativeCreateProcessShapeInvalid =
        $actualNativeCreateProcessSources.Count -ne 2 -or
        $nativeCreateProcessPerFileInvalid -or
        (Compare-Object $expectedNativeCreateProcessSources `
            $actualNativeCreateProcessSources) -or
        $nativeCreateProcessImports.Count -ne 1 -or
        -not [IO.Path]::GetFullPath(
            $nativeCreateProcessImports[0].Path).Equals(
                $nativeMethodsContainmentSource,
                [StringComparison]::OrdinalIgnoreCase) -or
        $nativeCreateProcessImports[0].Line.Trim() -ne `
            'EntryPoint = "CreateProcessW",' -or
        $nativeCreateProcessDeclarations.Count -ne 1
    $nativeFixtureTestSource = Join-Path $moduleRoot `
        'test\HrcJobObserver.WindowsBootstrap.TestHarness\Program.NativeFixture.cs'
    $testLaunch = $sources | Where-Object {
        $_.FullName -notlike '*\test\HrcJobObserver.WindowsBootstrap.TestHarness\Program.cs' -and
        -not $_.FullName.Equals(
            $nativeFixtureTestSource,
            [StringComparison]::OrdinalIgnoreCase)
    } | Select-String -Pattern 'ProcessStartInfo|Process\.Start'
    $nativeFixtureTestLaunches = Select-String -LiteralPath `
        $nativeFixtureTestSource -Pattern 'ProcessStartInfo|Process\.Start'
    $expectedNativeFixtureLaunchShape = @(
        'ProcessStartInfo start = new()',
        'process = Process.Start(start) ?? throw new InvalidOperationException(',
        'ProcessStartInfo start,')
    $actualNativeFixtureLaunchShape = @(
        $nativeFixtureTestLaunches.Line.Trim())
    if ($forbidden -or $productionLaunch -or
        $nativeCreateProcessShapeInvalid -or $testLaunch -or
        $actualNativeFixtureLaunchShape.Count -ne 3 -or
        (Compare-Object $expectedNativeFixtureLaunchShape `
            $actualNativeFixtureLaunchShape)) {
        throw 'Windows bootstrap source crossed its offline or data boundary.'
    }

    $project = Join-Path $moduleRoot `
        'HrcJobObserver.WindowsBootstrap.TestHarness.csproj'
    & $DotnetPath restore $project --configfile `
        (Join-Path $moduleRoot 'NuGet.Config')
    if ($LASTEXITCODE -ne 0) {
        throw "Windows bootstrap restore failed with exit code $LASTEXITCODE"
    }
    $validationExitCode = Invoke-BoundedValidation `
        -FilePath $DotnetPath `
        -ArgumentList @('run', '--project', $project, '-c', 'Release',
            '--no-restore') `
        -WorkingDirectory $moduleRoot `
        -TimeoutMilliseconds 180000
    if ($validationExitCode -ne 0) {
        throw "Windows bootstrap validation failed with exit code $validationExitCode"
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
