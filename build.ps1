# Builds dist\PhanTrang.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
# No Visual Studio or .NET SDK needed. Also makes dist\PhanTrang-Windows.zip for the website.
#   -OutDir <folder>   build somewhere else (e.g. for the Store package) and leave dist\ and a running copy alone
param([string]$OutDir)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = if ($OutDir) { $OutDir } else { Join-Path $root 'dist' }
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }

New-Item -ItemType Directory -Force $dist | Out-Null
if (-not $OutDir) {
    $stop = Join-Path $root 'tools\stop-app.ps1'   # maintainer-only helper, not in the public repository
    if (Test-Path $stop) { & $stop; Start-Sleep -Milliseconds 300 }   # ask a running copy to close properly; never leave the pointer hidden
}

# App icon: the chalk-on-green-board mark, drawn here and packed as PNG-in-ICO (256, 48, 32, 16).
$ico = Join-Path $root 'build\app.ico'
New-Item -ItemType Directory -Force (Split-Path $ico) | Out-Null
Add-Type -AssemblyName System.Drawing
function Draw-Logo([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.ScaleTransform($size / 96.0, $size / 96.0)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(4, 4, 52, 52, 180, 90); $path.AddArc(40, 4, 52, 52, 270, 90); $path.AddArc(40, 40, 52, 52, 0, 90); $path.AddArc(4, 40, 52, 52, 90, 90); $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(30, 74, 59))), $path)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(190, 244, 241, 230)), 4
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $g.DrawBezier($pen, 18, 74, 30, 64, 42, 82, 54, 72)
    $g.TranslateTransform(50, 44); $g.RotateTransform(-35)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(244, 241, 230))), -28, -10, 56, 20)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)), -24, -10, 50, 6)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(226, 218, 202))), -28, -9, 11, 18)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    return ,$ms.ToArray()
}
$sizes = 256, 48, 32, 16
$pngs = $sizes | ForEach-Object { ,(Draw-Logo $_) }
$w = New-Object System.IO.BinaryWriter ([System.IO.File]::Create($ico))
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()
[System.IO.File]::WriteAllBytes((Join-Path $root 'build\logo-256.png'), $pngs[0])

$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
& $csc /nologo /codepage:65001 /target:winexe /optimize+ /unsafe /platform:anycpu "/out:$dist\PhanTrang.exe" "/win32icon:$ico" `
    "/win32manifest:$root\src\app.manifest" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Xml.dll /r:System.Xml.Linq.dll $sources
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Sign when a code-signing certificate is available (Certum SimplySign logged in, or PT_SIGN_THUMBPRINT set).
$hasCert = $env:PT_SIGN_THUMBPRINT -or @(Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue | Where-Object { $_.NotAfter -gt (Get-Date) }).Count -gt 0
$signer = Join-Path $root 'sign.ps1'   # maintainer-only, not in the public repository (releases are signed in CI)
if ($hasCert -and (Test-Path $signer)) { & $signer -File "$dist\PhanTrang.exe" }
else { Write-Warning 'CHUA KY SO: khong thay chung chi ky ma. Smart App Control se chan file nay. Xem CODE-SIGNING.md.' }

$fonts = Join-Path $dist 'assets\fonts'
New-Item -ItemType Directory -Force $fonts | Out-Null
Copy-Item (Join-Path $root 'assets\fonts\*') $fonts -Force
Copy-Item (Join-Path $root 'README.md') $dist -Force -ErrorAction SilentlyContinue

if (-not $OutDir) {
    $zip = Join-Path $root 'PhanTrang-Windows.zip'
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $dist 'PhanTrang.exe'), (Join-Path $dist 'assets'), (Join-Path $dist 'README.md') -DestinationPath $zip
}
Write-Host "Built $dist\PhanTrang.exe"
