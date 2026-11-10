# PayControl — Kullanım Kılavuzu (1.2)

## Kurulum (Windows 10/11 x64)

1. .NET 8 Desktop Runtime veya SDK kurulu olsun (self-contained yayın kullanıyorsanız gerekmez).
2. `PayControl.exe` dosyasını bir klasöre kopyalayın.
3. Programı çalıştırın. İnternet gerekmez.

Veriler bilgisayarınızda saklanır:  
`C:\Users\<Kullanıcı>\AppData\Local\PayControl\paycontrol.db`

## Giriş

- Kullanıcı adı: **admin**
- Şifre: **Admin123!**  
İlk girişten sonra şifreyi değiştirmeniz önerilir (sonraki sürümde arayüz eklenecek).

## Personel listesi

- Ana ekranda personelleri arayabilir (ad, TC, sicil), şirket/işyeri filtreleyebilirsiniz.
- Bir satıra çift tıklayarak personel kartını açın.

## Personel kartı sekmeleri

1. Genel Bilgiler  
2. Maaş Geçmişi (dönem dönem; eski aylar korunur)  
3–7. Sonraki sürümlerde doldurulacak (avans, banka, kesinti vb.)  
8. İşe Giriş/Çıkış Geçmişi (yeniden işe alımlar ayrı dönem)  
9. Evraklar (sonraki sürüm)  
10. Kontrol ve Denetim Geçmişi  

## Kullanıcı rolleri

| İşlem | Gerekli rol |
|-------|-------------|
| Ödeme kontrolü çalıştırma, avans, kasa/prim girişi | Herhangi bir oturum |
| Banka talimatı düzeltme, onaylı liste üretme, mutabakat, fark çözme, düzeltme kaydı, ters kayıt | Muhasebe veya Yönetici |
| Kritik satır için istisna, mutabakat farkı için istisna, yönetici kapanış onayı, dönemi kapatma / yeniden açma | Yönetici |

## Hesap ekstresi (personel kartı)

Personel kartında **Hesap Ekstresi** sekmesi işe girişten bugüne bütün mali hareketleri gösterir.

**Üst bölüm — dört ayrı bakiye (birbirine mahsup edilmez):**

* Toplam hak ediş / gerçekleşen toplam ödeme
* Ödenmemiş hak ediş — her hak ediş dönemi ayrı hesaplanır; bir aydaki fazla ödeme başka aydaki eksik ödemeyi gizlemez
* Açık avans borcu — avans kartındaki kalan borçla aynıdır
* İnceleme bekleyen fazla ödeme — mutabakatta bulunan, henüz kesinleşmemiş tutar
* Kesinleşmiş geri ödeme borcu — yalnızca "Fazla ödemeyi kesinleştir" işlemiyle oluşur

**Filtreler:** tarih aralığı, şirket, tesis, işlem türü ve çalışma dönemi. Filtreli ve genel (tüm zamanlar) bakiye ayrı gösterilir.

**Alt sekmeler:** *Mali hareketler* (tarih, dönem, tutar, açıklama, kaynak belge, kullanıcı, işlem kimliği ve her satırdaki yürüyen bakiyeler), *Dönem bakiyeleri* (ay bazında hak ediş/ödeme farkı), *Çalışma dönemleri* (yeniden işe alımlarda her dönem ayrı).

**Manuel işlem:** kasa ödemesi, bordro dışı prim/ikramiye/fazla mesai/tazminat/izin ücreti, iade, devreden alacak/borç, fazla ödemeyi kesinleştirme. Hatalı manuel hareket silinmez; **ters kayıt** alınır. Bordro, avans ve banka kaynaklı hareketler kendi modülünden iptal edilir (ör. bordro geri alma).

**Dışa aktarma:** Excel (ekstre, dönem bakiyeleri, çalışma dönemleri sayfaları) ve PDF (çok sayfalı, Türkçe karakterli). Çıktı ekrandaki filtreyi kullanır.

Ekstreye otomatik düşen kayıtlar: işe giriş/çıkış, maaş değişikliği, bordro hak edişi, bordro kesinti kalemleri (icra, BES, diğer), bordrodan avans mahsubu, avans, banka/kasa ödemeleri, mutabakat fazla ödemeleri, düzeltmeler, çıkış kaydında beyan edilen kıdem/ihbar/izin tutarları (bilgi satırı).

## Ödeme öncesi kontrol (bankaya göndermeden önce)

1. Ana ekranda **Ödeme Öncesi Kontrol**
2. Dönemi girin, **Bordro Seç** ile ayın bordro dosyasını seçin (daha önce yüklendiyse mevcut kayıt kullanılır; boş bırakılırsa dönemin son bordrosu kullanılır)
3. **Banka Dosyası Seç** ile bankaya göndereceğiniz Excel'i seçin; **Kontrol Et**
4. Satırlar sonuca göre sıralanır: Uygun, Eksik ödeme riski, Fazla ödeme riski, İşten çıkış kontrolü, IBAN uyuşmazlığı, Mükerrer ödeme riski, Bordro uyuşmazlığı, İnceleme gerekiyor
5. Hatalı satırı alttaki panelden **düzeltin** (IBAN, tutar) veya **listeden çıkarın** — gerekçe zorunludur, orijinal değer saklanır
6. Ödenmesi gerektiğine karar verdiğiniz kritik satır için **Yönetici** gerekçeli istisna verebilir. Kimlik veya ödeme sahibi belirsiz satırlara istisna verilemez
7. **Onaylı Liste Oluştur** — dosya `Belgelerim\PayControl\OnayliOdemeListeleri` altına, **yüklediğiniz banka dosyasının formatında** (üst bilgi satırları, başlık sırası, ek kolonlar) yazılır. İkinci sayfada kontrol bilgisi bulunur
8. Liste üretildikten sonra yapılan her düzeltme/istisna eski listeyi geçersiz kılar; yeni sürüm (v2, v3…) gerekir. Eski dosyalar silinmez
9. Kontrolden sonra veri değişirse (örn. bu arada kasa ödemesi girildi) liste üretilmez, sonuçlar güncellenir
10. Program bankaya kendiliğinden ödeme **göndermez**

