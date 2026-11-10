# Değişiklik Geçmişi

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
