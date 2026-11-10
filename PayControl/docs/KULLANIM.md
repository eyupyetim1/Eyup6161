# PayControl — Kullanım Kılavuzu (1.3.1)

## Kurulum (Windows 10/11 x64)

1. .NET 8 Desktop Runtime veya SDK kurulu olsun (self-contained yayın kullanıyorsanız gerekmez).
2. `PayControl.exe` dosyasını bir klasöre kopyalayın.
3. Programı çalıştırın. İnternet gerekmez.

Veriler bilgisayarınızda saklanır:  
`C:\Users\<Kullanıcı>\AppData\Local\PayControl\paycontrol.db`

## Giriş

- Kullanıcı adı: **admin**
- Şifre: **Admin123!**  
İlk girişte yeni şifre belirlemeniz istenir. Şifrenizi sonradan sol alttaki kullanıcı kartındaki anahtar simgesinden değiştirebilirsiniz.

## Personel listesi

- Ana ekranda personelleri arayabilir (ad, TC, sicil), şirket/işyeri filtreleyebilirsiniz.
- Bir satıra çift tıklayarak personel kartını açın.

## Ana ekran

- **Genel bakış:** son 12 ayın bordro net / gerçekleşen ödeme grafiği (çubuğun üzerine gelince ayrıntı), tesis dağılımı, açık ve kritik uyarı sayısı.
- **Personeller:** arama ve şirket/tesis filtresi.
- Sol menü: Rapor Merkezi, Uyarı Merkezi, Bildirimler, Tesisler, Denetim Geçmişi, Ayarlar ve Yönetim (yalnız yönetici).

## Personel kartı sekmeleri

Hesap ekstresi, maaş geçmişi, avanslar, işe giriş/çıkış, çıkış ve SGK kontrolü, **İcra · BES · Tahsilat**, **Banka / kasa ödemeleri** (iptal), **Belgeler**.

- **İcra:** dosya no, icra dairesi, alacaklı, toplam borç, kesinti oranı ve sıra girin. Bordrodaki İCRA kolonu aktif dosyalara sırayla dağıtılır, kalan borç izlenir; ödeme kontrolü icra kesintisini bu dayanakla doğrular.
- **BES:** oran ve başlangıç; cayma tarihi işlenebilir.
- **Tahsilat planı:** kesinleşmiş fazla ödeme için taksit sayısı ve ilk ay. Bordroya **FAZLA ÖDEME KESİNTİSİ** kolonu eklendiğinde taksitlere işlenir ve geri ödeme borcundan düşer.
- **Ödeme iptali:** yanlış girilmiş banka/kasa ödemesini seçip nedenini yazın. Kayıt silinmez, ekstrede ters kayıt oluşur, doğru ödeme yeniden girilebilir. Kapalı dönemde yapılamaz.
- **Belgeler:** istifa dilekçesi, ibraname, icra yazısı gibi dosyalar eklenir; kopyası güvenli depoya alınır ve SHA-256 ile korunur. Belgeler silinmez, gerekçeyle geçersiz kılınır.

## Çıkış takibi (İK çıkışı unutursa)

Bir personelin çıkarılmasını İK'ya söylediğinizde bunu **Çıkış Takibi** ekranından (veya personel kartında **Çıkış talimatı ver**) kaydedin:

1. Personeli, **son çalışma gününü**, nedeni ve çıkışı yapacak kişiyi seçin → **Talimatı kaydet ve bildir**. Sorumluya e-posta gider (WhatsApp ayarlıysa WhatsApp da).
2. İK çıkış kaydını girince talimat otomatik olarak **Çıkış yapıldı** olur. İsterseniz **Çıkışı şimdi kaydet** ile kendiniz de yapabilirsiniz.
3. Son gün geçtiği hâlde çıkış yapılmadıysa satır kırmızı **Gecikti** olur ve kaç gündür beklediği yazar. Sorumluya her gün bir hatırlatma gider (program kapalıyken de çalışması için Ayarlar → Zamanlanmış görevler).
4. Alttaki tabloda **son çalışma gününden sonra yapılan her ödeme** görünür: ödeme tarihi, hangi ay için, banka/kasa, tutar, referans ve **ne için ödendiği**. Çıkış ayında yalnız çalışılan günlerin ücreti hak edilmiş sayılır; sonraki ayların ücreti tamamen hak edilmemiştir. Kırmızı sütun tahmini geri istenmesi gereken tutardır (kesinleştirmek için personel kartı → hesap ekstresi).
5. Çıkışı yapılmamış talimatı olan kişi **ödeme öncesi kontrolde kritik** olarak durdurulur; böylece unutulsa bile bir sonraki maaş listesinde yakalanır. Uyarı merkezinde de kritik uyarı olarak görünür.

**Yalnız sorunlular** filtresi gecikenleri ve son günden sonra ödeme yapılanları gösterir. Excel düğmesi listeyi rapor olarak kaydeder (Rapor Merkezi → Çıkış talimatı takibi).

## Tesisler (birden fazla tesis)

1. **Tesisler → Personelden oluştur** ile personel kartlarındaki işyeri adlarından tesisler açılır (bordro içe aktarırken de otomatik açılır). Sorumlu ve iletişim bilgilerini girin.
2. **Ödeme öncesi kontrolde** tesis seçin: yalnız o tesisin bordrosu, o tesisin banka dosyasıyla karşılaştırılır; her tesisin ayrı onaylı listesi olur.
3. **Kapanış → Tesis onayları:** her tesisin bordro net, ödenen, fark, kritik uyarı ve açık fark durumu görünür; tesis sorumlusu onaylar. Tüm tesisler onaylanmadan dönem kapanmaz.
4. Tüm raporlar tesis filtresiyle alınabilir; **Tesis karşılaştırma** raporu tesisleri yan yana gösterir.

