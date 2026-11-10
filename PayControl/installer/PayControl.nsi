; PayControl kurulum programı (NSIS 3, Unicode)
; Derleme: makensis -DSURUM=1.3.1 -DKAYNAK=<publish klasörü> -DCIKTI=<setup.exe yolu> installer/PayControl.nsi
; Yönetici hakkı gerekmez: kullanıcı klasörüne kurar (%LOCALAPPDATA%\Programs\PayControl).
; Veriler (%LOCALAPPDATA%\PayControl) kurulumdan ayrıdır; güncelleme ve kaldırma verilere dokunmaz.

Unicode true
SetCompressor /SOLID lzma
SetCompressorDictSize 64
RequestExecutionLevel user
ManifestDPIAware true

!ifndef SURUM
  !define SURUM "1.3.1"
!endif
!ifndef KAYNAK
  !error "KAYNAK (publish klasörü) tanımlanmalı"
!endif
!ifndef CIKTI
  !define CIKTI "PayControl-${SURUM}-Kurulum.exe"
!endif

!define URUN "PayControl"
!define ACIKLAMA "PayControl — Personel Mali Denetim"
!define KALDIR_ANAHTARI "Software\Microsoft\Windows\CurrentVersion\Uninstall\PayControl"
!define GOREV_ADI "PayControl\Zamanlanmis Gorevler"

Name "${URUN} ${SURUM}"
OutFile "${CIKTI}"
InstallDir "$LOCALAPPDATA\Programs\PayControl"
InstallDirRegKey HKCU "${KALDIR_ANAHTARI}" "InstallLocation"
BrandingText "${ACIKLAMA} ${SURUM}"

VIProductVersion "${SURUM}.0"
VIAddVersionKey /LANG=1055 "ProductName" "${URUN}"
VIAddVersionKey /LANG=1055 "FileDescription" "${URUN} kurulum programı"
VIAddVersionKey /LANG=1055 "FileVersion" "${SURUM}"
VIAddVersionKey /LANG=1055 "ProductVersion" "${SURUM}"
VIAddVersionKey /LANG=1055 "LegalCopyright" "${URUN}"

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"

!define MUI_ICON "${KAYNAK}\..\PayControl.ico"
!define MUI_UNICON "${KAYNAK}\..\PayControl.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "${URUN} ${SURUM} kurulumu"
!define MUI_WELCOMEPAGE_TEXT "Bu sihirbaz PayControl'ü bilgisayarınıza kuracak.$\r$\n$\r$\nYönetici hakkı gerekmez. Daha önce kurduysanız yeni sürüm üzerine kurulur; verileriniz (personel, bordro, ödemeler, ayarlar) korunur.$\r$\n$\r$\nDevam etmeden önce PayControl açıksa kapatın."
!define MUI_FINISHPAGE_TITLE "Kurulum tamamlandı"
!define MUI_FINISHPAGE_TEXT "PayControl kuruldu. Masaüstünde ve Başlat menüsünde kısayol oluşturuldu.$\r$\n$\r$\nİlk giriş: kullanıcı adı admin, şifre Admin123! (ilk girişte yeni şifre belirlemeniz istenir)."
!define MUI_FINISHPAGE_RUN "$INSTDIR\PayControl.exe"
!define MUI_FINISHPAGE_RUN_TEXT "PayControl'ü şimdi başlat"
!define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\KULLANIM.md"
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Kullanım kılavuzunu aç"
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Turkish"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "PayControl 64 bit Windows 10 veya 11 gerektirir."
    Abort
  ${EndIf}
FunctionEnd

; Açık PayControl'ü kapatmayı ister (dosyalar kilitli kalmasın)
!macro UygulamayiKapat
  nsExec::ExecToStack 'cmd /c tasklist /FI "IMAGENAME eq PayControl.exe" /NH | find /I "PayControl.exe"'
  Pop $0
  ${If} $0 == 0
    MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION "PayControl şu anda açık. Devam etmek için kapatılacak (kaydedilmemiş işlem varsa önce İptal'e basıp kaydedin)." /SD IDOK IDOK +2
    Abort
    nsExec::Exec 'taskkill /IM PayControl.exe'
    Sleep 2500
    nsExec::Exec 'taskkill /F /IM PayControl.exe'
    Sleep 800
  ${EndIf}
!macroend

