#Requires -Version 5.1
<#
.SYNOPSIS
  PayControl'u kullanıcı klasörüne kurar ve masaüstü kısayolu oluşturur.
  Yönetici hakkı gerekmez.
#>
$ErrorActionPreference = 'Stop'
try {
    if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        $SourceDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    } else {
        $SourceDir = $PSScriptRoot
    }

    $exe = Join-Path $SourceDir 'PayControl.exe'
    if (-not (Test-Path $exe)) {
        # Yanlışlıkla installer alt klasöründen çalıştırıldıysa bir üstü dene
        $alt = Join-Path (Split-Path $SourceDir -Parent) 'PayControl.exe'
        if (Test-Path $alt) {
            $SourceDir = Split-Path $SourceDir -Parent
            $exe = $alt
        }
    }

    if (-not (Test-Path $exe)) {
        Write-Host ''
        Write-Host 'HATA: PayControl.exe bu klasörde yok.' -ForegroundColor Red
        Write-Host ''
        Write-Host 'Yanlis dosyayi actiniz olabilir (kaynak kod).' -ForegroundColor Yellow
        Write-Host 'Dogru paket: PayControl-Kurulum-win-x64.zip' -ForegroundColor Yellow
        Write-Host 'Icinde PayControl.exe + Install-PayControl.ps1 birlikte olmali.' -ForegroundColor Yellow
        Write-Host ''
        Write-Host "Su an bakilan klasor: $SourceDir"
        Write-Host ''
        Read-Host 'Devam etmek icin Enter'
        exit 1
    }

    $TargetDir = Join-Path $env:LOCALAPPDATA 'Programs\PayControl'
    New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

    Write-Host "PayControl kuruluyor..." -ForegroundColor Cyan
    Write-Host "Hedef: $TargetDir"

    Get-ChildItem -Path $SourceDir -File | ForEach-Object {
        if ($_.Name -like 'Install-PayControl*') { return }
        if ($_.Name -like 'KURULUM*') { return }
        if ($_.Name -like 'BASLAT*') { return }
        if ($_.Name -like 'OKU*') { return }
        if ($_.Extension -eq '.pdb') { return }
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $TargetDir $_.Name) -Force
    }
    # Alt klasörler (self-contained publish)
    Get-ChildItem -Path $SourceDir -Directory | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $TargetDir $_.Name) -Recurse -Force
    }
    Get-ChildItem -LiteralPath $TargetDir -Recurse -File | Unblock-File

    $Desktop = [Environment]::GetFolderPath('Desktop')
    $ShortcutPath = Join-Path $Desktop 'PayControl.lnk'
    $Wsh = New-Object -ComObject WScript.Shell
    $Sc = $Wsh.CreateShortcut($ShortcutPath)
    $Sc.TargetPath = Join-Path $TargetDir 'PayControl.exe'
    $Sc.WorkingDirectory = $TargetDir
    $Sc.Description = 'PayControl — Personel Mali Denetim Sistemi'
    $Sc.Save()

    $StartMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    $StartShortcut = Join-Path $StartMenu 'PayControl.lnk'
    $Sc2 = $Wsh.CreateShortcut($StartShortcut)
    $Sc2.TargetPath = Join-Path $TargetDir 'PayControl.exe'
    $Sc2.WorkingDirectory = $TargetDir
    $Sc2.Description = 'PayControl — Personel Mali Denetim Sistemi'
    $Sc2.Save()

    $Uninstall = @"
`$dir = '$TargetDir'
Remove-Item -LiteralPath (Join-Path ([Environment]::GetFolderPath('Desktop')) 'PayControl.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path `$env:APPDATA 'Microsoft\Windows\Start Menu\Programs\PayControl.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath `$dir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'PayControl kaldirildi. Veritabani silinmedi.' -ForegroundColor Yellow
Read-Host 'Enter'
"@
    Set-Content -Path (Join-Path $TargetDir 'Uninstall-PayControl.ps1') -Value $Uninstall -Encoding UTF8

    Write-Host ''
    Write-Host 'Kurulum tamamlandi.' -ForegroundColor Green
    Write-Host 'Masaustundeki PayControl kisayoluna cift tiklayin.'
    Write-Host ''
    Write-Host 'Giris: admin / Admin123!'
    Write-Host "Veriler: $env:LOCALAPPDATA\PayControl\"
    Write-Host ''
    Read-Host 'Kapatmak icin Enter'
}
catch {
    Write-Host ''
    Write-Host "HATA: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ''
    Read-Host 'Kapatmak icin Enter'
    exit 1
}