## Rapor merkezi

Soldan rapor türünü seçin, tarih aralığı / tesis / şirket girip **Oluştur**'a basın. Önizleme tablosu, üstte özet göstergeler. **Excel / PDF / CSV** ile kaydedin veya sağdan kanal, biçim ve alıcıları seçip **Raporu gönder** ile e-posta / WhatsApp ile gönderin.

## Bildirimler (e-posta ve WhatsApp)

1. **Bağlantı ayarları** (yönetici): SMTP sunucusu, port, SSL, kullanıcı, şifre, gönderen. **Deneme e-postası gönder** ile doğrulayın.
   - Gmail: uygulama şifresi gerekir (smtp.gmail.com, 587). Office 365: smtp.office365.com, 587.
2. WhatsApp otomatik gönderim için Meta WhatsApp Business Cloud API: telefon numarası kimliği, kalıcı erişim anahtarı ve onaylı şablon (gövdede tek `{{1}}` değişkeni). Anahtar yoksa **WhatsApp'ta aç (elle)** mesajı hazır doldurulmuş sohbeti açar, siz gönderirsiniz.
3. **Alıcılar:** ad, e-posta, telefon, rol ve (isteğe bağlı) tesis.
4. **Otomatik kurallar:** zamanlanmış rapor (günlük / haftalık / aylık, gün ve saat), uyarı özeti veya olay (dönem kapandı, mutabakat farkı, yedek hatası). Tesis seçilen kuralda rapor o tesise süzülür ve yalnız o tesisin alıcılarına gider.
5. **Gönderim geçmişi:** her gönderim, durum ve hata. Başarısızlar otomatik olarak en fazla 3 kez yeniden denenir.

Otomatik işler PayControl açıkken 5 dakikada bir çalışır. Program kapalıyken de çalışması için **Ayarlar ve Yönetim → Zamanlanmış görevler → Windows görevini kaydet**.

## Uyarı merkezi ve denetim geçmişi

- **Uyarı merkezi:** kapanış, mutabakat, avans, çıkış, icra, tahsilat, yedek ve bildirim kaynaklı açık işler; tesis/seviye/kategori filtresi; satıra çift tıklayınca personel kartı açılır.
- **Denetim geçmişi:** tarih, kullanıcı, işlem, kayıt türü ve metin filtresi; eski/yeni değer ve neden; Excel'e aktarma. Kayıtlar değiştirilemez.

## Kullanıcı rolleri

| İşlem | Gerekli rol |
|-------|-------------|
| Ödeme kontrolü çalıştırma, avans, kasa/prim girişi | Herhangi bir oturum |
| Banka talimatı düzeltme, onaylı liste üretme, mutabakat, fark çözme, düzeltme kaydı, ters kayıt | Muhasebe veya Yönetici |
| Kritik satır için istisna, mutabakat farkı için istisna, yönetici kapanış onayı, dönemi kapatma / yeniden açma | Yönetici |
| Kullanıcı yönetimi, bildirim bağlantı ayarları, yedekten geri yükleme, veritabanı ayarı | Yönetici |
| Bildirim alıcı/kuralı, rapor gönderimi, icra/BES/tahsilat, belge, ödeme iptali | Muhasebe veya Yönetici |

Görüntüleyici rolü maaş tutarlarını göremez.

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

## Yedekleme ve geri yükleme

**Ayarlar ve Yönetim → Yedekleme**

- **Şimdi yedek al:** program açıkken tutarlı yedek alınır (`.pcyedek`). İçinde SHA-256 bütünlük özeti vardır.
- **Otomatik yedek:** günde bir kez; saklanacak yedek sayısı ayarlanabilir. Yedek klasörü olarak ağ sürücüsü veya harici disk önerilir.
- **Yedek şifresi:** verilirse yedekler AES-256-GCM ile şifrelenir. Şifreyi kaybederseniz şifreli yedek açılamaz.
- **Geri yükle (yönetici):** yedek doğrulanır, mevcut veri önce güvenlik yedeğine alınır, ardından geri yüklenip program yeniden başlar.

PostgreSQL kullanıyorsanız yedeği sunucuda `pg_dump` ile alın.

## Çok kullanıcılı kullanım (PostgreSQL)

**Ayarlar ve Yönetim → Veritabanı:** PostgreSQL sunucusu, port, veritabanı, kullanıcı ve şifre → **Bağlantıyı dene** → **Kaydet ve yeniden başlat**. Boş veritabanında tablolar otomatik oluşur. Her kullanıcının bilgisayarında aynı ayar yapılır; bağlantı bilgisi o bilgisayarda şifreli saklanır. Mevcut SQLite verisi sunucuya otomatik taşınmaz.

## Banka hesap hareketi dosyaları

Kapanış ekranında **Gerçekleşen ödemeleri içe aktar** Excel'in yanında **MT940** (`.sta`, `.940`, `.txt`) ve **CAMT.053** (`.xml`) dosyalarını da kabul eder; biçim içerikten algılanır. Yalnız giden (borç) hareketler alınır, ters kayıtlar atlanır; toplu ödemede her işlem ayrı satırdır. Eşleşme IBAN veya TC kimlik no ile yapılır.

## Güncelleme

**Ayarlar ve Yönetim → Güncelleme:** açılışta otomatik denetim, **Güncellemeleri denetle**, **İndir ve kur**. Paket SHA-256 ile doğrulanmadan kurulmaz; kurulumdan önce yedek alınır. Şirket içinde güncellemeyi ağ paylaşımından dağıtmak için bildirim adresine `\\sunucu\paylasim\guncelleme.json` yazın.

## Loglar

`%LocalAppData%/PayControl/logs/`