Denetlenen kurallar: işten ayrılmış personele ödeme, çıkış ayı gün sayısı ve orantısı, aynı dönemin daha önce ödenmesi (banka/kasa veya önceki onaylı liste), aynı kişiye birden fazla talimat, IBAN kart uyumu ve kontrol hanesi, aynı IBAN'ın farklı kişilerde olması, banka-kart ad uyumu, bordro tutarı, avans mahsup planı, icra/BES sürekliliği, eksik gün, bordroda olmayan/banka listesinde olmayan kişiler, önceki dönemden devreden düzeltme, açık şüpheli fazla ödeme.

## Aylık kapanış ve mutabakat

1. Ana ekranda **Dönem Kapanış**, dönemi girip **Yükle**
2. Onaylı listeyi bankaya ilettiğinizde **Liste bankaya iletildi**
3. Banka hesap hareketi dökümünü **Gerçekleşen Banka Ödemelerini İçe Aktar** ile yükleyin (TARİH, TUTAR ve TC veya IBAN kolonları; dekont/referans no varsa mükerrer kontrolünde kullanılır). Aynı hareket ikinci kez kaydedilmez
4. **Mutabakat Yap** — bordro × onaylı liste × gerçekleşen ödeme karşılaştırılır. Farklar: eksik ödeme, fazla ödeme, bordrosuz ödeme, çıkış sonrası ödeme, listede olup ödenmeyen, liste dışı ödeme, eşleşmeyen banka hareketi. Fazla ödemeler personel ekstresinde "inceleme bekleyen" bakiyeye yazılır (kesin borç sayılmaz)
5. Her farkı **Çözüldü** (Muhasebe) veya **Gerekçeli İstisna** (Yönetici) olarak kapatın
6. Kontrol listesi maddelerinin çoğu veriyle otomatik doğrulanır ("Veri kontrolü" kolonu). Otomatik kontrolü başarısız madde ancak gerekçe yazılarak elle onaylanabilir; bu durum raporda istisna olarak görünür
7. **Yönetici kapanış onayı** maddesi diğer bütün maddeler tamamlanınca Yönetici tarafından verilir; ardından **Dönemi Kapat**. Açık fark veya çözülmemiş kritik uyarı varken dönem kapanmaz
8. Kapanışta kişi bazında anlık görüntü ve toplamlar dondurulur; sonradan değişen personel/ödeme kayıtları kapatılmış dönemin raporunu etkilemez
9. Kapatılmış döneme bordro, ödeme, kontrol ve mutabakat kaydı yazılamaz (veritabanı düzeyinde de engellidir). Düzeltme için **Düzeltmeler** sekmesinden düzeltme kaydı girin: eski kayıt değişmez, eski/yeni değer ve neden saklanır, sayısal fark sonraki açık döneme devreder ve o ayın ödeme kontrolünde uyarı olarak çıkar
10. Gerekirse Yönetici gerekçeyle **yeniden açar**; tekrar kapanış yeni yönetici onayı ister. Her kapanış/yeniden açma denetim geçmişinde ve "kapanış sayısı" ile izlenir
11. **Önceki ay karşılaştırması** sekmesi personel sayısı değişimini ve toplam farkın kaynaklarını (ayrılan, başlayan, maaş değişikliği, diğer) kişi bazında, dayandığı çıkış kaydı / çalışma dönemi / maaş değişikliği kaydı ile gösterir
12. **Kapanış Raporu (Excel)**: özet, kontrol listesi, farklar, istisnalar, karşılaştırma ve kapanış anlık görüntüsü

## Bordro içe aktarma

1. Ana ekranda **Bordro İçe Aktar**  
2. Excel dosyasını seçin (XLSX önerilir)  
3. Dönem yıl/ay girin  
4. **Önizle / Doğrula** — hatalı satırları kontrol edin  
5. **İçe Aktar**  

Aynı dosya tekrar yüklenirse mükerrer kayıt üretilmez.

Desteklenen başlıklar: T.C. KİMLİK NO, ADI SOYADI, İŞYERİ, GÖREVİ, MAAŞ (Net/Brüt), GİRİŞ/ÇIKIŞ TARİHİ, GÜN, NET İSTİHKAK, IBAN NO, AVANS, İCRA, OTO.KAT.BES, TRAFİK CEZASI, AİLE, İHBAR TAZMİNATI, YEMEK, YOL.

## İşten çıkış kaydı

1. Personel kartında **İşten Çıkış Kaydı Oluştur**  
2. Fiilî son çalışma ve çıkış tarihlerini girin  
3. SGK bildirgesi kutusunu **yalnızca gerçekten verildiyse** işaretleyin  
4. Kalan avans varsa sistem uyarı yazar  

**Önemli:** Çıkış ayı ücretinin sonraki ayda yatırılması tek başına hata değildir. Program hak ediş dönemi ile ödeme tarihini ayrı tutar.

## Yedekleme

1. PayControl kapalıyken  
2. `paycontrol.db` dosyasını kopyalayın  
3. Geri yüklemek için aynı konuma geri koyun  

Şifreli yedekleme sonraki sürümde gelecektir.

## Loglar

`%LocalAppData%/PayControl/logs/`
