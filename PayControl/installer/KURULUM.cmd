@echo off
chcp 65001 >nul
cd /d "%~dp0"
title PayControl Kurulum
echo PayControl kurulum baslatiliyor...
echo.

if not exist "%~dp0PayControl.exe" (
  echo HATA: PayControl.exe bu klasorde yok.
  echo Dogru paket PayControl-Kurulum-win-x64.zip olmali.
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%~dp0' -Recurse -File | Unblock-File" >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-PayControl.ps1"
if errorlevel 1 (
  echo.
  echo Kurulum scripti hata verdi. Bunun yerine BASLAT.cmd deneyin.
  pause
  exit /b 1
)

echo.
echo Isterseniz simdi programi acmak icin Enter'a basin...
pause
start "" "%LOCALAPPDATA%\Programs\PayControl\PayControl.exe"
