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

## 1.3 eklentileri

- **Sağlayıcı seçimi:** `VeritabaniYapilandirmasi` (`%LocalAppData%/PayControl/veritabani.json` veya uygulama klasörü). `Saglayici: "PostgreSql"` + `BaglantiMetni` / `BaglantiMetniSifreli`. Npgsql için `Npgsql.EnableLegacyTimestampBehavior` açıktır (uygulama yerel ve UTC zamanlarını birlikte kullanır).
- **PostgreSQL testleri:** `PAYCONTROL_TEST_PG="Host=localhost;Username=postgres;Password=..." dotnet test PayControl.sln -c Release -p:ExcludeDesktop=true` — her test geçici `pc_test_*` veritabanı açar ve siler. SQLite'a özgü dosya yedeği testleri PostgreSQL'de atlanır.
- **Zamanlanmış görevler:** `IZamanlanmisGorevServisi` (otomatik yedek → zamanlanmış kurallar → yeniden deneme; her adım ayrı kapsam). Masaüstünde `DispatcherTimer` (5 dk), CLI `PayControl.exe --zamanlanmis-gorevler`, Windows kaydı `WindowsGorevZamanlayici` (schtasks).
- **Bildirim:** `BildirimServisi` → `IEpostaGonderici` (MailKit) / `IWhatsAppGonderici` (Graph API). `BildirimKaydi.OlayAnahtari` benzersizdir; aynı olay/vade ikinci kez gönderilmez.
- **Banka dosyaları:** `BankaHareketOkuyucu.Oku` biçimi içerikten algılar (xlsx / MT940 / CAMT.053).
- **Güncelleme:** `GuncellemeServisi` bildirim (`guncelleme.json`: surum, adres, sha256) → indirme + SHA-256 → PowerShell güncelleyici (süreç kapanınca kopyalar, hata olursa `.onceki` klasöründen geri alır). CI `v*` etiketinde zip ve `guncelleme.json` dosyasını GitHub sürümüne yükler.
- **Kod imzalama:** depo gizlerine `CODE_SIGN_PFX` (base64 .pfx) ve `CODE_SIGN_PASSWORD` eklenirse CI `signtool` ile exe/dll imzalar.

## Mali hareket defteri ve veri bütünlüğü (1.2)

- Tüm mali hareketler `IMaliHareketServisi.DeftereEkleAsync` üzerinden yazılır (benzersiz `IslemKimligi`, çalışma dönemi/şirket/tesis ataması, kapalı dönem kontrolü)
- Hareketin bakiye etkisi `MaliHareketKurallari.Etki` ile hesaplanır; dört bakiye (maaş alacağı, avans, şüpheli fazla, kesin geri ödeme) ayrıdır
- `VeriButunluguKorumasi` (DbContext `SaveChanges`) son savunma hattıdır: `MaliHareket` ve `DenetimKaydi` değiştirilemez/silinemez; kapalı döneme ait bordro, kalem, ödeme, avans mahsubu, kontrol, liste ve mutabakat kayıtları eklenemez/değişemez. Tek istisna `OncekiDonemDuzeltme` hareketi
- `MaasDonemKaydi` global sorgu filtresiyle iptal edilenleri gizler (`IgnoreQueryFilters` ile erişilir)
- Şema değişikliği: entity'ye alan ekleyin; `SemaYukseltici` eski veritabanlarına kolonu ekler. Zorunlu (non-null) alanların varsayılanı entity'nin `new()` değerinden alınır

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

Masaüstü projesi Linux'ta yalnızca **derlenebilir** (çalıştırılamaz). Microsoft SDK'sında bulunan
`Microsoft.NET.Sdk.WindowsDesktop` klasörü gerekir:

```bash
dotnet build src/PayControl.Desktop/PayControl.Desktop.csproj -p:ExcludeDesktop=false -p:EnableWindowsTargeting=true
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

Bkz. `docs/DEGISIKLIK_GECMISI.md` ve sürüm notlarındaki öneriler.
