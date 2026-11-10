using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Domain.Services;

public sealed record BankaTalimatSatiri(
    string? TcKimlikNo,
    string? AdSoyad,
    string? Iban,
    decimal Tutar,
    int SatirNo);

public sealed class OdemeOncesiKontrolMotoru
{
    public IReadOnlyList<OdemeKontrolSatiri> Karsilastir(
        int donemYil,
        int donemAy,
        IReadOnlyList<MaasDonemKaydi> bordroKayitlari,
        IReadOnlyList<BankaTalimatSatiri> bankaTalimatlari,
        IReadOnlyList<Personel> personeller,
        IReadOnlyList<BankaOdemeKaydi> oncekiOdemeler,
        IReadOnlyList<IstenCikisKaydi> cikislar)
    {
        var sonuc = new List<OdemeKontrolSatiri>();
        var personelByTc = personeller
            .GroupBy(p => p.TcKimlikNo)
            .ToDictionary(g => g.Key, g => g.First());

        var ibanGruplari = personeller
            .Where(p => !string.IsNullOrWhiteSpace(p.Iban))
            .GroupBy(p => p.Iban!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var bankaByTc = bankaTalimatlari
            .Where(b => !string.IsNullOrWhiteSpace(b.TcKimlikNo))
            .GroupBy(b => NormalizeTc(b.TcKimlikNo!))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Aynı IBAN ile birden fazla talimat
        var bankaIbanCift = bankaTalimatlari
            .Where(b => !string.IsNullOrWhiteSpace(b.Iban))
            .GroupBy(b => NormalizeIban(b.Iban!))
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var bordro in bordroKayitlari)
        {
            var personel = personeller.FirstOrDefault(p => p.Id == bordro.PersonelId);
            var tc = personel?.TcKimlikNo ?? "";
            bankaByTc.TryGetValue(tc, out var talimatlar);
            talimatlar ??= new List<BankaTalimatSatiri>();

            var bankaToplam = talimatlar.Sum(t => t.Tutar);
            var bankaIban = talimatlar.FirstOrDefault()?.Iban;
            var kartIban = personel?.Iban ?? bordro.Iban;

            var satir = new OdemeKontrolSatiri
            {
                PersonelId = bordro.PersonelId,
                TcKimlikNo = tc,
                AdSoyad = personel?.AdSoyad,
                BordroIban = bordro.Iban,
                BankaIban = bankaIban,
                KartIban = kartIban,
                BordroNetIstihkak = bordro.NetIstihkak,
                BankaOdemeTutari = talimatlar.Count == 0 ? null : bankaToplam
            };

            Degerlendir(satir, personel, bordro, talimatlar, oncekiOdemeler, cikislar, donemYil, donemAy, ibanGruplari, bankaIbanCift);
            sonuc.Add(satir);
        }

        // Bordroda yok, banka listesinde var
        var bordroTcSet = bordroKayitlari
            .Select(b => personeller.FirstOrDefault(p => p.Id == b.PersonelId)?.TcKimlikNo)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .ToHashSet();

        foreach (var grp in bankaByTc)
        {
            if (bordroTcSet.Contains(grp.Key))
                continue;

            personelByTc.TryGetValue(grp.Key, out var p);
            var satir = new OdemeKontrolSatiri
            {
                PersonelId = p?.Id,
                TcKimlikNo = grp.Key,
                AdSoyad = p?.AdSoyad ?? grp.Value.First().AdSoyad,
                BankaIban = grp.Value.First().Iban,
                KartIban = p?.Iban,
                BankaOdemeTutari = grp.Value.Sum(x => x.Tutar),
                BordroNetIstihkak = null,
                Sonuc = OdemeKontrolSonucu.BordroUyusmazligi,
                KritikMi = true,
                EslesmeBelirsiz = p is null,
                Gerekce = p is null
                    ? "Banka listesinde olup bordroda ve personel kartında bulunamayan kayıt. Kimlik eşleşmesi belirsiz — onay engellenir."
                    : "Bordroda yer almayan kişi banka listesinde var."
            };
            sonuc.Add(satir);
        }

        return sonuc;
    }

