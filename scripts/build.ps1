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
    @{File='UI\DisplayScaleTests.cs';Main='DisplayScaleTests'},
    @{File='UI\VisualChecks.cs';Main='VisualChecks'},
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
$resultsRoot = Join-Path $projectRoot 'artifacts/test-results'
[void](New-Item -ItemType Directory -Path $resultsRoot -Force)
$runRoot = Join-Path $resultsRoot ([Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $runRoot -Force)
$results = @()
$previousSuite = $env:TEST_SUITE
$previousArtifactDirectory = $env:TEST_ARTIFACT_DIR
try {
    foreach ($suite in $suites) {
        $name = $suite.Main
        $suiteRoot = Join-Path $runRoot $name
        [void](New-Item -ItemType Directory -Path $suiteRoot -Force)
        $env:TEST_SUITE = $name
        $env:TEST_ARTIFACT_DIR = $suiteRoot
        $testPath = @($suite.File | ForEach-Object { Join-Path $projectRoot ('tests\' + $_) })
        $supportFiles = @(Get-ChildItem (Join-Path $projectRoot 'tests\Support') -Filter *.cs | ForEach-Object FullName)
        $executable = Join-Path $outputRoot ($name + '.exe')
        $watch = [Diagnostics.Stopwatch]::StartNew()
        & $compiler /nologo /target:exe /main:TestRunner @resources "/out:$executable" @references @production @testPath @supportFiles 2>&1 | Tee-Object -FilePath (Join-Path $suiteRoot 'compile.log')
        $code = $LASTEXITCODE
        if ($code -eq 0) {
            & $executable 2>&1 | Tee-Object -FilePath (Join-Path $suiteRoot 'test.log')
            $code = $LASTEXITCODE
        }
        $results += [pscustomobject]@{ Suite = $name; Passed = ($code -eq 0); ExitCode = $code; Seconds = [Math]::Round($watch.Elapsed.TotalSeconds, 2) }
    }
} finally {
    $env:TEST_SUITE = $previousSuite
    $env:TEST_ARTIFACT_DIR = $previousArtifactDirectory
    $results | ConvertTo-Json | Set-Content (Join-Path $runRoot 'results.json') -Encoding utf8
    $summary = @('# Regression test results', '', '| Suite | Result | Seconds |', '| --- | --- | --- |')
    foreach ($result in $results) {
        $outcome = if ($result.Passed) { 'PASS' } else { 'FAIL' }
        $summary += "| $($result.Suite) | $outcome | $($result.Seconds) |"
    }
    $summary | Set-Content (Join-Path $runRoot 'summary.md') -Encoding utf8
    if ($env:GITHUB_STEP_SUMMARY) { $summary | Add-Content $env:GITHUB_STEP_SUMMARY -Encoding utf8 }
}
if (@($results | Where-Object { !$_.Passed }).Count -gt 0) { throw "Regression checks failed. See $runRoot" }
