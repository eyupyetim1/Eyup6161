@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo PayControl kurulum baslatiliyor...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-PayControl.ps1"
if errorlevel 1 (
  echo.
  echo Kurulum basarisiz. Yukaridaki kirmizi mesaji okuyun.
  pause
)
