using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Domain.Services;

/// <summary>Bankaya gönderilecek dosyanın bir satırı.</summary>
public sealed record BankaTalimatSatiri(
    string? TcKimlikNo,
    string? AdSoyad,
    string? Iban,
    decimal Tutar,
    int SatirNo);

/// <summary>Başka bir oturumda daha önce onaylı listeye alınmış ödeme.</summary>
public sealed record OncekiOnayliOdeme(Guid PersonelId, int Surum, decimal Tutar, string DosyaAdi);

/// <summary>Ödeme öncesi kontrolün ihtiyaç duyduğu tüm veriler — motor veritabanına erişmez.</summary>
public sealed class OdemeKontrolBaglami
{
    public int DonemYil { get; init; }
    public int DonemAy { get; init; }
    public IReadOnlyList<MaasDonemKaydi> Bordro { get; init; } = Array.Empty<MaasDonemKaydi>();
    public IReadOnlyList<BankaTalimatSatiri> Talimatlar { get; init; } = Array.Empty<BankaTalimatSatiri>();
    public IReadOnlyList<Personel> Personeller { get; init; } = Array.Empty<Personel>();
    /// <summary>Bu hak ediş dönemi için daha önce kaydedilmiş banka/kasa ödemeleri.</summary>
    public IReadOnlyList<BankaOdemeKaydi> DonemOdemeleri { get; init; } = Array.Empty<BankaOdemeKaydi>();
    public IReadOnlyList<IstenCikisKaydi> Cikislar { get; init; } = Array.Empty<IstenCikisKaydi>();
    public IReadOnlyList<AvansKaydi> Avanslar { get; init; } = Array.Empty<AvansKaydi>();
    public IReadOnlyList<MaasDonemKaydi> OncekiAyBordro { get; init; } = Array.Empty<MaasDonemKaydi>();
    public IReadOnlyDictionary<Guid, decimal> DevredenDuzeltmeler { get; init; } = new Dictionary<Guid, decimal>();
    public IReadOnlyDictionary<Guid, decimal> AcikSupheliBakiyeler { get; init; } = new Dictionary<Guid, decimal>();
    public IReadOnlyList<OncekiOnayliOdeme> OncekiOnayliOdemeler { get; init; } = Array.Empty<OncekiOnayliOdeme>();
    public IReadOnlyList<IcraDosyasi> IcraDosyalari { get; init; } = Array.Empty<IcraDosyasi>();
    public IReadOnlyList<BesTanimi> BesTanimlari { get; init; } = Array.Empty<BesTanimi>();
    public IReadOnlyList<TahsilatPlani> TahsilatPlanlari { get; init; } = Array.Empty<TahsilatPlani>();
    /// <summary>Henüz çıkış kaydı girilmemiş (bekleyen) çıkış talimatları.</summary>
    public IReadOnlyList<CikisTalimati> CikisTalimatlari { get; init; } = Array.Empty<CikisTalimati>();
}

/// <summary>Tek bir kontrol kuralının bulgusu.</summary>
public sealed record KontrolBulgusu(OdemeKontrolSonucu Sonuc, bool Kritik, bool Belirsiz, string Mesaj)
{
    public static KontrolBulgusu Bilgi(string m) => new(OdemeKontrolSonucu.Uygun, false, false, m);
}

/// <summary>
/// Bankaya göndermeden önce bordro × banka talimatı × personel kartı × geçmiş işlemler denetimi.
/// Her kural bir bulgu üretir; satırın sonucu en yüksek öncelikli bulgudan belirlenir.
/// </summary>
public sealed class OdemeOncesiKontrolMotoru
{
    private const decimal Tolerans = 0.01m;

    private static readonly OdemeKontrolSonucu[] Oncelik =
    [
        OdemeKontrolSonucu.BordroUyusmazligi,
        OdemeKontrolSonucu.IbanUyusmazligi,
        OdemeKontrolSonucu.MukerrerOdemeRiski,
        OdemeKontrolSonucu.IstenCikisKontrolu,
        OdemeKontrolSonucu.FazlaOdemeRiski,
        OdemeKontrolSonucu.EksikOdemeRiski,
        OdemeKontrolSonucu.IncelemeGerekiyor
    ];

    /// <summary>Eski imza (1.1) — geriye dönük uyumluluk.</summary>
    public IReadOnlyList<OdemeKontrolSatiri> Karsilastir(
        int donemYil,
        int donemAy,
        IReadOnlyList<MaasDonemKaydi> bordroKayitlari,
        IReadOnlyList<BankaTalimatSatiri> bankaTalimatlari,
        IReadOnlyList<Personel> personeller,
        IReadOnlyList<BankaOdemeKaydi> oncekiOdemeler,
        IReadOnlyList<IstenCikisKaydi> cikislar) =>
        Karsilastir(new OdemeKontrolBaglami
        {
            DonemYil = donemYil,
            DonemAy = donemAy,
            Bordro = bordroKayitlari,
            Talimatlar = bankaTalimatlari,
            Personeller = personeller,
            DonemOdemeleri = oncekiOdemeler,
            Cikislar = cikislar
        });

