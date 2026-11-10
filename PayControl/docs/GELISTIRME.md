# PayControl — Geliştirme Dokümantasyonu

## Mimari

Katmanlı yapı:

| Proje | Rol |
|-------|-----|
| `PayControl.Domain` | Entity, enum, domain servisleri (çıkış denetimi, hak ediş) |
| `PayControl.Application` | DTO, uygulama servisleri, arayüzler |
| `PayControl.Infrastructure` | EF Core SQLite, ClosedXML import, Serilog, kimlik |
| `PayControl.Desktop` | WPF MVVM arayüz (`net8.0-windows`) |
| `PayControl.Tests` | xUnit otomatik testler |

Bağımlılık yönü: Desktop → Application → Domain; Infrastructure → Application + Domain.

## Veritabanı

- SQLite dosyası: `%LocalAppData%/PayControl/paycontrol.db`
- `EnsureCreated` ile şema oluşturulur (V1)
- Finansal kayıtlar soft-delete ile silinmez; içe aktarma geri alma denetim izi bırakır

## Kritik domain kuralları

1. **Hak ediş dönemi ≠ çıkış tarihi ≠ banka ödeme tarihi**
2. SGK çıkışı yalnızca kullanıcı onayı / belge ile `Verildi` olur
3. Belirsiz ücret hesabı nihai sonuç kabul edilmez
4. Maaş değişikliği kartı günceller; geçmiş `MaasDonemKaydi` değişmez
5. Yeniden işe alım yeni `CalismaDonemi` açar, aynı `Personel` kartı

## Linux’ta derleme

WPF Windows’a özgüdür. CI / Cloud Agent:

```bash
dotnet test PayControl.sln -c Release -p:ExcludeDesktop=true
```

## Windows’ta derleme ve yayın

```bash
dotnet build src/PayControl.Desktop/PayControl.Desktop.csproj -c Release
dotnet publish src/PayControl.Desktop/PayControl.Desktop.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Çıktı: `src/PayControl.Desktop/bin/Release/net8.0-windows/win-x64/publish/PayControl.exe`

## Örnek bordro

```bash
dotnet run --project tools/GenerateSampleExcel -- samples
```

`samples/bordro-ornek.xlsx` üretilir.

## Varsayılan giriş

- Kullanıcı: `admin`
- Şifre: `Admin123!`
- İlk çalıştırmada otomatik oluşturulur.

## Sonraki aşamalar

Avans taksit motoru, banka dosyası eşleme, uyarı merkezi UI, yönetici paneli, PDF/OCR, şifreli yedek, kurulum paketi (Inno Setup / MSI).