Section "PayControl" SecAna
  SectionIn RO
  !insertmacro UygulamayiKapat

  SetOutPath "$INSTDIR"
  ; Eski sürümden kalan uygulama dosyalarını temizle (veriler ayrı klasördedir)
  RMDir /r "$INSTDIR\runtimes"
  Delete "$INSTDIR\*.dll"

  File /r "${KAYNAK}\*.*"
  File "/oname=PayControl.ico" "${KAYNAK}\..\PayControl.ico"

  CreateDirectory "$INSTDIR\Ornekler"
  SetOutPath "$INSTDIR\Ornekler"
  File "/oname=bordro-ornek.xlsx" "${KAYNAK}\..\bordro-ornek.xlsx"
  SetOutPath "$INSTDIR"

  ; Kısayollar
  CreateShortCut "$DESKTOP\PayControl.lnk" "$INSTDIR\PayControl.exe" "" "$INSTDIR\PayControl.exe" 0 SW_SHOWNORMAL "" "${ACIKLAMA}"
  CreateDirectory "$SMPROGRAMS\PayControl"
  CreateShortCut "$SMPROGRAMS\PayControl\PayControl.lnk" "$INSTDIR\PayControl.exe" "" "$INSTDIR\PayControl.exe" 0 SW_SHOWNORMAL "" "${ACIKLAMA}"
  CreateShortCut "$SMPROGRAMS\PayControl\Kullanım Kılavuzu.lnk" "$INSTDIR\KULLANIM.md"
  CreateShortCut "$SMPROGRAMS\PayControl\Örnek Bordro.lnk" "$INSTDIR\Ornekler\bordro-ornek.xlsx"
  CreateShortCut "$SMPROGRAMS\PayControl\PayControl'ü Kaldır.lnk" "$INSTDIR\Kaldir.exe"
  ; Eski (zip/PowerShell) kurulumun Başlat kısayolu
  Delete "$SMPROGRAMS\PayControl.lnk"

  ; Programlar ve Özellikler kaydı
  WriteUninstaller "$INSTDIR\Kaldir.exe"
  WriteRegStr HKCU "${KALDIR_ANAHTARI}" "DisplayName" "${URUN}"
  WriteRegStr HKCU "${KALDIR_ANAHTARI}" "DisplayVersion" "${SURUM}"
  WriteRegStr HKCU "${KALDIR_ANAHTARI}" "Publisher" "${URUN}"
  WriteRegStr HKCU "${KALDIR_ANAHTARI}" "DisplayIcon" "$INSTDIR\PayControl.exe"
  WriteRegStr HKCU "${KALDIR_ANAHTARI}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${KALDIR_ANAHTARI}" "UninstallString" '"$INSTDIR\Kaldir.exe"'
  WriteRegStr HKCU "${KALDIR_ANAHTARI}" "QuietUninstallString" '"$INSTDIR\Kaldir.exe" /S'
  WriteRegDWORD HKCU "${KALDIR_ANAHTARI}" "NoModify" 1
  WriteRegDWORD HKCU "${KALDIR_ANAHTARI}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKCU "${KALDIR_ANAHTARI}" "EstimatedSize" "$0"
SectionEnd

Section "Uninstall"
  !insertmacro UygulamayiKapat

  ; Program kapalıyken çalışan zamanlanmış görev varsa kaldır
  nsExec::Exec 'schtasks /Delete /F /TN "${GOREV_ADI}"'

  Delete "$DESKTOP\PayControl.lnk"
  RMDir /r "$SMPROGRAMS\PayControl"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "${KALDIR_ANAHTARI}"

  ; Veriler varsayılan olarak korunur
  IfFileExists "$LOCALAPPDATA\PayControl\*.*" 0 bitti
    MessageBox MB_YESNO|MB_ICONQUESTION|MB_DEFBUTTON2 "PayControl verileri (personel, bordro, ödemeler, yedekler, ayarlar) de silinsin mi?$\r$\n$\r$\n$LOCALAPPDATA\PayControl$\r$\n$\r$\nEmin değilseniz Hayır'ı seçin; program yeniden kurulduğunda veriler kaldığı yerden kullanılır." /SD IDNO IDYES sil
    Goto bitti
  sil:
    MessageBox MB_YESNO|MB_ICONEXCLAMATION|MB_DEFBUTTON2 "Bu işlem geri alınamaz. Tüm PayControl verileri kalıcı olarak silinecek. Emin misiniz?" IDNO bitti
    RMDir /r "$LOCALAPPDATA\PayControl"
  bitti:
SectionEnd
