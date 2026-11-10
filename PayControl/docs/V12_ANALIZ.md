# PayControl 1.2 — Veri Modeli ve Etki Analizi (Bölüm 16–18)

Bu doküman, 1.1 sürümündeki hesap ekstresi / ödeme öncesi kontrol / dönem kapanış
kodunun gereksinimlere karşı incelenmesini ve 1.2'de yapılan değişikliklerin
mevcut modüllere etkisini kaydeder.

## 1. 1.1 sürümünde tespit edilen eksikler

1.1'de 29 test geçiyordu ancak aşağıdaki davranışlar gereksinimi karşılamıyordu.

### Hesap ekstresi (16)
| # | Bulgu | Sonuç |
|---|-------|-------|
| E1 | Bordrodaki avans kesintisi mali harekete `AvansMahsup` yazılıyor, avans kartındaki `KalanBorc` ve mahsup planı güncellenmiyordu. Aynı ay elle mahsup yapılırsa defterde çift mahsup oluşuyordu. | Avans kartı ile ekstre farklı bakiye gösteriyordu |
| E2 | Bordro geri alma `MaasDonemKaydi` kayıtlarını fiziksel siliyor, ama hak ediş mali hareketlerini bırakıyordu. | Ekstrede geri alınmış bordronun hak edişi görünüyordu |
| E3 | Şirket / tesis filtresi DTO'da vardı ama uygulanmıyordu; `MaliHareket` üzerinde bu alanlar yoktu. | Filtre çalışmıyordu |
| E4 | Avans, banka ödemesi ve düzeltme hareketlerine `CalismaDonemiId` atanmıyordu. | Çalışma dönemine göre ekstre eksikti |
| E5 | İşe giriş/çıkış, maaş değişikliği, kasa ödemesi, iade, devreden bakiye, prim/ikramiye/fazla mesai (bordro dışı), fazla ödeme inceleme/kesinleştirme için hiçbir kayıt yolu yoktu. | Bu satırlar ekstrede hiç oluşmuyordu |
| E6 | Ödenmemiş hak ediş tek global farkla hesaplanıyordu: bir aydaki fazla ödeme başka aydaki eksik ödemeyi gizliyordu. | Bakiyeler karışıyordu |
| E7 | PDF ilk 80 satırdan sonrasını sessizce kesiyordu; Türkçe karakterler ASCII'ye dönüştürülüyordu. | Uzun ekstreler eksik basılıyordu |
| E8 | Düzeltme, farkı değil yeni tutarı `Bilgi` bakiyesine yazıyordu. | Düzeltme bakiyeye yansımıyordu |

### Ödeme öncesi kontrol (17)
| # | Bulgu |
|---|-------|
| K1 | TC'si olmayan banka satırları sessizce yok sayılıyordu (raporlanmıyor, onaylanmıyordu). |
| K2 | İşten ayrılmış her personel (yeniden işe alınmış olsa bile) kritik işaretleniyordu; çıkış ayı hak edişi kontrol edilmiyordu. |
| K3 | Avans mahsubu, icra/BES, eksik gün ve devreden düzeltme kuralları yalnızca açıklama metni üretiyordu, karar vermiyordu. |
| K4 | Aynı IBAN'ın banka listesinde farklı TC'lere gitmesi, banka IBAN'ının başka personelin kartına ait olması, ad uyuşmazlığı ve IBAN kontrol hanesi denetlenmiyordu. |
| K5 | Sonuç kodu, kuralların sırasına göre birbirinin üzerine yazılıyordu. |
| K6 | İstisna onayı için yetki kontrolü yoktu. |
| K7 | Kullanıcının kaydı düzeltip yeniden kontrol etme imkânı yoktu. |
| K8 | Bordro dosyası oturuma bağlanmıyordu; banka formatı sabitti (yüklenen dosyaya uyarlanmıyordu). |
| K9 | Onaylı listenin hangi satırlardan üretildiği saklanmıyordu; sürüm numarası oturum bazlıydı (aynı dönemde yeni oturum tekrar v1 üretiyordu); listeden sonra yapılan değişiklik eski listeyi geçersiz kılmıyordu. |
| K10 | Kontrol ile liste üretimi arasında veri değişirse (örn. bu arada ödeme kaydı girildi) fark edilmiyordu. |

### Kapanış (18)
| # | Bulgu |
|---|-------|
| D1 | `CozulmemisUyariSayisi` hiç hesaplanmıyordu (her zaman 0) — kapanış açık farkları görmüyordu. |
| D2 | Ekran her yüklemede `OzetHesapla` çağırıyordu; kapatılmış dönemin toplamları sonradan değişen kayıtlarla yeniden hesaplanıyordu. |
| D3 | Gerçekleşen banka ödemelerini içe aktaran bir servis ve mutabakat yoktu. |
| D4 | Kontrol listesi tamamen elle işaretleniyordu; hiçbir madde veriyle doğrulanmıyordu. |
| D5 | Kapalı dönem kilidi yalnızca üç serviste kontrol ediliyordu; bordro geri alma kapalı dönemin kayıtlarını silebiliyordu. Veritabanı katmanında koruma yoktu. |
| D6 | Durum geçişleri serbestti (her durumdan her duruma). Kapatma/yeniden açma için yetki kontrolü yoktu. |
| D7 | Önceki ay karşılaştırması yalnızca sayı veriyordu; kişi bazında ve kaynak kayda dayalı neden yoktu. |
| D8 | Açık avans toplamı tüm zamanların toplamıydı, dönem sonu bakiyesi değildi. Çıkış sonrası şüpheli ödeme alanı hiç dolmuyordu. |

