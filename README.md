# PayControl — Personel Mali Denetim Sistemi

Windows masaüstü uygulaması (WPF). Personel kartı, dönemsel maaş geçmişi, bordro Excel içe aktarma ve işten çıkış denetimi (V1).

## Hızlı başlangıç

```bash
cd PayControl
dotnet test PayControl.sln -c Release -p:ExcludeDesktop=true
dotnet run --project tools/GenerateSampleExcel -- samples
```

Windows’ta uygulama:

```bash
dotnet publish src/PayControl.Desktop/PayControl.Desktop.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Varsayılan giriş: `admin` / `Admin123!`

Ayrıntılar: [docs/KULLANIM.md](PayControl/docs/KULLANIM.md), [docs/GELISTIRME.md](PayControl/docs/GELISTIRME.md)
