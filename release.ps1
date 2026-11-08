# Makes a release for the website:
#   dist\release\PhanTrang-Setup-<ver>.exe      per-user installer (signed)
#   dist\release\PhanTrang-<ver>-portable.zip   the signed app, no install
#   dist\release\phien-ban.json                 version, sizes, SHA-256 (read by the landing page)
# and copies them to site\tai-ve\.
#
#   powershell -ExecutionPolicy Bypass -File release.ps1                  # needs the code-signing certificate (SimplySign logged in)
#   powershell -ExecutionPolicy Bypass -File release.ps1 -AllowUnsigned   # test build only; NOT copied to the site
#   ... -SkipBuild    package the dist\PhanTrang.exe already there (CI: the exe was signed by SignPath in between)
param([switch]$AllowUnsigned, [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$out = Join-Path $dist 'release'
$site = Join-Path $root 'site\tai-ve'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }

if (-not $SkipBuild) { & (Join-Path $root 'build.ps1') }
$exe = Join-Path $dist 'PhanTrang.exe'
if (-not (Test-Path $exe)) { throw "Chưa có $exe. Chạy build.ps1 trước." }
$ver = (Get-Item $exe).VersionInfo.ProductVersion
$hasCert = $env:PT_SIGN_THUMBPRINT -or @(Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue | Where-Object { $_.NotAfter -gt (Get-Date) }).Count -gt 0
$signed = (Get-AuthenticodeSignature $exe).Status -eq 'Valid'
if (-not $signed -and -not $AllowUnsigned) {
    throw "PhanTrang.exe chưa có chữ ký số hợp lệ. Đăng nhập SimplySign rồi chạy lại (xem CODE-SIGNING.md). Chỉ để thử: thêm -AllowUnsigned."
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
$stage = Join-Path $out 'stage'
New-Item -ItemType Directory -Force (Join-Path $stage 'assets') | Out-Null
Copy-Item $exe $stage
Copy-Item (Join-Path $dist 'assets\*') (Join-Path $stage 'assets') -Recurse
Copy-Item (Join-Path $root 'README.md'), (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD-PARTY-NOTICES.md') $stage -ErrorAction SilentlyContinue   # font licences travel in assets\fonts

Add-Type -AssemblyName System.IO.Compression.FileSystem
$payload = Join-Path $root 'build\payload.zip'
if (Test-Path $payload) { Remove-Item $payload -Force }
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $payload, 'Optimal', $false)
$verFile = Join-Path $root 'build\version.txt'
[IO.File]::WriteAllText($verFile, $ver, (New-Object Text.UTF8Encoding $false))

$setup = Join-Path $out "PhanTrang-Setup-$ver.exe"
& $csc /nologo /codepage:65001 /target:winexe /optimize+ /platform:anycpu "/out:$setup" "/win32icon:$root\build\app.ico" `
    "/win32manifest:$root\installer\setup.manifest" "/resource:$payload,PhanTrang.payload.zip" "/resource:$verFile,PhanTrang.version.txt" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll `
    (Join-Path $root 'installer\Setup.cs')
if ($LASTEXITCODE -ne 0) { throw 'Biên dịch bộ cài lỗi' }
$signer = Join-Path $root 'sign.ps1'   # maintainer-only, not in the public repository (releases are signed in CI)
if ($hasCert -and (Test-Path $signer)) { & $signer -File $setup }

$portable = Join-Path $out "PhanTrang-$ver-portable.zip"
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $portable, 'Optimal', $true)
Remove-Item $stage -Recurse -Force

function Info($f) { $i = Get-Item $f; [ordered]@{ file = $i.Name; sizeMB = [Math]::Round($i.Length / 1MB, 2); sha256 = (Get-FileHash $f -Algorithm SHA256).Hash.ToLower() } }
$s = Info $setup; $p = Info $portable
$setupSigned = (Get-AuthenticodeSignature $setup).Status -eq 'Valid'
$json = [ordered]@{
    version = $ver; date = (Get-Date -Format 'dd/MM/yyyy'); signed = ($signed -and $setupSigned)
    file = $s.file; sizeMB = $s.sizeMB; sha256 = $s.sha256
    portable = $p
}
$jsonText = $json | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText((Join-Path $out 'phien-ban.json'), $jsonText, (New-Object Text.UTF8Encoding $false))

if ($json.signed) {
    New-Item -ItemType Directory -Force $site | Out-Null
    Get-ChildItem $site -Include 'PhanTrang-Setup-*.exe', 'PhanTrang-*-portable.zip' -Recurse | Remove-Item -Force
    Copy-Item $setup, $portable, (Join-Path $out 'phien-ban.json') $site -Force
    Write-Host "Đã phát hành $ver vào $site (đã ký số)."
} else {
    Write-Warning "Bản $ver CHƯA KÝ SỐ: chỉ để thử, không chép lên site\tai-ve. Kết quả ở $out"
}
$jsonText
