#Requires -Version 5.1
<#
.SYNOPSIS
  PayControl'u kullanıcı klasörüne kurar ve masaüstü kısayolu oluşturur.
  Yönetici hakkı gerekmez.
#>
$ErrorActionPreference = 'Stop'

$SourceDir = $PSScriptRoot
if (-not (Test-Path (Join-Path $SourceDir 'PayControl.exe'))) {
    Write-Host 'HATA: PayControl.exe bu klasörde bulunamadı.' -ForegroundColor Red
    Write-Host 'Kurulum paketinin tamamını çıkarıp bu betiği yeniden çalıştırın.'
    exit 1
}

$TargetDir = Join-Path $env:LOCALAPPDATA 'Programs\PayControl'
New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

Write-Host "PayControl kuruluyor..." -ForegroundColor Cyan
Write-Host "Hedef: $TargetDir"

Get-ChildItem -Path $SourceDir -File | ForEach-Object {
    if ($_.Name -like 'Install-PayControl*') { return }
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $TargetDir $_.Name) -Force
}

# Masaüstü kısayolu
$Desktop = [Environment]::GetFolderPath('Desktop')
$ShortcutPath = Join-Path $Desktop 'PayControl.lnk'
$Wsh = New-Object -ComObject WScript.Shell
$Sc = $Wsh.CreateShortcut($ShortcutPath)
$Sc.TargetPath = Join-Path $TargetDir 'PayControl.exe'
$Sc.WorkingDirectory = $TargetDir
$Sc.Description = 'PayControl — Personel Mali Denetim Sistemi'
$Sc.Save()

# Başlat menüsü
$StartMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$StartShortcut = Join-Path $StartMenu 'PayControl.lnk'
$Sc2 = $Wsh.CreateShortcut($StartShortcut)
$Sc2.TargetPath = Join-Path $TargetDir 'PayControl.exe'
$Sc2.WorkingDirectory = $TargetDir
$Sc2.Description = 'PayControl — Personel Mali Denetim Sistemi'
$Sc2.Save()

# Kaldırma betiği
$Uninstall = @"
`$dir = '$TargetDir'
Remove-Item -LiteralPath (Join-Path ([Environment]::GetFolderPath('Desktop')) 'PayControl.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path `$env:APPDATA 'Microsoft\Windows\Start Menu\Programs\PayControl.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath `$dir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'PayControl kaldırıldı. Veritabanı (%LocalAppData%\PayControl) silinmedi.' -ForegroundColor Yellow
pause
"@
Set-Content -Path (Join-Path $TargetDir 'Uninstall-PayControl.ps1') -Value $Uninstall -Encoding UTF8

Write-Host ''
Write-Host 'Kurulum tamamlandı.' -ForegroundColor Green
Write-Host 'Masaüstündeki PayControl kısayoluna çift tıklayarak açabilirsiniz.'
Write-Host ''
Write-Host 'Giriş bilgileri:'
Write-Host '  Kullanıcı: admin'
Write-Host '  Şifre:     Admin123!'
Write-Host ''
Write-Host "Veriler: $env:LOCALAPPDATA\PayControl\paycontrol.db"
Write-Host ''
pause
