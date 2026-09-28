param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sourceRoot = Join-Path $projectRoot 'src\LeapworkBuildManager'
$outputRoot = Join-Path $projectRoot 'artifacts\bin'
[void](New-Item -ItemType Directory -Path $outputRoot -Force)
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$production = @(Get-ChildItem $sourceRoot -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } | ForEach-Object FullName)
$references = @('/reference:System.Net.Http.dll','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll')
$resources = @("/resource:$sourceRoot\Assets\logo.png,LeapworkLogo","/resource:$sourceRoot\Assets\app.ico,LeapworkIcon")
& $compiler /nologo /optimize+ /target:winexe "/win32icon:$sourceRoot\Assets\app.ico" "/win32manifest:$sourceRoot\app.manifest" @resources "/out:$outputRoot\LeapworkBuildManager.exe" @references @production
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $sourceRoot 'App.config') -Destination (Join-Path $outputRoot 'LeapworkBuildManager.exe.config') -Force
if ($SkipTests) { return }
$suites = @(
 @{File='Services\BuildServiceTests.cs';Main='BuildServiceTests'},
 @{File='UI\UiTests.cs';Main='UiTests'},
 @{File='UI\DiagnosticLayoutTests.cs';Main='DiagnosticLayoutTests'},
 @{File='UI\InteractionTests.cs';Main='InteractionTests'},
 @{File='UI\SavedInstallerTests.cs';Main='SavedInstallerTests'},
 @{File='Application\ControllerTests.cs';Main='ControllerTests'},
 @{File='Application\ReleaseTests.cs';Main='ReleaseTests'},
 @{File='UI\LifecycleTests.cs';Main='LifecycleTests'},
 @{File='Services\PreparationTests.cs';Main='PreparationTests'},
 @{File='Services\RecoveryTests.cs';Main='RecoveryTests'},
 @{File='Application\ManagerTests.cs';Main='ManagerTests'},
 @{File='Application\OptimizationTests.cs';Main='OptimizationTests'},
 @{File='Application\ValidationTests.cs';Main='ValidationTests'},
 @{File='Application\ReliabilityTests.cs';Main='ReliabilityTests'},
 @{File=@('Diagnostics\SecurityAndPersistenceTests.cs','Diagnostics\AttachmentChecks.cs','UI\DiagnosticLifecycleTests.cs');Main='HardeningTests'}
)
foreach ($suite in $suites) {
 $testPath = @($suite.File | ForEach-Object { Join-Path $projectRoot ('tests\' + $_) })
 $supportFiles = @(Get-ChildItem (Join-Path $projectRoot 'tests\Support') -Filter *.cs | ForEach-Object FullName)
 $executable = Join-Path $outputRoot ($suite.Main + '.exe')
 & $compiler /nologo /target:exe "/main:$($suite.Main)" @resources "/out:$executable" @references @production @testPath @supportFiles
 if ($LASTEXITCODE -ne 0) { throw "$($suite.Main) compilation failed." }
 & $executable
 if ($LASTEXITCODE -ne 0) { throw "$($suite.Main) failed." }
}
