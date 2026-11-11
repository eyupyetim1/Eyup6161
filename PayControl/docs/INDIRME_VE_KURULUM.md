# PayControl — İndirme ve Kurulum

## Kurulum programı (önerilen)

1. `PayControl-<sürüm>-Kurulum.exe` dosyasını indirin (GitHub sürümleri veya Actions → **PayControl-Kurulum-Setup**).
2. Çift tıklayın. Windows "bilinmeyen yayıncı" uyarısı gösterirse **Ek bilgi → Yine de çalıştır** (program henüz kod imzalı değil).
3. Sihirbazda **İleri → Kur → Son**. Yönetici hakkı gerekmez; `%LOCALAPPDATA%\Programs\PayControl` klasörüne kurulur.
4. Masaüstü ve Başlat menüsünde **PayControl** kısayolu oluşur. Başlat menüsünde kullanım kılavuzu, örnek bordro ve kaldırma kısayolları da vardır.

- **Güncelleme:** yeni sürümün kurulum programını aynı şekilde çalıştırın; verileriniz korunur. (Ayarlar → Güncelleme ile otomatik de yapılabilir.)
- **Kaldırma:** Ayarlar → Uygulamalar → PayControl → Kaldır (veya Başlat → PayControl → Kaldır). Veriler sorulmadan silinmez.
- **Sessiz kurulum (BT yöneticileri):** `PayControl-<sürüm>-Kurulum.exe /S` — kaldırma: `"%LOCALAPPDATA%\Programs\PayControl\Kaldir.exe" /S`

## ZIP paketi (alternatif)

1. GitHub Actions sayfasını açın:  
   https://github.com/eyupyetim1/Eyup6161/actions/workflows/build-windows.yml
2. En üstteki yeşil başarılı çalıştırmaya tıklayın.
3. **Artifacts** bölümünden **PayControl-Kurulum-win-x64** dosyasını indirin.
4. ZIP’i bilgisayarınıza kaydedip çıkarın.
5. `KURULUM.txt` adımlarını izleyin (veya `Install-PayControl.ps1` çalıştırın).

Giriş: **admin** / **Admin123!**

## Alternatif: Kaynak koddan (geliştirici)

Windows’ta:

```powershell
cd PayControl
dotnet publish src/PayControl.Desktop/PayControl.Desktop.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o publish/win-x64
```

Ardından `installer/Install-PayControl.ps1` dosyasını `publish/win-x64` yanına kopyalayıp çalıştırın.
