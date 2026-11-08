@echo off
rem Phan Trang: double-click to build the app from source. Result: dist\PhanTrang.exe
rem Needs only Windows 10/11 (uses the C# compiler that ships with Windows).
setlocal
cd /d "%~dp0"
echo Dang build Phan Trang...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if errorlevel 1 (
  echo.
  echo Build loi. Xem thong bao o tren.
  if not defined CI pause
  exit /b 1
)
echo.
echo Xong: %~dp0dist\PhanTrang.exe
if not defined CI (
  explorer "%~dp0dist"
  pause
)
