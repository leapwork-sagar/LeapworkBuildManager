$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$check = Join-Path $PSScriptRoot 'check-release.ps1'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('release-check-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path (Join-Path $fixture 'src/LeapworkBuildManager') -Force)
Copy-Item (Join-Path $root 'src/LeapworkBuildManager/VersionInfo.cs') (Join-Path $fixture 'src/LeapworkBuildManager/VersionInfo.cs')
Copy-Item (Join-Path $root 'CHANGELOG.md') (Join-Path $fixture 'CHANGELOG.md')
$number = [regex]::Match((Get-Content (Join-Path $fixture 'src/LeapworkBuildManager/VersionInfo.cs') -Raw), 'Number\s*=\s*"([^"]+)"').Groups[1].Value
$exe = Join-Path $fixture 'LeapworkBuildManager.exe'
Copy-Item (Join-Path $root 'artifacts/bin/LeapworkBuildManager.exe') $exe
$sum = Join-Path $fixture 'SHA256SUMS.txt'
((Get-FileHash $exe).Hash + '  LeapworkBuildManager.exe') | Set-Content $sum -Encoding ascii
function Expect-Failure([scriptblock]$Action) {
    $failed = $false
    try { & $Action } catch { $failed = $true }
    if (!$failed) { throw 'Invalid release was accepted.' }
}
& $check -ProjectRoot $fixture -Tag "v$number" -ExecutablePath $exe -ChecksumPath $sum
Expect-Failure { & $check -ProjectRoot $fixture -Tag 'v0.0.0.0' }
Expect-Failure { & $check -ProjectRoot $fixture -Tag "v$number" -ExecutablePath $exe }
Expect-Failure { & $check -ProjectRoot $fixture -Tag "v$number" -ExecutablePath $exe -ChecksumPath "$sum.missing" }
('0' * 64 + '  LeapworkBuildManager.exe') | Set-Content $sum -Encoding ascii
Expect-Failure { & $check -ProjectRoot $fixture -Tag "v$number" -ExecutablePath $exe -ChecksumPath $sum }
'## v0.0' | Set-Content (Join-Path $fixture 'CHANGELOG.md')
Expect-Failure { & $check -ProjectRoot $fixture -Tag "v$number" }
$otherVersion = '0.0.0.1'
('public const string Number = "' + $otherVersion + '";') | Set-Content (Join-Path $fixture 'src/LeapworkBuildManager/VersionInfo.cs')
"## v$otherVersion" | Set-Content (Join-Path $fixture 'CHANGELOG.md')
Expect-Failure { & $check -ProjectRoot $fixture -Tag "v$otherVersion" -ExecutablePath $exe -ChecksumPath $sum }
Write-Output 'Release checks: valid assets accepted; wrong tag, missing inputs/assets, corrupt checksum and stale changelog rejected.'