    private static void Degerlendir(
        OdemeKontrolSatiri satir,
        Personel? personel,
        MaasDonemKaydi bordro,
        List<BankaTalimatSatiri> talimatlar,
        IReadOnlyList<BankaOdemeKaydi> oncekiOdemeler,
        IReadOnlyList<IstenCikisKaydi> cikislar,
        int donemYil,
        int donemAy,
        HashSet<string> paylasilanIbanlar,
        HashSet<string> bankaCiftIban)
    {
        var gerekceler = new List<string>();

        if (talimatlar.Count == 0)
        {
            satir.Sonuc = OdemeKontrolSonucu.EksikOdemeRiski;
            satir.KritikMi = true;
            satir.Gerekce = "Banka listesinde bulunmayan bordrolu personel.";
            return;
        }

        if (talimatlar.Count > 1)
        {
            gerekceler.Add($"Aynı kişiye {talimatlar.Count} ayrı ödeme talimatı.");
            satir.Sonuc = OdemeKontrolSonucu.MukerrerOdemeRiski;
            satir.KritikMi = true;
        }

        var cikis = cikislar.Where(c => c.PersonelId == bordro.PersonelId)
            .OrderByDescending(c => c.IstenCikisTarihi)
            .FirstOrDefault();

        if (personel?.CalismaDurumu == CalismaDurumu.IstenAyrildi || cikis is not null)
        {
            gerekceler.Add("İşten ayrılmış personele ödeme planlanmış — çıkış ayı hak edişi kontrol edilmeli.");
            if (satir.Sonuc == OdemeKontrolSonucu.Uygun || satir.Sonuc == default)
                satir.Sonuc = OdemeKontrolSonucu.IstenCikisKontrolu;
            satir.KritikMi = true;
        }

        var oncekiAyniDonem = oncekiOdemeler
            .Where(o => o.PersonelId == bordro.PersonelId &&
                        o.HakEdisDonemYil == donemYil &&
                        o.HakEdisDonemAy == donemAy)
            .Sum(o => o.Tutar);

        if (oncekiAyniDonem > 0)
        {
            gerekceler.Add($"Bu dönem için daha önce {oncekiAyniDonem:N2} TL ödeme kaydı var — mükerrer ödeme riski.");
            satir.Sonuc = OdemeKontrolSonucu.MukerrerOdemeRiski;
            satir.KritikMi = true;
        }

        var bankaIban = NormalizeIban(talimatlar.First().Iban ?? "");
        var kartIban = NormalizeIban(personel?.Iban ?? bordro.Iban ?? "");
        if (!string.IsNullOrEmpty(kartIban) && !string.IsNullOrEmpty(bankaIban) &&
            !string.Equals(kartIban, bankaIban, StringComparison.OrdinalIgnoreCase))
        {
            gerekceler.Add("IBAN personel kartı / bordro kaydı ile uyuşmuyor.");
            satir.Sonuc = OdemeKontrolSonucu.IbanUyusmazligi;
            satir.KritikMi = true;
        }

        if (!string.IsNullOrEmpty(bankaIban) && paylasilanIbanlar.Contains(bankaIban))
        {
            gerekceler.Add("Aynı IBAN birden fazla personel kartında tanımlı.");
            satir.Sonuc = OdemeKontrolSonucu.IncelemeGerekiyor;
            satir.KritikMi = true;
            satir.EslesmeBelirsiz = true;
        }

        if (!string.IsNullOrEmpty(bankaIban) && bankaCiftIban.Contains(bankaIban) && talimatlar.Count == 1)
        {
            gerekceler.Add("Aynı IBAN banka listesinde birden fazla satırda.");
            satir.KritikMi = true;
            satir.Sonuc = OdemeKontrolSonucu.MukerrerOdemeRiski;
        }

        var fark = (satir.BankaOdemeTutari ?? 0m) - bordro.NetIstihkak;
        if (Math.Abs(fark) > 0.01m)
        {
            if (fark > 0)
            {
                gerekceler.Add($"Fazla ödeme riski: banka {satir.BankaOdemeTutari:N2}, bordro {bordro.NetIstihkak:N2}.");
                if (!satir.KritikMi || satir.Sonuc == OdemeKontrolSonucu.Uygun)
                    satir.Sonuc = OdemeKontrolSonucu.FazlaOdemeRiski;
                satir.KritikMi = true;
            }
            else
            {
                gerekceler.Add($"Eksik ödeme riski: banka {satir.BankaOdemeTutari:N2}, bordro {bordro.NetIstihkak:N2}.");
                if (satir.Sonuc is OdemeKontrolSonucu.Uygun or 0)
                    satir.Sonuc = OdemeKontrolSonucu.EksikOdemeRiski;
            }
        }

        if (bordro.Gun is < 30 and > 0)
            gerekceler.Add($"Eksik çalışma günü: {bordro.Gun}.");

        var avansKalem = bordro.Kalemler.FirstOrDefault(k => k.Tur == MaasKalemTuru.AvansKesinti);
        if (avansKalem is not null)
            gerekceler.Add($"Bordroda avans mahsupu: {avansKalem.Tutar:N2} TL (doğrulama gerekli).");

        var icra = bordro.Kalemler.FirstOrDefault(k => k.Tur == MaasKalemTuru.Icra);
        var bes = bordro.Kalemler.FirstOrDefault(k => k.Tur == MaasKalemTuru.OtoKatBes);
        if (icra is not null) gerekceler.Add($"İcra: {icra.Tutar:N2}");
        if (bes is not null) gerekceler.Add($"BES: {bes.Tutar:N2}");

        if (!satir.KritikMi && Math.Abs(fark) <= 0.01m && talimatlar.Count == 1)
        {
            satir.Sonuc = OdemeKontrolSonucu.Uygun;
            satir.Gerekce = gerekceler.Count == 0 ? "Bordro ve banka talimatı uyumlu." : string.Join(" ", gerekceler);
            return;
        }

        if (satir.Sonuc == default)
            satir.Sonuc = OdemeKontrolSonucu.IncelemeGerekiyor;

        satir.Gerekce = string.Join(" ", gerekceler);
        if (personel is null)
        {
            satir.EslesmeBelirsiz = true;
            satir.KritikMi = true;
        }
    }

    private static string NormalizeTc(string tc) => new string(tc.Where(char.IsDigit).ToArray());
    private static string NormalizeIban(string iban) =>
        new string(iban.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
}
