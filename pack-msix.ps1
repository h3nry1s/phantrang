# Đóng gói Phấn Trắng thành MSIX để nộp lên Microsoft Store.
# Store sẽ ký gói bằng chứng chỉ của Microsoft, nên gói nộp lên KHÔNG cần ký trước.
#
# Ba giá trị dưới đây lấy trong Partner Center > ứng dụng > Product identity (sau khi đặt tên "Phấn Trắng"):
#   -IdentityName      Package/Identity/Name        (ví dụ 12345TenBan.PhanTrang)
#   -Publisher         Package/Identity/Publisher   (ví dụ CN=A1B2C3D4-....)
#   -PublisherName     Package/Properties/PublisherDisplayName
#
#   powershell -ExecutionPolicy Bypass -File pack-msix.ps1 -IdentityName ... -Publisher "CN=..." -PublisherName "..."
#
# Không có ba giá trị đó thì gói dùng tên thử nghiệm: đủ để kiểm tra gói đóng được, KHÔNG nộp lên Store được.
# Script tự build app vào build\store-app (không đụng tới dist\ và bản đang chạy).
param(
    [string]$IdentityName = 'PhanTrang.ThuNghiem',
    [string]$Publisher = 'CN=PhanTrangThuNghiem',
    [string]$PublisherName = 'Phấn Trắng',
    # must be one of the names reserved for the app in Partner Center (Store rejects the package otherwise)
    [string]$DisplayName = 'Phấn Trắng',
    [string]$Version          # mặc định: phiên bản của app, dạng a.b.c.0 (Store đòi số cuối là 0)
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$appDir = Join-Path $root 'build\store-app'
& (Join-Path $root 'build.ps1') -OutDir $appDir
$exe = Join-Path $appDir 'PhanTrang.exe'
if (-not $Version) { $v = [Version](Get-Item $exe).VersionInfo.FileVersion; $Version = "$($v.Major).$($v.Minor).$([Math]::Max(0, $v.Build)).0" }

$makeappx = @((Join-Path $root 'tools\sdk-buildtools'), "${env:ProgramFiles(x86)}\Windows Kits\10\bin") |
    ForEach-Object { Get-ChildItem $_ -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue } |
    Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $makeappx) { throw 'Chưa có makeappx (Windows SDK). Cài Windows 10/11 SDK hoặc giải nén gói NuGet Microsoft.Windows.SDK.BuildTools vào tools\sdk-buildtools.' }

$layout = Join-Path $root 'build\msix-layout'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $layout 'Assets'), (Join-Path $layout 'assets\fonts') | Out-Null
Copy-Item $exe $layout
Copy-Item (Join-Path $root 'assets\fonts\*') (Join-Path $layout 'assets\fonts')
Copy-Item (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD-PARTY-NOTICES.md') $layout

# ---- logos (the chalk-on-green-board mark), every size the manifest names ----
Add-Type -AssemblyName System.Drawing
function Save-Logo([int]$w, [int]$h, [string]$name, [double]$markShare = 0.8) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = [Math]::Min($w, $h) * $markShare
    $g.TranslateTransform(($w - $s) / 2, ($h - $s) / 2)
    $g.ScaleTransform($s / 96.0, $s / 96.0)
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
    $bmp.Save((Join-Path $layout "Assets\$name"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
Save-Logo 50 50 'StoreLogo.png' 1.0
Save-Logo 44 44 'Square44x44Logo.png' 1.0
Save-Logo 24 24 'Square44x44Logo.targetsize-24_altform-unplated.png' 1.0
Save-Logo 48 48 'Square44x44Logo.targetsize-48_altform-unplated.png' 1.0
Save-Logo 256 256 'Square44x44Logo.targetsize-256_altform-unplated.png' 1.0
Save-Logo 150 150 'Square150x150Logo.png' 0.7
Save-Logo 310 150 'Wide310x150Logo.png' 0.7

# ---- manifest ----
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
         IgnorableNamespaces="uap desktop rescap">
  <Identity Name="$IdentityName" Publisher="$Publisher" Version="$Version" ProcessorArchitecture="neutral" />
  <Properties>
    <DisplayName>$DisplayName</DisplayName>
    <PublisherDisplayName>$PublisherName</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
    <Description>Bộ công cụ giảng dạy cho thầy cô: chỉ bảng bằng viên phấn, gõ chữ như viết tay, bảng xanh và vở ô li, chấm bài bằng Điểm 10 và Hoa điểm tốt.</Description>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Resources>
    <Resource Language="vi-VN" />
  </Resources>
  <Applications>
    <Application Id="PhanTrang" Executable="PhanTrang.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="$DisplayName" Description="Bảng xanh trên mọi màn hình"
                          BackgroundColor="transparent" Square150x150Logo="Assets\Square150x150Logo.png" Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" />
      </uap:VisualElements>
      <Extensions>
        <desktop:Extension Category="windows.startupTask" Executable="PhanTrang.exe" EntryPoint="Windows.FullTrustApplication">
          <desktop:StartupTask TaskId="PhanTrangStartup" Enabled="false" DisplayName="$DisplayName" />
        </desktop:Extension>
      </Extensions>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@
[System.IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object System.Text.UTF8Encoding $false))

$outDir = Join-Path $root 'dist\release'
New-Item -ItemType Directory -Force $outDir | Out-Null
$out = Join-Path $outDir ("PhanTrang-" + $Version + ".msix")
if (Test-Path $out) { Remove-Item $out -Force }
& $makeappx pack /d $layout /p $out /o | Select-Object -Last 3
if ($LASTEXITCODE -ne 0) { throw 'Đóng gói MSIX thất bại.' }
Write-Host "Đã tạo $out (chưa ký; Store sẽ ký khi duyệt)."
if ($IdentityName -eq 'PhanTrang.ThuNghiem') { Write-Warning 'Gói này dùng danh tính thử nghiệm. Muốn nộp Store: chạy lại với -IdentityName, -Publisher, -PublisherName lấy từ Partner Center.' }
