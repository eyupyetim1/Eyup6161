using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Domain.Enums;

namespace PayControl.Desktop.ViewModels;

public partial class IstenCikisViewModel : ObservableObject
{
    private readonly IIstenCikisServisi _servis;
    private readonly OturumServisi _oturum;

    public Guid PersonelId { get; set; }

    [ObservableProperty]
    private string _personelAd = string.Empty;

    [ObservableProperty]
    private DateTime _fiiliSonCalisma = DateTime.Today;

    [ObservableProperty]
    private DateTime _istenCikisTarihi = DateTime.Today;

    [ObservableProperty]
    private string? _cikisNedeni;

    [ObservableProperty]
    private int? _sonBordroYil = DateTime.Today.Year;

    [ObservableProperty]
    private int? _sonBordroAy = DateTime.Today.Month;

    [ObservableProperty]
    private SonMaasOdemeDurumu _sonMaasDurumu = SonMaasOdemeDurumu.Belirsiz;

    [ObservableProperty]
    private decimal? _kalanAvans;

    [ObservableProperty]
    private decimal? _izinUcreti;

    [ObservableProperty]
    private decimal? _kidem;

    [ObservableProperty]
    private decimal? _ihbar;

    [ObservableProperty]
    private string? _aciklama;

    [ObservableProperty]
    private bool _sgkVerildiOnay;

    [ObservableProperty]
    private DateTime? _sgkBildirgeTarihi;

    [ObservableProperty]
    private string? _hata;

    [ObservableProperty]
    private string? _uyari;

    [ObservableProperty]
    private bool _kaydedildi;

    public Array SonMaasDurumlari => Enum.GetValues(typeof(SonMaasOdemeDurumu));

    public IstenCikisViewModel(IIstenCikisServisi servis, OturumServisi oturum)
    {
        _servis = servis;
        _oturum = oturum;
    }

    [RelayCommand]
    private async Task KaydetAsync()
    {
        Hata = null;
        Uyari = null;
        try
        {
            if (KalanAvans is > 0)
                Uyari = $"Uyarı: Kapanmamış avans borcu {KalanAvans:N2} TL.";

            var dto = new IstenCikisOlusturDto
            {
                PersonelId = PersonelId,
                FiiliSonCalismaTarihi = DateOnly.FromDateTime(FiiliSonCalisma),
                IstenCikisTarihi = DateOnly.FromDateTime(IstenCikisTarihi),
                CikisNedeni = CikisNedeni,
                SonBordroDonemYil = SonBordroYil,
                SonBordroDonemAy = SonBordroAy,
                SonMaasOdemeDurumu = SonMaasDurumu,
                KalanAvans = KalanAvans,
                IzinUcreti = IzinUcreti,
                KidemTazminati = Kidem,
                IhbarTazminati = Ihbar,
                Aciklama = Aciklama,
                // SGK Verildi yalnızca açık onay ile
                SgkKullaniciOnayi = SgkVerildiOnay,
                SgkCikisDurumu = SgkVerildiOnay ? SgkCikisDurumu.Verildi : SgkCikisDurumu.Belirsiz,
                SgkBildirgeTarihi = SgkVerildiOnay && SgkBildirgeTarihi.HasValue
                    ? DateOnly.FromDateTime(SgkBildirgeTarihi.Value)
                    : null
            };

            await _servis.OlusturAsync(dto, _oturum.KullaniciAdi ?? "sistem");

            var hesap = await _servis.CikisAyiHakEdisHesaplaAsync(
                PersonelId,
                DateOnly.FromDateTime(IstenCikisTarihi),
                bordroOnayli: false);

            if (!hesap.NihaiSonucMu)
                Uyari = (Uyari is null ? "" : Uyari + " ") + hesap.UyariMesaji;

            Kaydedildi = true;
        }
        catch (Exception ex)
        {
            Hata = ex.Message;
        }
    }
}
