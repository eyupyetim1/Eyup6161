using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Domain.Services;

public sealed class CikisDenetimServisi : ICikisDenetimServisi
{
    public CikisOdemeIncelemeSonucu OdemeleriIncele(
        IstenCikisKaydi cikis,
        MaasDonemKaydi? ilgiliHakEdis,
        IReadOnlyList<BankaOdemeKaydi> ayniHakEdisOdemeleri)
    {
        var beklenen = ilgiliHakEdis?.NetIstihkak;
        var toplamOdenen = ayniHakEdisOdemeleri.Sum(x => x.Tutar);
        var fark = (beklenen ?? 0m) == 0m && toplamOdenen == 0m
            ? (decimal?)null
            : toplamOdenen - (beklenen ?? 0m);

        // Ödeme yok
        if (ayniHakEdisOdemeleri.Count == 0)
        {
            return new CikisOdemeIncelemeSonucu
            {
                Siniflandirma = OdemeSiniflandirma.NormalHakEdisOdemesi,
                Gerekce = "İlgili hak ediş için banka ödemesi henüz yok.",
                IncelenenTutar = 0,
                BeklenenHakEdis = beklenen,
                ToplamOdenen = 0,
                Fark = fark,
                KullaniciOnayiGerekli = false
            };
        }

        // Çıkış sonrası sonraki dönemde yatan tek seferlik eşleşen ödeme → normal olabilir
        if (beklenen.HasValue &&
            ayniHakEdisOdemeleri.Count == 1 &&
            Math.Abs(toplamOdenen - beklenen.Value) < 0.01m)
        {
            var odeme = ayniHakEdisOdemeleri[0];
            var odemeCikistanSonra = odeme.OdemeTarihi > cikis.IstenCikisTarihi;
            return new CikisOdemeIncelemeSonucu
            {
                Siniflandirma = OdemeSiniflandirma.NormalHakEdisOdemesi,
                Gerekce = odemeCikistanSonra
                    ? "Çıkış sonrası yatan tutar, ilgili dönem hak edişi ile eşleşiyor; tek başına hata değildir. Bordro-banka eşleşmesi kontrol edilmeli."
                    : "Ödeme tutarı ilgili dönem net istihkakı ile eşleşiyor.",
                IncelenenTutar = toplamOdenen,
                BeklenenHakEdis = beklenen,
                ToplamOdenen = toplamOdenen,
                Fark = 0,
                KullaniciOnayiGerekli = false
            };
        }

        // Aynı yükümlülüğe beklenenden fazla ödeme
        if (beklenen.HasValue && toplamOdenen > beklenen.Value + 0.01m)
        {
            var fazla = toplamOdenen - beklenen.Value;
            return new CikisOdemeIncelemeSonucu
            {
                Siniflandirma = OdemeSiniflandirma.BrutSupheliOdeme,
                Gerekce = $"Aynı hak ediş yükümlülüğüne karşılık beklenen {beklenen.Value:N2} TL iken toplam {toplamOdenen:N2} TL ödenmiş. Olası fazla: {fazla:N2} TL. Önce başka hak ediş/tazminata ait olup olmadığı araştırılmalı.",
                IncelenenTutar = fazla,
                BeklenenHakEdis = beklenen,
                ToplamOdenen = toplamOdenen,
                Fark = fazla,
                KullaniciOnayiGerekli = true
            };
        }

        // Beklenenden az
        if (beklenen.HasValue && toplamOdenen + 0.01m < beklenen.Value)
        {
            return new CikisOdemeIncelemeSonucu
            {
                Siniflandirma = OdemeSiniflandirma.NormalHakEdisOdemesi,
                Gerekce = $"Eksik ödeme olabilir. Beklenen: {beklenen.Value:N2}, ödenen: {toplamOdenen:N2}. Avans/kesinti/parçalı ödeme kontrol edilmeli.",
                IncelenenTutar = beklenen.Value - toplamOdenen,
                BeklenenHakEdis = beklenen,
                ToplamOdenen = toplamOdenen,
                Fark = toplamOdenen - beklenen.Value,
                KullaniciOnayiGerekli = true
            };
        }

        // Beklenen yok ama ödeme var
        return new CikisOdemeIncelemeSonucu
        {
            Siniflandirma = OdemeSiniflandirma.BrutSupheliOdeme,
            Gerekce = "Hak ediş kaydı bulunamadı ancak banka ödemesi mevcut. Eşleştirme ve belge incelemesi gerekir.",
            IncelenenTutar = toplamOdenen,
            BeklenenHakEdis = null,
            ToplamOdenen = toplamOdenen,
            Fark = null,
            KullaniciOnayiGerekli = true
        };
    }

    public void SgkCikisOnayla(
        IstenCikisKaydi cikis,
        DateOnly bildirgeTarihi,
        string onaylayanKullanici,
        string? belgeYolu)
    {
        if (string.IsNullOrWhiteSpace(onaylayanKullanici))
            throw new InvalidOperationException("SGK çıkışı için onaylayan kullanıcı zorunludur.");

        if (string.IsNullOrWhiteSpace(belgeYolu) && string.IsNullOrWhiteSpace(onaylayanKullanici))
            throw new InvalidOperationException("SGK çıkışı yalnızca onay veya belge ile kaydedilir.");

        cikis.SgkCikisDurumu = SgkCikisDurumu.Verildi;
        cikis.SgkBildirgeTarihi = bildirgeTarihi;
        cikis.SgkOnaylayanKullanici = onaylayanKullanici;
        cikis.SgkBelgeYolu = belgeYolu;
    }
}
