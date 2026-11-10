# Değişiklik Geçmişi

## 1.3.0 — 2026-11-10 (çoklu tesis, rapor merkezi, e-posta / WhatsApp, yönetim)

### Çoklu tesis
- Tesis tanımları (kod, şirket, sorumlu, iletişim); bordro içe aktarmada eksik tesisler otomatik açılır, "Personelden oluştur" ile toplu oluşturma
- Ödeme öncesi kontrol tesis bazında: yalnız o tesisin bordrosu, ayrı sürümlü onaylı liste, dosya adında tesis
- Mutabakat ve kapanış anlık görüntüsü tesis bilgisini taşır; her tesis sorumlusu dönemi ayrıca onaylar, tüm tesisler onaylanmadan kapanış tamamlanmaz (yeni kontrol maddesi "Tesis onayları")
- Gösterge paneli: son 12 ay bordro/ödeme grafiği, tesis dağılımı, açık uyarı sayısı

### Rapor merkezi (18 rapor)
Dönem bordro özeti, tesis karşılaştırma, personel maliyet trendi, ödenmemiş hak ediş yaşlandırma, avans durumu, fazla ödeme ve tahsilat, giriş-çıkış/devir hızı, SGK çıkış durumu, kesinti raporu (avans/icra/BES/tahsilat), mutabakat farkları, ödeme kontrol istisnaları, banka/kasa ödeme dökümü, maaş değişiklikleri, denetim izi, kapanış özetleri, personel bakiye listesi, icra dosyaları, tesis personel listesi. Tarih aralığı, tesis ve şirket filtresi; önizleme; Excel, PDF ve CSV.

### Bildirimler (e-posta ve WhatsApp)
- Elle gönderim: kayıtlı alıcılar veya doğrudan adres; rapor ekli gönderim; API anahtarı olmadan "WhatsApp'ta aç" (wa.me)
- Otomatik: günlük / haftalık / aylık zamanlanmış rapor, uyarı özeti; olay bildirimleri (dönem kapandı, mutabakat farkı, yedek hatası); tesis seçilirse rapor süzülür ve yalnız o tesisin alıcılarına gider
- SMTP (MailKit) ve WhatsApp Business Cloud API (onaylı şablon + belge eki); şifre/anahtar şifreli saklanır
- Gönderim geçmişi, başarısızların en fazla 3 kez yeniden denenmesi, aynı olay için tekrar gönderim engeli
- Uygulama açıkken 5 dakikada bir; kapalıyken Windows Görev Zamanlayıcısı (`PayControl.exe --zamanlanmis-gorevler`)

### Yönetim ve güvenlik
- Kullanıcı yönetimi: oluşturma, rol, pasifleştirme, geçici şifre; ilk girişte zorunlu şifre değişimi; şifre politikası; 5 hatalı girişte 15 dk kilit; son yöneticinin korunması
- Yedekleme: çalışırken tutarlı anlık yedek, SHA-256 manifest, isteğe bağlı AES-256-GCM şifreleme, günlük otomatik yedek ve saklama sayısı, güvenlik yedeği alınarak geri yükleme
- Denetim geçmişi ekranı (filtre, sayfalama, Excel); uyarı merkezi (tüm modüllerin açık işleri)
- Banka/kasa ödemesi iptali: kayıt silinmez, ters kayıt oluşur, doğru ödeme yeniden girilebilir
- İcra dosyaları (sıralı dağıtım, kalan borç), BES tanımı/cayma, fazla ödeme için taksitli tahsilat planı (bordrodaki "FAZLA ÖDEME KESİNTİSİ" kolonu taksitlere işlenir); kontrol motoru bu dayanaklarla tutar doğrular
- Personel belgeleri: güvenli depo, SHA-256 bütünlük, silinemez (geçersiz kılınır)

### Altyapı
- PostgreSQL desteği (çok kullanıcılı); tüm testler SQLite ve PostgreSQL 16'da çalışır; şema yükseltici iki sağlayıcıyı destekler
- Banka hareketi içe aktarmada MT940 ve ISO 20022 CAMT.053 (toplu ödeme işlem bazında, ters kayıt ve alacaklar atlanır)
- Otomatik güncelleme: GitHub sürümü veya ağ paylaşımındaki `guncelleme.json`; SHA-256 doğrulaması, kurulum öncesi yedek, hata olursa geri alma
- CI: PostgreSQL test işi, `CODE_SIGN_PFX` tanımlıysa kod imzalama, `v*` etiketinde otomatik GitHub sürümü


## 1.2.0 — 2026-11-10 (Bölüm 16–18 tam entegrasyon)

1.1'deki üç modülün gereksinime göre eksik/hatalı kalan kısımları tamamlandı. Ayrıntılı analiz: `docs/V12_ANALIZ.md`.

### Hesap ekstresi
- Merkezi mali hareket defteri (`MaliHareketServisi`): tüm modüller buradan yazar; çalışma dönemi, şirket, tesis otomatik atanır
- Bordrodaki avans kesintisi avans kartına FIFO mahsup edilir (kart ve ekstre aynı bakiye); elle mahsupla çift sayım engellendi
- Bordro geri alma artık kayıt silmiyor: iptal işareti + ters kayıt + avans iadesi; kapalı dönemde engelli
- İşe giriş/çıkış, maaş değişikliği, kasa ödemesi, bordro dışı hak edişler, iade, devreden bakiye, fazla ödeme kesinleştirme/açıklama, ters kayıt
- Ödenmemiş hak ediş dönem bazında; yürüyen dört ayrı bakiye; dönem ve çalışma dönemi özetleri
- Şirket / tesis / tarih / işlem türü / çalışma dönemi filtreleri çalışıyor
- PDF: çok sayfalı (eskisi 80 satırda kesiliyordu), Türkçe karakterler korunuyor
- Yeniden işe alım (`YenidenIseAlAsync`) ve bordrodan yeniden giriş tespiti

