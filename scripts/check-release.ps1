param(
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent),
    [Parameter(Mandatory = $true)][string]$Tag,
    [string]$ExecutablePath,
    [string]$ChecksumPath
)
$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $ProjectRoot 'src/LeapworkBuildManager/VersionInfo.cs') -Raw
$number = [regex]::Match($source, 'Number\s*=\s*"(\d+\.\d+\.\d+\.\d+)"').Groups[1].Value
if (!$number -or $Tag -cne "v$number") { throw 'Release tag must match the four-part VersionInfo.Number.' }
$version = [Version]$number
$changelog = Get-Content (Join-Path $ProjectRoot 'CHANGELOG.md') -Raw
$heading = [regex]::Match($changelog, '(?m)^##\s+v(\d+(?:\.\d+){1,3})\s*\r?$').Groups[1].Value
# Historical headings omit zero build/revision components, e.g. v1.22 for 1.22.0.0.
$parts = @($heading.Split('.'))
while ($parts.Count -lt 4) { $parts += '0' }
if (!$heading -or ($parts -join '.') -ne $number) { throw 'Latest changelog heading must match the release version.' }
if ([bool]$ExecutablePath -ne [bool]$ChecksumPath) { throw 'Provide both executable and checksum paths.' }
if ($ExecutablePath) {
    if (!(Test-Path -LiteralPath $ExecutablePath -PathType Leaf) -or !(Test-Path -LiteralPath $ChecksumPath -PathType Leaf)) {
        throw 'Required release asset is missing.'
    }
    if ((Split-Path $ExecutablePath -Leaf) -cne 'LeapworkBuildManager.exe') { throw 'Unexpected portable executable name.' }
    if ((Get-Item -LiteralPath $ExecutablePath).Length -eq 0) { throw 'Portable executable is empty.' }
    $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path $ExecutablePath).Path).FileVersion
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName((Resolve-Path $ExecutablePath).Path).Version
    if ([Version]$fileVersion -ne $version -or $assemblyVersion -ne $version) { throw 'EXE file/assembly version does not match source.' }
    $checksum = (Get-Content -LiteralPath $ChecksumPath -Raw).Trim()
    $match = [regex]::Match($checksum, '^([A-Fa-f0-9]{64})  LeapworkBuildManager\.exe$')
    if (!$match.Success -or $match.Groups[1].Value -ne (Get-FileHash -LiteralPath $ExecutablePath -Algorithm SHA256).Hash) {
        throw 'Checksum manifest does not match the portable executable.'
    }
}
Write-Output "Release consistency passed: $Tag"