    public IReadOnlyList<OdemeKontrolSatiri> Karsilastir(OdemeKontrolBaglami b)
    {
        var donemBasi = new DateOnly(b.DonemYil, b.DonemAy, 1);
        var donemSonu = donemBasi.AddMonths(1).AddDays(-1);

        var personelById = b.Personeller.ToDictionary(p => p.Id);
        var personelByTc = b.Personeller.GroupBy(p => p.TcKimlikNo).ToDictionary(g => g.Key, g => g.First());
        var kartIbanSahipleri = b.Personeller
            .Where(p => !string.IsNullOrWhiteSpace(p.Iban))
            .GroupBy(p => IbanKurallari.Normalize(p.Iban)!)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());

        // 1) Banka satırlarını kişiye bağla: önce TC, TC yoksa kartta tek sahibi olan IBAN.
        var atanmis = new Dictionary<Guid, List<BankaTalimatSatiri>>();
        var eslesmeyen = new List<BankaTalimatSatiri>();
        var ibanIleEslesen = new HashSet<int>();
        foreach (var t in b.Talimatlar)
        {
            var tc = Rakam(t.TcKimlikNo);
            Personel? p = null;
            if (tc.Length > 0)
                personelByTc.TryGetValue(tc, out p);
            else if (IbanKurallari.Normalize(t.Iban) is { } ib && kartIbanSahipleri.TryGetValue(ib, out var sahipler) && sahipler.Count == 1)
            {
                p = personelById[sahipler[0]];
                ibanIleEslesen.Add(t.SatirNo);
            }

            if (p is null) eslesmeyen.Add(t);
            else
            {
                if (!atanmis.TryGetValue(p.Id, out var l)) atanmis[p.Id] = l = new List<BankaTalimatSatiri>();
                l.Add(t);
            }
        }

        // Banka listesinde aynı IBAN'a farklı kişiler için talimat
        var bankaIbanKisileri = b.Talimatlar
            .Where(t => IbanKurallari.Normalize(t.Iban) is not null)
            .GroupBy(t => IbanKurallari.Normalize(t.Iban)!)
            .ToDictionary(g => g.Key, g => g.Select(t => Rakam(t.TcKimlikNo) is { Length: > 0 } tc ? tc : $"#{t.SatirNo}:{t.AdSoyad}").Distinct().Count());

        var sonuc = new List<OdemeKontrolSatiri>();
        var bordroGruplari = b.Bordro.GroupBy(x => x.PersonelId).ToList();

        foreach (var grup in bordroGruplari)
        {
            personelById.TryGetValue(grup.Key, out var personel);
            var bordroListesi = grup.ToList();
            var bordro = bordroListesi.OrderByDescending(x => x.OlusturmaZamani).First();
            var talimatlar = atanmis.GetValueOrDefault(grup.Key) ?? new List<BankaTalimatSatiri>();
            var bulgular = new List<KontrolBulgusu>();

            if (bordroListesi.Count > 1)
                bulgular.Add(new(OdemeKontrolSonucu.MukerrerOdemeRiski, true, false,
                    $"Bu dönem için {bordroListesi.Count} ayrı bordro kaydı var (farklı içe aktarmalar). Bordro mükerrer olabilir."));

            var satir = YeniSatir(personel, bordro, talimatlar);

            if (talimatlar.Count == 0)
            {
                if (bordro.NetIstihkak <= Tolerans)
                    bulgular.Add(KontrolBulgusu.Bilgi("Net istihkak sıfır; ödeme beklenmiyor."));
                else
                    bulgular.Add(new(OdemeKontrolSonucu.EksikOdemeRiski, true, false,
                        $"Banka listesinde bulunmayan bordrolu personel. Bordro net istihkak {bordro.NetIstihkak:N2}."));
            }
            else
            {
                BankaKurallari(b, personel, bordro, talimatlar, kartIbanSahipleri, bankaIbanKisileri, ibanIleEslesen, bulgular);
            }

            if (personel is not null)
            {
                CalismaKurallari(b, personel, bordro, donemBasi, donemSonu, bulgular);
                GecmisOdemeKurallari(b, personel, bulgular);
                AvansKurallari(b, personel, bordro, donemSonu, bulgular);
                KesintiVeGunKurallari(b, personel, bordro, donemBasi, donemSonu, bulgular);
                IcraBesTahsilatKurallari(b, personel, bordro, donemBasi, donemSonu, bulgular);
                CikisTalimatiKurallari(b, personel, donemBasi, donemSonu, bulgular);
            }
            else
            {
                bulgular.Add(new(OdemeKontrolSonucu.BordroUyusmazligi, true, true, "Bordro satırının personel kartı bulunamadı."));
            }

            Uygula(satir, bulgular);
            sonuc.Add(satir);
        }