### Ödeme öncesi kontrol
- Kural motoru bulgu tabanlı yeniden yazıldı; çıkış ayı gün/orantı, yeniden işe alım, avans planı, icra/BES sürekliliği, eksik gün, devreden düzeltme, açık şüpheli bakiye, IBAN kontrol hanesi, ad uyumu, başka personelin IBAN'ı, TC'siz banka satırları
- Banka satırı düzeltme / listeden çıkarma (orijinal değer saklanır), yeniden değerlendirme
- İstisna yalnızca Yönetici; belirsiz eşleşmede engelli; tüm istisnalar denetimde
- Onaylı liste yüklenen banka dosyası formatında; listeye giren satırlar ve bordro kaynağı saklanır; dönem bazlı sürüm; sonradan değişiklik eski sürümü geçersiz kılar
- Kontrol sonrası veri değişikliği liste üretimini durdurur

### Kapanış ve mutabakat
- Gerçekleşen banka hareketlerini Excel'den içe aktarma (satır bazında mükerrer engeli)
- Mutabakat farkları (7 tür), çözüm/istisna akışı, şüpheli fazla ödemenin deftere yazılması
- Kontrol listesinin 11 maddesi veriyle otomatik doğrulanıyor; başarısız madde gerekçesiz onaylanamaz
- Açık fark varken kapanış engelli; kapanışta anlık görüntü ve toplam tarihçesi; kapalı dönem özeti yeniden hesaplanmaz
- Veritabanı düzeyinde koruma: mali hareket ve denetim kayıtları değiştirilemez; kapalı döneme ait kayıtlar eklenemez/değiştirilemez/silinemez
- Durum makinesi geçiş kuralları; kapatma / yeniden açma yetkisi
- Düzeltme kaydı farkı sonraki açık döneme devreden alacak olarak yazar
- Önceki ay karşılaştırması kişi bazında ve kaynak kayıtla; kapanış raporu Excel

### Arayüz (tasarım sistemi)
- Yeni tema (`Themes/Theme.xaml`): tüm butonlar, metin kutuları, açılır listeler, tarih seçici, tablolar, sekmeler, onay kutuları, kaydırma çubukları ve ipuçları yeniden tasarlandı; hover/basma/odak animasyonları
- Ana ekran yan menülü kabuk + KPI kartları; personel kartında avatar, durum rozetleri, altı bakiye kartı
- Ödeme kontrolünde adım göstergesi, kritik satır vurgusu ve seçili satır paneli; kapanışta dönem durum zaman çizelgesi
- Tutar/tarih biçimi uygulama genelinde Türkçe (1.234,56)

### Altyapı
- `SemaYukseltici`: mevcut `paycontrol-v11.db` veritabanına eksik tablo/kolon/indeksleri ekler (veri silinmez)
- `YetkiServisi`: rol kontrolü (Görüntüleyici < Muhasebe < Yönetici)
- Testler: 12 zorunlu senaryo + 14 entegrasyon testi (toplam 42)


## 1.1.0 — 2026-11-10 (Hesap ekstresi, ödeme öncesi kontrol, dönem kapanış)

### Eklenenler
- Personel **Hesap Ekstresi** sekmesi: ayrı bakiyeler (maaş alacağı / avans borcu / şüpheli fazla / kesinleşmiş geri ödeme), Excel+PDF export
- `MaliHareket` defteri (benzersiz işlem kimliği), `AvansKaydi` + mahsup planı/gerçekleşme
- **Ödeme Öncesi Kontrol**: bordro × banka Excel karşılaştırması, kritik uyarı bloğu, gerekçeli istisna, onaylı liste sürümleri (bankaya göndermez)
- **Aylık dönem kapanış**: durum makinesi, 12 maddelik kontrol listesi, kilit, düzeltme kaydı, yeniden açma, önceki ay karşılaştırması
- Veritabanı dosyası: `paycontrol-v11.db`
- 13 yeni otomatik senaryo testi (toplam 29)

## 1.0.0 — 2026-11-10 (V1)

### Eklenenler
- Katmanlı çözüm: Domain, Application, Infrastructure, Desktop (WPF), Tests
- SQLite + EF Core personel, çalışma dönemi, maaş dönem kaydı, işten çıkış, denetim
- Bordro Excel içe aktarma (ClosedXML), başlık eşleme, önizleme, mükerrer dosya engeli, geri alma
- İşten çıkış kaydı; SGK durumu ayrı ve onay zorunlu
- Hak ediş / ödeme / çıkış tarihi ayrımı ile çıkış ödeme inceleme servisi
- WPF: giriş, personel listesi, detay sekmeleri, çıkış formu, bordro import
- Serilog dosya loglama
- Otomatik testler (çıkış ödeme, mükerrer import, yeniden işe alım, maaş dönem koruması)
- Kullanım ve geliştirme dokümanları; Windows win-x64 publish profili