## 2. Veri modeli değişiklikleri

| Varlık | Değişiklik |
|--------|-----------|
| `MaliHareket` | `Sirket`, `Isyeri`, `IlgiliHareketId` (ters kayıt bağı). **Değiştirilemez / silinemez** (DbContext koruması). |
| `MaliIslemTuru` | `IseGiris`, `IstenCikis`, `TersKayit`, `FazlaOdemeKesinlesti`, `FazlaOdemeAciklandi`, `BordroKalemBilgisi` eklendi |
| `MaasDonemKaydi` | `IptalEdildi`, `IptalZamani`, `IptalEden`, `IptalNedeni`, `CalismaDonemiId`, `Sirket` — geri alma artık yumuşak iptal; global sorgu filtresi iptalleri gizler |
| `BankaOdemeKaydi` | `OdemeKanali` (Banka/Kasa), `TekillikAnahtari` (benzersiz), `IceAktarmaId`, `KaynakTuru`, `TcKimlikNo`, `AdSoyad` |
| `OdemeKontrolOturumu` | `BordroIceAktarmaId`, `BankaSablonJson`, `SonDegisiklikZamani` |
| `OdemeTalimatSatiri` (yeni) | Banka dosyasının ham satırları, orijinal/düzeltilmiş IBAN-tutar, hariç bayrağı, düzelten kullanıcı |
| `OdemeKontrolSatiri` | `TalimatSatirNolari`, `KartAdSoyad`, `BankaAdSoyad` |
| `OnayliOdemeListesi` | `BordroIceAktarmaId`, `BordroDosyaAdi`, `Gecerli`, `GecersizlikNedeni`, `YerineGecenListeId` |
| `OnayliOdemeListesiSatiri` (yeni) | Listeye giren her onaylı kayıt ve istisna bilgisi |
| `MutabakatFarki` (yeni) | Banka mutabakatı farkları; durum Açık / Çözüldü / İstisna |
| `DonemKapanisAnlikGoruntusu` (yeni) | Kapanış anındaki kişi bazında beklenen/ödenen/fark |
| `DonemKapanisOzeti` (yeni) | Her kapanışın (yeniden açma sonrası tekrar kapanış dahil) toplamları |
| `MaasDonemKapanisi` | `KapanisSayisi`, `MutabakatZamani` |
| `DonemKontrolMaddesi` | `OtomatikSonuc`, `OtomatikAciklama`, `ElleOnaylandi` |
| `DonemDuzeltmeKaydi` | `UygulanacakDonemYil/Ay`, `FarkTutari` |

Mevcut veritabanları (`paycontrol-v11.db`) silinmez: `SemaYukseltici` eksik tablo ve
kolonları başlangıçta ekler.

## 3. Mevcut modüllere etkiler

* **Personel**: oluşturma → `IseGiris` hareketi; maaş değişikliği → `MaasDegisikligi`
  hareketi; yeni `YenidenIseAlAsync` → yeni çalışma dönemi + `IseGiris` hareketi.
* **Bordro içe aktarma**: avans kesintisi `AvansServisi` üzerinden açık avanslara FIFO
  mahsup edilir; geri alma yumuşak iptal + ters kayıt + avans bakiyesi iadesi; kapalı
  dönemde engellenir; eski çıkışlı personel yeni giriş tarihiyle gelirse yeni çalışma dönemi açılır.
* **İşten çıkış**: `IstenCikis` bilgi hareketi; beyan edilen kıdem/ihbar/izin tutarları
  bilgi satırı olarak ekstreye düşer (bordroya dahil olduklarından hak edişe ikinci kez eklenmez).
* **Banka ödemesi**: tek kayıt veya Excel ekstre içe aktarma; tekillik anahtarı ile mükerrer engeli.
* **Yetki**: istisna onayı, dönem kapatma/yeniden açma, yönetici onay maddesi → Yönetici;
  onaylı liste üretimi, düzeltme, mutabakat → Muhasebe veya Yönetici.

## 4. Bilinen sınırlar (bilinçli olarak yapılmayanlar)

* İcra ve BES kesintilerinin **tutar doğruluğu** için kaynak veri (icra dosyası, BES
  oranı) sistemde yok. Kontrol, önceki aya göre süreklilik (kesinti kayboldu / yeni başladı)
  üzerinden uyarı verir; tutarı doğrulamaz.
* Çıkış ayı hak edişi brüt-net hesabı yapılmaz; bordro gün sayısı çıkış tarihine göre
  denetlenir, tutar önceki ayın tam ayına göre orantı sapması ile uyarılır.
* Program bankaya bağlanmaz ve ödeme göndermez; yalnızca dosya üretir.