        // 2) Bordroda olmayan ama bankada olan kişiler
        var bordroKisileri = bordroGruplari.Select(g => g.Key).ToHashSet();
        foreach (var (pid, talimatlar) in atanmis.Where(kv => !bordroKisileri.Contains(kv.Key)))
        {
            var p = personelById[pid];
            var satir = YeniSatir(p, null, talimatlar);
            var bulgular = new List<KontrolBulgusu>
            {
                new(OdemeKontrolSonucu.BordroUyusmazligi, true, false,
                    $"Bordroda yer almayan kişi banka listesinde var ({talimatlar.Sum(t => t.Tutar):N2} TL).")
            };
            if (p.CalismaDurumu == CalismaDurumu.IstenAyrildi)
                bulgular.Add(new(OdemeKontrolSonucu.IstenCikisKontrolu, true, false,
                    $"Personel işten ayrılmış (son çıkış {p.SonCikisTarihi:dd.MM.yyyy})."));
            if (talimatlar.Count > 1)
                bulgular.Add(new(OdemeKontrolSonucu.MukerrerOdemeRiski, true, false, $"Aynı kişiye {talimatlar.Count} ayrı ödeme talimatı."));
            GecmisOdemeKurallari(b, p, bulgular);
            CikisTalimatiKurallari(b, p, donemBasi, donemSonu, bulgular);
            Uygula(satir, bulgular);
            sonuc.Add(satir);
        }

        // 3) Kimliği belirlenemeyen banka satırları — istisna ile onaylanamaz
        foreach (var t in eslesmeyen)
        {
            var satir = new OdemeKontrolSatiri
            {
                TcKimlikNo = Rakam(t.TcKimlikNo) is { Length: > 0 } tc ? tc : null,
                AdSoyad = t.AdSoyad,
                BankaAdSoyad = t.AdSoyad,
                BankaIban = IbanKurallari.Normalize(t.Iban),
                BankaOdemeTutari = t.Tutar,
                TalimatSatirNolari = t.SatirNo.ToString()
            };
            var mesaj = string.IsNullOrWhiteSpace(t.TcKimlikNo)
                ? "Banka satırında TC yok ve IBAN tek bir personel kartıyla eşleşmiyor. Ödeme sahibi belirsiz — onay engellenir."
                : "Banka listesinde olup personel kartında bulunamayan TC. Kimlik eşleşmesi belirsiz — onay engellenir.";
            Uygula(satir, [new(OdemeKontrolSonucu.BordroUyusmazligi, true, true, mesaj)]);
            sonuc.Add(satir);
        }

