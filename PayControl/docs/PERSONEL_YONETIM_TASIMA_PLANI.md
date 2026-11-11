# Personel Yönetim Sistemi — Excel/VBA sisteminin taşınma planı

Amaç: "Personel Yönetim Sistemi.xlsb" (VBA, Access `Database.accdb`) ile yapılan tüm işler bu programdan yapılacak.
Kural: VBA'daki kodlar, puantaj kodları, hesap kuralları ve kontroller **aynen** korunur; her hesap Access'teki mevcut sonuçlarla karşılaştırılarak doğrulanır.

## Kaynak sistem envanteri

**VBA:** 60 modül, ~50.000 satır. Ana pencere `frmUygulama` (6.900 satır, 445 işlem).
**Access:** 59 tablo. Gerçek veride: 114 personel, 13 tesis, ~11.000 günlük puantaj kaydı (tesis başına ayrı tablo), 485 aylık özet, 211 puantaj notu, 158 izin işlem kaydı, 147 HT bakiye sabitleme, 131 personel belgesi, 70 şirket sözleşmesi, 96 kişiye özel izin ayarı, 4 şirket.

| Alan | VBA modülleri | Access tabloları |
|---|---|---|
| Personel kartı, nakil, işlem günlüğü | frmUygulama, mdlPersonel, mdlPersonelIslem, frmPersonelAktarma, frmIslemler | Personel, PersonelNakil, PersonelIslemLog, PersonelMaas |
| İşe giriş kontrol listesi | frmIseGiris | IseGirisKontrol |
| Puantaj (günlük kod tablosu, takvim, resmi tatil, not) | frmUygulama, mdlPuantajTakvim, mdlHesaplamaVeIzin | PuantajGunluk_<Tesis>, PuantajNot, ManuelDuzeltme, AylikOzet, (Ayarlar: İzinler/Resmi Tatiller, KodAyar) |
| İzin ve bakiye motoru (yıllık izin, hafta tatili, devreden, görev/hasat izni) | mdlBakiye, mdlHesaplamaVeIzin, frmKurallar | IzinHareket, HTBakiyeSabitleme, PersonelIzinAyar, GorevIzinKesinti, HasatIzinKesinti, IzinIslemLog |
| Düzeltme / güvenlik | mdlDuzeltme | ManuelDuzeltme, IzinIslemLog |
| Özlük belgeleri, belge merkezi, hatırlatma, evrak ayırma, QR | mdlBelge, mdlBelgeMerkezi, frmBelgeHatirlat, frmEvrakAyir, frmKisiSec, mdlQR | BelgeTanim, PersonelBelge |
| İşyeri İSG belgeleri | frmIsgBelge | IsyeriBelgeTur, IsyeriBelge, IsyeriBelgeKisi |
| Şirketler, şirket/personel sözleşmeleri | mdlSirket, frmSirket, frmSozlesme | Sirket, PersonelSirket, TesisSirket, SirketSozlesme, PersonelSozlesme |
| Tesisler | frmTesisYonetimi | Tesisler, TesisAdEsleme |
| Zimmet (KKD, demirbaş), bakım, tutanak | mdlZimmet, frmZimmet, frmZimmetDuzelt | ZimmetMalzeme, Zimmet, ZimmetBakim |
| Eğitim ve sertifika | mdlEgitim, frmEgitim | EgitimTur, Egitim, EgitimKatilim |
| Disiplin (savunma, karar) | mdlDisiplin, frmDisiplin | Disiplin |
| Fazla mesai (onay akışı) | mdlMesai, frmMesai | FazlaMesai |
| Görevlendirme | mdlGorev, frmGorev | Gorevlendirme |
| Avans ve kesinti (onay, taksit) | mdlAvans, frmAvans | AvansKesinti, AvansTaksit |
| Raporlar, yönetici paneli | mdlRaporlar, mdlRapor | (tüm tablolar) |
| Dönem devri | mdlDonem | DonemDevri |
| Kullanıcılar, roller, modül yetkileri, oturum | mdlGuvenlik, frmGiris, frmKullanici, frmSifre, frmProfil | Kullanicilar, KullaniciYetki, GirisKayit, OturumKilit |
| Şema sürümü, veri yolu, ayarlar | mdlSema, mdlVeri, mdlVeriYolu, mdlVeritabani | SemaSurum, ProgramAyar |

## Aşamalar

1. **Veri aktarımı ve personel çekirdeği:** Access'ten içe aktarma (personel tüm alanları: cinsiyet, doğum tarihi, medeni hal, sicil, telefon, IBAN; tesisler; şirketler ve eşleşmeler; nakiller; çıkışlar). Personel kartının genişletilmesi, nakil, işe giriş kontrol listesi, personel işlem günlüğü. Çıkış talimatı (1.3.1) personel kartı ve İK işlemleriyle birleşir.
2. **Puantaj:** tesis/ay puantaj tablosu (aynı kodlar: Ç, HT, Yi, RT, Üİ, İİ…), resmi tatiller, notlar, manuel düzeltme, aylık özet. Doğrulama: programın hesapladığı aylık özetler Access'teki 485 `AylikOzet` satırıyla birebir karşılaştırılır.
3. **İzin ve bakiye motoru:** yıllık izin hakkı (kıdem/cinsiyet/yaş kuralları), hafta tatili bakiyesi ve sabitleme, devreden izin, görev/hasat izin kesintileri, kurallar ekranı, işlem geri alma. Doğrulama: Excel'deki "Kullanılması Gereken İzin" değerleriyle karşılaştırma.
4. **Belgeler:** özlük belge takibi, belge hatırlatma, İSG işyeri belgeleri, şirket ve personel sözleşmeleri, taranmış evrak ayırma, QR.
5. **Zimmet, eğitim/sertifika, disiplin, fazla mesai, görevlendirme, avans onay akışı** (mevcut avans modülüyle birleşir).
6. **Raporlar ve yönetici paneli, dönem devri, modül bazlı kullanıcı yetkileri.**

Her aşama sonunda: testler (Access verisiyle karşılaştırma dahil), Windows'ta otomatik kurulum/çalıştırma denetimi, yeni kurulum paketi.

## Notlar

- Kullanıcı şifreleri Access'te farklı bir özetleme yöntemiyle saklandığı için aktarılmaz; kullanıcılar aktarılır, ilk girişte yeni şifre belirlenir.
- Tesis başına ayrı puantaj tabloları (PuantajGunluk_<Tesis>) programda tek tabloda (tesis alanıyla) tutulur.
- İsimlerdeki fazla boşluklar her girişte otomatik düzeltilir (1.3.4).
