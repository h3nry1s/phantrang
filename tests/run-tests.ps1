# Builds the app from the current sources, then the self-tests, and runs them. Exit code 0 = every test passed.
#   powershell -ExecutionPolicy Bypass -File tests\run-tests.ps1            # build + test
#   powershell -ExecutionPolicy Bypass -File tests\run-tests.ps1 -NoRun     # build only (e.g. to run the tests in Windows Sandbox)
param([switch]$NoRun)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$dist = Join-Path $root 'dist'
$out = Join-Path $dist 'tests'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }

# always test what is in src\ now, never an older build lying in dist\
& (Join-Path $root 'build.ps1')
if (-not $?) { throw 'Biên dịch ứng dụng lỗi' }

New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item (Join-Path $dist 'PhanTrang.exe') $out -Force    # the tests load the app's classes from here
& $csc /nologo /codepage:65001 /target:exe /platform:anycpu "/out:$out\PhanTrang.Tests.exe" `
    "/r:$out\PhanTrang.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Xml.dll /r:System.Xml.Linq.dll `
    (Join-Path $PSScriptRoot 'Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Biên dịch test lỗi' }
if ($NoRun) { Write-Host "Đã biên dịch: $out\PhanTrang.Tests.exe"; exit 0 }

& "$out\PhanTrang.Tests.exe"
exit $LASTEXITCODE
