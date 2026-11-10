@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ============================================
echo  PayControl - Windows kurulum (yerel derleme)
echo ============================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
  echo .NET SDK bulunamadi.
  echo Simdi indirme sayfasi acilacak. ".NET 8 SDK" kurun, sonra bu dosyayi tekrar calistirin.
  echo.
  pause
  start https://dotnet.microsoft.com/download/dotnet/8.0
  exit /b 1
)

echo Derleniyor, lutfen bekleyin (birkaç dakika sürebilir)...
dotnet publish src\PayControl.Desktop\PayControl.Desktop.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o "%~dp0publish\win-x64"
if errorlevel 1 (
  echo Derleme basarisiz.
  pause
  exit /b 1
)

copy /Y "%~dp0installer\Install-PayControl.ps1" "%~dp0publish\win-x64\" >nul
copy /Y "%~dp0installer\KURULUM.cmd" "%~dp0publish\win-x64\" >nul
copy /Y "%~dp0installer\KURULUM.txt" "%~dp0publish\win-x64\" >nul

echo.
echo Derleme tamam. Kurulum basliyor...
cd /d "%~dp0publish\win-x64"
call KURULUM.cmd
