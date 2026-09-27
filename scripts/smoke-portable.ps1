param([string]$Executable)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (!$Executable) { $Executable = Join-Path $projectRoot 'artifacts/bin/LeapworkBuildManager.exe' }
$runRoot = Join-Path $projectRoot ('artifacts/portable-smoke/' + [Guid]::NewGuid().ToString('N'))
$portableRoot = Join-Path $runRoot 'app'
[void](New-Item -ItemType Directory -Path $portableRoot -Force)
Copy-Item -LiteralPath $Executable -Destination (Join-Path $portableRoot 'LeapworkBuildManager.exe')
$probeSource = Join-Path $runRoot 'Probe.cs'
@"
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
static class Probe {
 [STAThread] static int Main(string[] args) {
  try {
   Application.EnableVisualStyles();
   var assembly=Assembly.LoadFrom(args[0]);
   if(assembly.GetManifestResourceStream("LeapworkLogo")==null || assembly.GetManifestResourceStream("LeapworkIcon")==null)throw new Exception("Missing embedded assets");
   using(var form=(Form)Activator.CreateInstance(assembly.GetType("LeapworkBuildManager.MainForm"),new object[]{args[1]})) {
    form.Show();var time=Stopwatch.StartNew();
    var ready=form.GetType().GetField("preferencesLoaded",BindingFlags.Instance|BindingFlags.NonPublic);
    while(!(bool)ready.GetValue(form)){Application.DoEvents();Thread.Sleep(5);if(time.ElapsedMilliseconds>5000)throw new Exception("Startup timed out");}
    if(!form.Visible || form.Icon==null)throw new Exception("Form did not initialize");
    form.Close();time.Restart();while(!form.IsDisposed){Application.DoEvents();Thread.Sleep(5);if(time.ElapsedMilliseconds>5000)throw new Exception("Close timed out");}
   }
   Console.WriteLine("Portable form startup, embedded assets and shutdown passed without an adjacent config or DLL.");return 0;
  }catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
}
"@ | Set-Content -LiteralPath $probeSource
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$probe = Join-Path $runRoot 'Probe.exe'
& $compiler /nologo /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$probe" $probeSource
if ($LASTEXITCODE -ne 0) { throw 'Smoke probe compilation failed' }
& $probe (Join-Path $portableRoot 'LeapworkBuildManager.exe') (Join-Path $runRoot 'settings/preferences.xml')
if ($LASTEXITCODE -ne 0) { throw 'Portable smoke test failed' }
if (@(Get-ChildItem -LiteralPath $portableRoot -File).Count -ne 1) { throw 'Portable directory contains a dependency' }
