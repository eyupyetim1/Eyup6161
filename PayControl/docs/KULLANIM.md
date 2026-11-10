# PayControl — Kullanım Kılavuzu (V1)

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