        return sonuc;
    }

    private static OdemeKontrolSatiri YeniSatir(Personel? personel, MaasDonemKaydi? bordro, List<BankaTalimatSatiri> talimatlar) => new()
    {
        PersonelId = personel?.Id ?? bordro?.PersonelId,
        TcKimlikNo = personel?.TcKimlikNo,
        AdSoyad = personel?.AdSoyad,
        KartAdSoyad = personel?.AdSoyad,
        BankaAdSoyad = talimatlar.FirstOrDefault()?.AdSoyad,
        BordroIban = IbanKurallari.Normalize(bordro?.Iban),
        BankaIban = IbanKurallari.Normalize(talimatlar.FirstOrDefault()?.Iban),
        KartIban = IbanKurallari.Normalize(personel?.Iban),
        BordroNetIstihkak = bordro?.NetIstihkak,
        BankaOdemeTutari = talimatlar.Count == 0 ? null : talimatlar.Sum(t => t.Tutar),
        TalimatSatirNolari = talimatlar.Count == 0 ? null : string.Join(",", talimatlar.Select(t => t.SatirNo))
    };

    private static void BankaKurallari(
        OdemeKontrolBaglami b, Personel? personel, MaasDonemKaydi bordro, List<BankaTalimatSatiri> talimatlar,
        Dictionary<string, List<Guid>> kartIbanSahipleri, Dictionary<string, int> bankaIbanKisileri,
        HashSet<int> ibanIleEslesen, List<KontrolBulgusu> bulgular)
    {
        if (talimatlar.Count > 1)
            bulgular.Add(new(OdemeKontrolSonucu.MukerrerOdemeRiski, true, false,
                $"Aynı kişiye {talimatlar.Count} ayrı ödeme talimatı (satır {string.Join(", ", talimatlar.Select(t => t.SatirNo))})."));

        var kartIban = IbanKurallari.Normalize(personel?.Iban) ?? IbanKurallari.Normalize(bordro.Iban);
        foreach (var t in talimatlar)
        {
            var bankaIban = IbanKurallari.Normalize(t.Iban);
            if (bankaIban is null)
            {
                bulgular.Add(new(OdemeKontrolSonucu.IbanUyusmazligi, true, false, $"Satır {t.SatirNo}: banka talimatında IBAN yok."));
                continue;
            }
            if (!IbanKurallari.GecerliMi(bankaIban))
                bulgular.Add(new(OdemeKontrolSonucu.IbanUyusmazligi, true, false, $"Satır {t.SatirNo}: IBAN geçersiz (kontrol hanesi/uzunluk hatalı): {bankaIban}."));

            if (kartIban is null)
                bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, true, false,
                    "Personel kartında ve bordroda IBAN yok; banka IBAN'ı doğrulanamıyor."));
            else if (!string.Equals(kartIban, bankaIban, StringComparison.OrdinalIgnoreCase))
            {
                var baskasininKarti = kartIbanSahipleri.TryGetValue(bankaIban, out var sahipler) && sahipler.Any(id => id != personel?.Id);
                bulgular.Add(new(OdemeKontrolSonucu.IbanUyusmazligi, true, baskasininKarti,
                    baskasininKarti
                        ? $"Banka IBAN'ı başka bir personelin kartında kayıtlı ({bankaIban}). Ödeme sahibi belirsiz."
                        : $"IBAN personel kartı ile uyuşmuyor. Kart: {kartIban}, banka: {bankaIban}."));
            }

            if (kartIbanSahipleri.TryGetValue(bankaIban, out var paylasanlar) && paylasanlar.Count > 1)
                bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, true, true,
                    $"Aynı IBAN {paylasanlar.Count} farklı personel kartında tanımlı. Ödeme sahibi belirsiz."));

            if (bankaIbanKisileri.TryGetValue(bankaIban, out var kisi) && kisi > 1)
                bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, true, true,
                    $"Banka listesinde aynı IBAN'a {kisi} farklı kişi için ödeme talimatı var."));

            if (!IbanKurallari.AdUyumluMu(t.AdSoyad, personel?.AdSoyad))
                bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, true, true,
                    $"Satır {t.SatirNo}: banka listesindeki ad ({t.AdSoyad}) personel kartıyla ({personel?.AdSoyad}) uyuşmuyor."));

            if (ibanIleEslesen.Contains(t.SatirNo))
                bulgular.Add(KontrolBulgusu.Bilgi($"Satır {t.SatirNo}: TC yok; kişi IBAN ve ad üzerinden eşleştirildi."));
        }

        var bankaToplam = talimatlar.Sum(t => t.Tutar);
        var fark = bankaToplam - bordro.NetIstihkak;
        if (fark > Tolerans)
            bulgular.Add(new(OdemeKontrolSonucu.FazlaOdemeRiski, true, false,
                $"Fazla ödeme riski: banka {bankaToplam:N2}, bordro {bordro.NetIstihkak:N2} (fark {fark:N2})."));
        else if (fark < -Tolerans)
            bulgular.Add(new(OdemeKontrolSonucu.EksikOdemeRiski, true, false,
                $"Eksik ödeme riski: banka {bankaToplam:N2}, bordro {bordro.NetIstihkak:N2} (fark {-fark:N2})."));
    }

    private static void CalismaKurallari(
        OdemeKontrolBaglami b, Personel personel, MaasDonemKaydi bordro, DateOnly donemBasi, DateOnly donemSonu,
        List<KontrolBulgusu> bulgular)
    {
        var donemler = personel.CalismaDonemleri.ToList();
        var cikislar = b.Cikislar.Where(c => c.PersonelId == personel.Id).OrderByDescending(c => c.IstenCikisTarihi).ToList();

        if (donemler.Count == 0)
        {
            var sonCikis = cikislar.FirstOrDefault();
            if (sonCikis is not null && sonCikis.IstenCikisTarihi < donemBasi)
                bulgular.Add(new(OdemeKontrolSonucu.IstenCikisKontrolu, true, false,
                    $"İşten ayrılmış personele ödeme planlanmış (çıkış {sonCikis.IstenCikisTarihi:dd.MM.yyyy})."));
            else if (personel.CalismaDurumu == CalismaDurumu.IstenAyrildi && sonCikis is null)
                bulgular.Add(new(OdemeKontrolSonucu.IstenCikisKontrolu, true, false,
                    "Personel kartı 'işten ayrıldı' durumunda ancak çıkış kaydı yok."));
            return;
        }

        var ortusen = donemler
            .Where(d => d.IseGirisTarihi <= donemSonu && (d.IstenCikisTarihi is null || d.IstenCikisTarihi >= donemBasi))
            .OrderByDescending(d => d.IseGirisTarihi)
            .ToList();

        if (ortusen.Count == 0)
        {
            var sonDonem = donemler.OrderByDescending(d => d.IseGirisTarihi).First();
            if (donemler.All(d => d.IseGirisTarihi > donemSonu))
                bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, true, false,
                    $"Personelin işe girişi ({donemler.Min(d => d.IseGirisTarihi):dd.MM.yyyy}) bu dönemden sonra; döneme ödeme beklenmez."));
            else
                bulgular.Add(new(OdemeKontrolSonucu.IstenCikisKontrolu, true, false,
                    $"İşten ayrılmış personele ödeme planlanmış: çıkış {sonDonem.IstenCikisTarihi:dd.MM.yyyy}, ödeme dönemi {donemBasi:yyyy-MM}."));
            return;
        }

        // Aynı dönemde yeniden girişi olan personel için en güncel dönem esas alınır.
        var aktif = ortusen.First();
        var cikisAyi = aktif.IstenCikisTarihi is { } ct && ct <= donemSonu;
        var girisAyi = aktif.IseGirisTarihi >= donemBasi;

        if (cikisAyi)
        {
            var cikisTarihi = aktif.IstenCikisTarihi!.Value;
            var bas = girisAyi ? aktif.IseGirisTarihi : donemBasi;
            var calisilan = cikisTarihi == donemSonu && !girisAyi ? 30m : Math.Min(30m, cikisTarihi.DayNumber - bas.DayNumber + 1);
            var cikisKaydi = cikislar.FirstOrDefault(c => c.CalismaDonemiId == aktif.Id) ?? cikislar.FirstOrDefault();

            if (bordro.Gun is { } gun && gun > calisilan + 0.5m)
                bulgular.Add(new(OdemeKontrolSonucu.FazlaOdemeRiski, true, false,
                    $"Çıkış ayı hak edişi hatalı olabilir: bordroda {G(gun)} gün, çıkış tarihine ({cikisTarihi:dd.MM.yyyy}) göre en fazla {G(calisilan)} gün."));
            else if (bordro.Gun is null)
                bulgular.Add(new(OdemeKontrolSonucu.IstenCikisKontrolu, false, false,
                    $"Çıkış ayı ({cikisTarihi:dd.MM.yyyy}); bordroda gün bilgisi yok, hak ediş günü doğrulanamadı."));
            else
                bulgular.Add(new(OdemeKontrolSonucu.IstenCikisKontrolu, false, false,
                    $"Çıkış ayı: {cikisTarihi:dd.MM.yyyy} çıkışlı personelin son hak edişi ({G(bordro.Gun)} gün) — gün sayısı çıkış tarihiyle uyumlu."));

            // Tam ay net tutarına göre orantı kontrolü (tazminat/izin kalemleri hariç)
            var onceki = b.OncekiAyBordro.FirstOrDefault(x => x.PersonelId == personel.Id);
            if (onceki is not null && (onceki.Gun is null or >= 30) && onceki.NetIstihkak > 0)
            {
                var ekKalemler = bordro.Kalemler
                    .Where(k => k.Tur is MaasKalemTuru.IhbarTazminati or MaasKalemTuru.KidemTazminati or MaasKalemTuru.IzinUcreti)
                    .Sum(k => k.Tutar);
                var gunBazli = bordro.Gun ?? calisilan;
                var ust = onceki.NetIstihkak * (gunBazli / 30m) * 1.15m + ekKalemler;
                if (bordro.NetIstihkak > ust + Tolerans)
                    bulgular.Add(new(OdemeKontrolSonucu.FazlaOdemeRiski, true, false,
                        $"Çıkış ayı net istihkakı ({bordro.NetIstihkak:N2}) önceki tam ay netine ({onceki.NetIstihkak:N2}) göre {G(gunBazli)} gün için beklenenden yüksek."));
            }

            if (cikisKaydi is not null)
            {
                if (cikisKaydi.SonBordroDonemYil.HasValue && cikisKaydi.SonBordroDonemAy.HasValue &&
                    new DateOnly(cikisKaydi.SonBordroDonemYil.Value, cikisKaydi.SonBordroDonemAy.Value, 1) < donemBasi)
                    bulgular.Add(new(OdemeKontrolSonucu.IstenCikisKontrolu, true, false,
                        $"Çıkış kaydında son ücret {cikisKaydi.SonBordroDonemYil}-{cikisKaydi.SonBordroDonemAy:D2} dönemine bağlanmış; bu dönemde ödeme beklenmez."));
                if (cikisKaydi.SgkCikisDurumu == SgkCikisDurumu.Belirsiz)
                    bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, false, false, "SGK işten ayrılış bildirgesi durumu belirsiz."));
            }
            else
            {
                bulgular.Add(new(OdemeKontrolSonucu.IstenCikisKontrolu, false, false,
                    "Çalışma döneminde çıkış tarihi var ancak işten çıkış kaydı (SGK/son ücret) oluşturulmamış."));
            }
        }
        else if (personel.CalismaDurumu == CalismaDurumu.IstenAyrildi && aktif.IstenCikisTarihi is null)
        {
            bulgular.Add(new(OdemeKontrolSonucu.IstenCikisKontrolu, true, false,
                "Personel kartı 'işten ayrıldı' durumunda ancak açık çalışma dönemi var; kayıtlar çelişkili."));
        }
    }

    /// <summary>
    /// Yönetici çıkış talimatı verdiği hâlde İK çıkış kaydını girmemişse: son günden sonrası için ücret ödenmemeli.
    /// Son gün dönemden önceyse tüm ay, dönem içindeyse son günden sonraki günler hak edilmez.
    /// </summary>
    private static void CikisTalimatiKurallari(OdemeKontrolBaglami b, Personel personel, DateOnly donemBasi, DateOnly donemSonu,
        List<KontrolBulgusu> bulgular)
    {
        var t = b.CikisTalimatlari
            .Where(x => x.PersonelId == personel.Id && x.Durum == CikisTalimatDurumu.Bekliyor && x.SonCalismaGunu < donemSonu)
            .OrderBy(x => x.SonCalismaGunu).FirstOrDefault();
        if (t is null) return;
        var kapsam = t.SonCalismaGunu < donemBasi
            ? "Bu dönemin tamamı son çalışma gününden sonra; ücret beklenmez."
            : $"Bu dönemde yalnız {t.SonCalismaGunu.Day} günlük ücret beklenir.";
        bulgular.Add(new(OdemeKontrolSonucu.IstenCikisKontrolu, true, false,
            $"ÇIKIŞ TALİMATI UYGULANMAMIŞ: {t.TalepZamani.ToLocalTime():dd.MM.yyyy} tarihinde {t.TalepEden} tarafından son çalışma günü " +
            $"{t.SonCalismaGunu:dd.MM.yyyy} olarak çıkış talimatı verildi ({t.Neden}) ama işten çıkış kaydı yapılmadı" +
            (string.IsNullOrWhiteSpace(t.Sorumlu) ? "" : $" (sorumlu: {t.Sorumlu})") + $". {kapsam}"));
    }

    private static void GecmisOdemeKurallari(OdemeKontrolBaglami b, Personel personel, List<KontrolBulgusu> bulgular)
    {
        var onceki = b.DonemOdemeleri.Where(o => o.PersonelId == personel.Id).ToList();
        if (onceki.Count > 0)
            bulgular.Add(new(OdemeKontrolSonucu.MukerrerOdemeRiski, true, false,
                $"Bu dönemin maaşı için daha önce {onceki.Sum(o => o.Tutar):N2} TL ödeme kaydı var " +
                $"({string.Join(", ", onceki.Select(o => $"{o.OdemeTarihi:dd.MM.yyyy} {o.OdemeKanali} {o.Tutar:N2}"))})."));

        var listeler = b.OncekiOnayliOdemeler.Where(o => o.PersonelId == personel.Id).ToList();
        if (listeler.Count > 0)
            bulgular.Add(new(OdemeKontrolSonucu.MukerrerOdemeRiski, true, false,
                $"Bu kişi bu dönem için daha önce onaylı ödeme listesine alınmış: " +
                string.Join(", ", listeler.Select(l => $"{l.DosyaAdi} v{l.Surum} ({l.Tutar:N2})")) + "."));
    }

    private static void AvansKurallari(OdemeKontrolBaglami b, Personel personel, MaasDonemKaydi bordro, DateOnly donemSonu,
        List<KontrolBulgusu> bulgular)
    {
        var avanslar = b.Avanslar.Where(a => a.PersonelId == personel.Id && a.AvansTarihi <= donemSonu).ToList();
        var kesinti = bordro.Kalemler.Where(k => k.Tur == MaasKalemTuru.AvansKesinti).Sum(k => k.Tutar);
        if (avanslar.Count == 0)
        {
            if (kesinti > Tolerans)
                bulgular.Add(new(OdemeKontrolSonucu.EksikOdemeRiski, true, false,
                    $"Bordroda {kesinti:N2} TL avans kesintisi var ancak personelin avans kaydı yok."));
            return;
        }

        bool OncekiDonem(AvansMahsupKaydi m) => m.DonemYil < b.DonemYil || (m.DonemYil == b.DonemYil && m.DonemAy < b.DonemAy);
        var acikOnce = avanslar.Sum(a => a.Tutar) -
                       avanslar.SelectMany(a => a.Mahsuplar).Where(m => m.GerceklesenTutar.HasValue && OncekiDonem(m)).Sum(m => m.GerceklesenTutar!.Value);
        var planlanan = Math.Min(acikOnce,
            avanslar.SelectMany(a => a.Mahsuplar).Where(m => m.DonemYil == b.DonemYil && m.DonemAy == b.DonemAy).Sum(m => m.PlanlananTutar));

        if (planlanan > Tolerans && kesinti + Tolerans < planlanan)
            bulgular.Add(new(OdemeKontrolSonucu.FazlaOdemeRiski, true, false,
                $"Avans mahsubu eksik: bu dönem planlanan {planlanan:N2}, bordroya yansıyan {kesinti:N2}. Fark kadar fazla ödeme riski."));
        else if (kesinti > acikOnce + Tolerans)
            bulgular.Add(new(OdemeKontrolSonucu.EksikOdemeRiski, true, false,
                $"Bordrodaki avans kesintisi ({kesinti:N2}) açık avans borcunu ({acikOnce:N2}) aşıyor."));
        else if (kesinti > 0)
            bulgular.Add(KontrolBulgusu.Bilgi($"Avans mahsubu {kesinti:N2} (açık borç {acikOnce:N2}, plan {planlanan:N2}) — uyumlu."));
    }

    private static void KesintiVeGunKurallari(OdemeKontrolBaglami b, Personel personel, MaasDonemKaydi bordro,
        DateOnly donemBasi, DateOnly donemSonu, List<KontrolBulgusu> bulgular)
    {
        var onceki = b.OncekiAyBordro.FirstOrDefault(x => x.PersonelId == personel.Id);
        foreach (var (tur, ad) in new[] { (MaasKalemTuru.Icra, "İcra"), (MaasKalemTuru.OtoKatBes, "BES") })
        {
            var bu = bordro.Kalemler.Where(k => k.Tur == tur).Sum(k => k.Tutar);
            var once = onceki?.Kalemler.Where(k => k.Tur == tur).Sum(k => k.Tutar) ?? 0m;
            if (onceki is not null && once > 0 && bu == 0)
                bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, false, false,
                    $"Önceki ay {ad} kesintisi ({once:N2}) vardı, bu ay bordroda yok. Kesinti sona erdiyse belgeyle doğrulayın."));
            else if (onceki is not null && once == 0 && bu > 0)
                bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, false, false,
                    $"Bu ay yeni {ad} kesintisi başlamış ({bu:N2}). Dayanak belgeyi kontrol edin."));
            else if (bu > 0)
                bulgular.Add(KontrolBulgusu.Bilgi($"{ad} kesintisi {bu:N2} (önceki ay {once:N2})."));
        }

        var girisCikisVar = personel.CalismaDonemleri.Any(d =>
            (d.IseGirisTarihi >= donemBasi && d.IseGirisTarihi <= donemSonu) ||
            (d.IstenCikisTarihi is { } c && c >= donemBasi && c <= donemSonu));
        if (bordro.Gun is { } gun && gun > 0 && gun < 30 && !girisCikisVar)
            bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, false, false,
                $"Eksik çalışma günü: {G(gun)} gün (ay içinde giriş/çıkış yok). Ücretsiz izin / devamsızlık belgesini kontrol edin."));

        if (b.DevredenDuzeltmeler.TryGetValue(personel.Id, out var duzeltme) && Math.Abs(duzeltme) > Tolerans)
            bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, false, false,
                $"Önceki dönemden devreden düzeltme: {duzeltme:N2} TL. Bu ayın bordrosuna/ödemesine yansıdığını doğrulayın."));

        if (b.AcikSupheliBakiyeler.TryGetValue(personel.Id, out var supheli) && supheli > Tolerans)
            bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, false, false,
                $"Personelin inceleme bekleyen fazla ödeme bakiyesi var: {supheli:N2} TL."));
    }

    private static void IcraBesTahsilatKurallari(OdemeKontrolBaglami b, Personel personel, MaasDonemKaydi bordro,
        DateOnly donemBasi, DateOnly donemSonu, List<KontrolBulgusu> bulgular)
    {
        // İcra: dayanak dosyaya göre kesinti var mı, tutar makul mü, borcu aşıyor mu
        var icraKesinti = bordro.Kalemler.Where(k => k.Tur == MaasKalemTuru.Icra).Sum(k => k.Tutar);
        var dosyalar = b.IcraDosyalari.Where(d => d.PersonelId == personel.Id && d.Baslangic <= donemSonu).ToList();
        if (dosyalar.Count > 0)
        {
            // Bu dönem için zaten işlenmiş kesintiler (bordro içe aktarılırken) önceki bakiyeye geri eklenir
            decimal BuDonem(IcraDosyasi d) => d.Kesintiler.Where(k => !k.IptalEdildi && k.DonemYil == b.DonemYil && k.DonemAy == b.DonemAy).Sum(k => k.Tutar);
            var acik = dosyalar.Where(d => d.Aktif || BuDonem(d) > 0).ToList();
            var kalanOnce = acik.Sum(d => d.ToplamBorc - d.Kesilen + BuDonem(d));
            if (kalanOnce > Tolerans && icraKesinti <= Tolerans)
                bulgular.Add(new(OdemeKontrolSonucu.FazlaOdemeRiski, true, false,
                    $"Aktif icra dosyası var ({string.Join(", ", acik.Select(d => d.DosyaNo))}, kalan {kalanOnce:N2}) ancak bordroda icra kesintisi yok. Kesilmeyen icra işverenin sorumluluğundadır."));
            else if (icraKesinti > kalanOnce + Tolerans)
                bulgular.Add(new(OdemeKontrolSonucu.EksikOdemeRiski, true, false,
                    $"İcra kesintisi ({icraKesinti:N2}) dosyalardaki kalan borcu ({kalanOnce:N2}) aşıyor."));
            else if (icraKesinti > 0)
            {
                var oran = acik.OrderBy(d => d.Sira).First().KesintiOrani;
                var beklenen = Math.Min(kalanOnce, Math.Round((bordro.NetIstihkak + icraKesinti) * oran, 2));
                if (Math.Abs(icraKesinti - beklenen) > Math.Max(1m, beklenen * 0.05m))
                    bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, false, false,
                        $"İcra kesintisi {icraKesinti:N2}; dosya oranına ({oran:P0}) göre beklenen yaklaşık {beklenen:N2}."));
                else
                    bulgular.Add(KontrolBulgusu.Bilgi($"İcra kesintisi dosya oranıyla uyumlu ({icraKesinti:N2}, kalan borç {kalanOnce - icraKesinti:N2})."));
            }
        }
        else if (icraKesinti > Tolerans)
            bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, false, false, $"Bordroda {icraKesinti:N2} icra kesintisi var ancak tanımlı icra dosyası yok."));

        // BES: otomatik katılım tanımına göre kesinti
        var bes = b.BesTanimlari.FirstOrDefault(t => t.PersonelId == personel.Id && t.Baslangic <= donemSonu &&
                                                     (t.CaymaTarihi is null || t.CaymaTarihi > donemBasi));
        var besKesinti = bordro.Kalemler.Where(k => k.Tur == MaasKalemTuru.OtoKatBes).Sum(k => k.Tutar);
        if (bes is not null && besKesinti <= Tolerans)
            bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, false, false, $"BES tanımı var (oran {bes.Oran:P1}) ancak bordroda BES kesintisi yok."));
        else if (bes is not null && bordro.MaasTuru == MaasTuru.Brut && bordro.MaasTutar is { } brut && brut > 0)
        {
            var beklenen = Math.Round(brut * ((bordro.Gun ?? 30m) / 30m) * bes.Oran, 2);
            if (Math.Abs(besKesinti - beklenen) > Math.Max(1m, beklenen * 0.02m))
                bulgular.Add(new(OdemeKontrolSonucu.IncelemeGerekiyor, false, false, $"BES kesintisi {besKesinti:N2}; brüt ücrete göre beklenen {beklenen:N2}."));
        }
        else if (bes is null && besKesinti > Tolerans && b.BesTanimlari.Any(t => t.PersonelId == personel.Id && t.CaymaTarihi <= donemBasi))
            bulgular.Add(new(OdemeKontrolSonucu.EksikOdemeRiski, true, false, $"Personel BES'ten caymış ancak bordroda {besKesinti:N2} BES kesintisi var."));

        // Fazla ödeme tahsilat planı
        var planli = b.TahsilatPlanlari.Where(p => p.PersonelId == personel.Id)
            .SelectMany(p => p.Taksitler).Where(t => t.DonemYil == b.DonemYil && t.DonemAy == b.DonemAy).Sum(t => t.Planlanan);
        var tahsilat = bordro.Kalemler.Where(k => k.Tur == MaasKalemTuru.GeriOdemeKesinti).Sum(k => k.Tutar);
        if (planli > Tolerans && tahsilat + Tolerans < planli)
            bulgular.Add(new(OdemeKontrolSonucu.FazlaOdemeRiski, true, false,
                $"Fazla ödeme tahsilat taksiti ({planli:N2}) bordroya yansımamış (bordroda {tahsilat:N2})."));
    }

    private static void Uygula(OdemeKontrolSatiri satir, List<KontrolBulgusu> bulgular)
    {
        var kritikler = bulgular.Where(x => x.Kritik).ToList();
        var uyarilar = bulgular.Where(x => !x.Kritik && x.Sonuc != OdemeKontrolSonucu.Uygun).ToList();

        satir.KritikMi = kritikler.Count > 0;
        satir.EslesmeBelirsiz = bulgular.Any(x => x.Belirsiz);
        satir.Sonuc = kritikler.Count > 0
            ? Oncelik.First(o => kritikler.Any(k => k.Sonuc == o))
            : uyarilar.Count > 0
                ? Oncelik.First(o => uyarilar.Any(k => k.Sonuc == o))
                : OdemeKontrolSonucu.Uygun;

        var metin = bulgular
            .OrderByDescending(x => x.Kritik)
            .Select(x => (x.Kritik ? "[KRİTİK] " : x.Sonuc == OdemeKontrolSonucu.Uygun ? "" : "[UYARI] ") + x.Mesaj)
            .ToList();
        satir.Gerekce = metin.Count == 0 ? "Bordro, banka talimatı ve personel kartı uyumlu." : string.Join(" ", metin);
        satir.Onaylandi = !satir.KritikMi;
    }

    private static string G(decimal? d) => d?.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) ?? "-";

    private static string Rakam(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());
}
