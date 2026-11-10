# PayControl — İndirme ve Kurulum

## Hazır kurulum paketini indirme

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
