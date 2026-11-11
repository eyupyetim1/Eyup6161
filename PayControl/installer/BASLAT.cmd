@echo off
chcp 65001 >nul
cd /d "%~dp0"
title PayControl

echo ========================================
echo   PayControl baslatiliyor...
echo ========================================
echo.

if not exist "%~dp0PayControl.exe" (
  echo HATA: PayControl.exe bu klasorde yok.
  echo Dogru ZIP: PayControl-Kurulum-win-x64.zip
  echo.
  pause
  exit /b 1
)

REM Windows "internetten indi" kilidini kaldir
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%~dp0' -Recurse -File | Unblock-File" >nul 2>&1

echo Windows uyari verirse:
echo   1^) "Ek bilgi" / "More info" tiklayin
echo   2^) "Yine de calistir" / "Run anyway" tiklayin
echo.
echo Program acilmazsa masaustunde PayControl-HATA.txt olusur.
echo.

start "" "%~dp0PayControl.exe"
timeout /t 3 /nobreak >nul

echo Program baslatildi. Bu pencereyi kapatabilirsiniz.
echo Giris: admin  /  Admin123!
echo.
pause
