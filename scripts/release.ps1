param([switch]$Overwrite)
$ErrorActionPreference = 'Stop'
function Publish-ReleaseFile([string]$Source, [string]$Destination) {
 if ($Overwrite) { Move-Item -LiteralPath $Source -Destination $Destination -Force }
 else { [IO.File]::Move($Source, $Destination) }
}
$projectRoot = Split-Path $PSScriptRoot -Parent
$sourceRoot = Join-Path $projectRoot 'src\LeapworkBuildManager'
$versionText = Get-Content (Join-Path $sourceRoot 'VersionInfo.cs') -Raw
$version = [Version]([regex]::Match($versionText, 'Number\s*=\s*"([^"]+)"').Groups[1].Value)
$releaseRoot = Join-Path $projectRoot 'releases'
[void](New-Item -ItemType Directory -Path $releaseRoot -Force)
$archivePath = Join-Path $releaseRoot ('LeapworkBuildManager-v' + $version.ToString(4) + '.zip')
$portable = Join-Path $releaseRoot ('v' + $version.ToString(4))
$sourceArchive = Join-Path $releaseRoot ('LeapworkBuildManager-source-v' + $version.ToString(4) + '.zip')
foreach ($existing in @($archivePath,$sourceArchive,$portable)) {
 if ((Test-Path -LiteralPath $existing) -and !$Overwrite) { throw "Release already exists: $existing. Increment the version or explicitly pass -Overwrite." }
}
& (Join-Path $PSScriptRoot 'build.ps1')
& (Join-Path $PSScriptRoot 'smoke-portable.ps1')
# A temporary archive is published only after successful compilation and tests.
$temporaryPath = $archivePath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($temporaryPath, [IO.Compression.ZipArchiveMode]::Create)
try {
 foreach ($name in @('LeapworkBuildManager.exe')) {
  [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $projectRoot ('artifacts\bin\' + $name)), $name)
 }
 [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $projectRoot 'README.md'), 'README.md')
 foreach ($directory in @('src','tests','scripts','docs','.github')) {
  foreach ($file in Get-ChildItem (Join-Path $projectRoot $directory) -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }) {
   $relative = $file.FullName.Substring($projectRoot.Length + 1).Replace('\','/')
   [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, 'source/' + $relative)
  }
 }
 foreach ($name in @('README.md','.gitignore','.editorconfig','.gitattributes','CONTRIBUTING.md','CHANGELOG.md','LeapworkBuildManager.sln')) {
  [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $projectRoot $name), 'source/' + $name)
 }
} finally { $archive.Dispose() }
$verify = [IO.Compression.ZipFile]::OpenRead($temporaryPath)
try {
 foreach ($required in @('LeapworkBuildManager.exe','README.md','source/scripts/build.ps1','source/.github/workflows/windows.yml')) {
  if (!$verify.GetEntry($required)) { throw "Missing archive entry: $required" }
 }
 if (@($verify.Entries.FullName) -match '/(bin|obj|artifacts|releases)/') { throw 'Generated files in source archive' }
 $stream = $verify.GetEntry('LeapworkBuildManager.exe').Open()
 $hash = [Security.Cryptography.SHA256]::Create()
 try { $packedHash = [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','') }
 finally { $stream.Dispose(); $hash.Dispose() }
 if ($packedHash -ne (Get-FileHash (Join-Path $projectRoot 'artifacts/bin/LeapworkBuildManager.exe')).Hash) { throw 'Archive executable mismatch' }
} finally { $verify.Dispose() }
Publish-ReleaseFile $temporaryPath $archivePath
$portable = Join-Path $releaseRoot ('v' + $version.ToString(4))
[void](New-Item -ItemType Directory -Path $portable -Force)
[IO.File]::Copy((Join-Path $projectRoot 'artifacts/bin/LeapworkBuildManager.exe'), (Join-Path $portable 'LeapworkBuildManager.exe'), [bool]$Overwrite)
$sourceArchive = Join-Path $releaseRoot ('LeapworkBuildManager-source-v' + $version.ToString(4) + '.zip')
$sourceTemporary = $sourceArchive + "." + [Guid]::NewGuid().ToString("N") + ".tmp"
$sourceZip = [IO.Compression.ZipFile]::Open($sourceTemporary, [IO.Compression.ZipArchiveMode]::Create)
try {
 foreach($directory in @('src','tests','scripts','docs','.github')) {
  foreach($file in Get-ChildItem (Join-Path $projectRoot $directory) -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }) {
   $relative = $file.FullName.Substring($projectRoot.Length + 1).Replace('\','/')
   [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($sourceZip,$file.FullName,$relative)
  }
 }
 foreach($name in @('README.md','.gitignore','.editorconfig','.gitattributes','CONTRIBUTING.md','CHANGELOG.md','LeapworkBuildManager.sln')) {
  [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($sourceZip,(Join-Path $projectRoot $name),$name)
 }
} finally { $sourceZip.Dispose() }
Publish-ReleaseFile $sourceTemporary $sourceArchive
Get-FileHash (Join-Path $projectRoot 'artifacts\bin\LeapworkBuildManager.exe') -Algorithm SHA256
Write-Output "Release created: $archivePath"
