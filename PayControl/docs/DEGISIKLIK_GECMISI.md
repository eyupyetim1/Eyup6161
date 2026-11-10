# Değişiklik Geçmişi

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
