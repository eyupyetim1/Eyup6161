using System.Security.Cryptography;
using System.Text;
using PayControl.Application.Interfaces;
using PayControl.Infrastructure.Import;

namespace PayControl.Tests.Modules;

public class BankaEkstreBicimTestleri : TestOrtami
{
    private const string GecerliTc = "10000000078"; // kontrol haneleri geçerli sentetik TC

    [Fact]
    public void Tc_kontrol_hanesi_dogrulanir()
    {
        Assert.True(BankaHareketOkuyucu.TcGecerli(GecerliTc));
        Assert.False(BankaHareketOkuyucu.TcGecerli("10000000079"));
        Assert.False(BankaHareketOkuyucu.TcGecerli("01234567890"));
    }

    [Fact]
    public void Mt940_yalniz_borc_hareketlerini_okur_yapilandirilmis_ve_serbest_86_alanini_cozer()
    {
        var iban1 = Iban(501);
        var mt = $"""
            {"{1:F01BANKTRISAXXX0000000000}{2:I940BANKTRISXXXXN}{4:"}
            :20:STMT20261005
            :25:TR330006100519786457841326
            :28C:00001/001
            :60F:C261004TRY1500000,00
            :61:2610051005D21000,00NTRFMAAS-0001//B26100500001
            :86:?20EKIM MAAS ODEMESI?31{iban1}?32AHMET YILMAZ
            :61:2610051005D18000,50NTRFNONREF//B26100500002
            :86:MAAS ODEMESI {GecerliTc} MEHMET KAYA IBAN {Iban(502)[..4]} {Iban(502)[4..]}
            :61:2610061006C5000,00NTRFIADE
            :86:GELEN HAVALE
            :61:2610061006RD21000,00NTRFMAAS-0001
            :86:TERS KAYIT
            :62F:C261006TRY1466000,00
            {"-}"}
            """;
        var satirlar = BankaHareketOkuyucu.Oku(new MemoryStream(Encoding.UTF8.GetBytes(mt)));
        Assert.Equal(2, satirlar.Count);
        var a = satirlar[0];
        Assert.Equal(new DateOnly(2026, 10, 5), a.Tarih);
        Assert.Equal(21000m, a.Tutar);
        Assert.Equal("MAAS-0001", a.ReferansNo);
        Assert.Equal(iban1, a.Iban);
        Assert.Equal("AHMET YILMAZ", a.AdSoyad);
        var b = satirlar[1];
        Assert.Equal(18000.50m, b.Tutar);
        Assert.Equal("B26100500002", b.ReferansNo); // NONREF → banka referansı
        Assert.Equal(GecerliTc, b.TcKimlikNo);
        Assert.Equal(Iban(502), b.Iban);
    }

    [Fact]
    public void Camt053_toplu_odemeyi_islem_bazinda_okur_alacak_ve_ters_kayitlari_atlar()
    {
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.08"><BkToCstmrStmt><Stmt><Id>S1</Id>
              <Ntry><Amt Ccy="TRY">39000.00</Amt><CdtDbtInd>DBIT</CdtDbtInd><Sts><Cd>BOOK</Cd></Sts>
                <BookgDt><Dt>2026-10-05</Dt></BookgDt><AcctSvcrRef>TOPLU-77</AcctSvcrRef>
                <NtryDtls>
                  <TxDtls><Refs><EndToEndId>E2E-1</EndToEndId></Refs><Amt Ccy="TRY">21000.00</Amt>
                    <RltdPties><Cdtr><Pty><Nm>Ahmet Yılmaz</Nm></Pty></Cdtr><CdtrAcct><Id><IBAN>{Iban(601)}</IBAN></Id></CdtrAcct></RltdPties>
                    <RmtInf><Ustrd>EKIM MAAS</Ustrd></RmtInf></TxDtls>
                  <TxDtls><Refs><EndToEndId>NOTPROVIDED</EndToEndId><AcctSvcrRef>B-2</AcctSvcrRef></Refs><AmtDtls><TxAmt><Amt Ccy="TRY">18000.00</Amt></TxAmt></AmtDtls>
                    <RltdPties><Cdtr><Nm>Ayşe Demir</Nm><Id><PrvtId><Othr><Id>{GecerliTc}</Id></Othr></PrvtId></Id></Cdtr></RltdPties></TxDtls>
                </NtryDtls></Ntry>
              <Ntry><Amt Ccy="TRY">500.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><BookgDt><Dt>2026-10-05</Dt></BookgDt></Ntry>
              <Ntry><Amt Ccy="TRY">21000.00</Amt><CdtDbtInd>DBIT</CdtDbtInd><RvslInd>true</RvslInd><BookgDt><Dt>2026-10-06</Dt></BookgDt></Ntry>
            </Stmt></BkToCstmrStmt></Document>
            """;
        var satirlar = BankaHareketOkuyucu.Oku(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
        Assert.Equal(2, satirlar.Count);
        Assert.Equal((21000m, "E2E-1", Iban(601), "Ahmet Yılmaz"), (satirlar[0].Tutar, satirlar[0].ReferansNo, satirlar[0].Iban, satirlar[0].AdSoyad));
        Assert.Equal((18000m, "B-2", GecerliTc), (satirlar[1].Tutar, satirlar[1].ReferansNo, satirlar[1].TcKimlikNo));
        Assert.All(satirlar, s => Assert.Equal(new DateOnly(2026, 10, 5), s.Tarih));
    }

    [Fact]
    public async Task Mt940_dosyasi_mutabakat_icin_ice_aktarilir_ve_ibanla_eslesir()
    {
        var p = await PersonelAsync("Ekstre Personeli", 30000, 700);
        await BordroYukleAsync(2026, 9, new BordroSatir(p, 21000m));
        var mt = $":20:X\n:25:TR1\n:61:2610051005D21000,00NTRFREF-1\n:86:?20MAAS?31{p.Iban}?32EKSTRE PERSONELI\n:61:2610051005D21000,00NTRFREF-1\n:86:?31{p.Iban}\n";
        var bytes = Encoding.UTF8.GetBytes(mt);
        Assert.Equal(BankaHareketOkuyucu.Bicim.Mt940, BankaHareketOkuyucu.BicimAlgila(bytes));
        var satirlar = BankaHareketOkuyucu.Oku(new MemoryStream(bytes));
        var sonuc = await S<IBankaOdemeServisi>().HareketleriIceAktarAsync(2026, 9, satirlar, "ekstre.sta", Convert.ToHexString(SHA256.HashData(bytes)), Muhasebe);
        Assert.Equal(1, sonuc.Eklenen);
        Assert.Equal(1, sonuc.MukerrerAtlanan); // aynı referans
        Assert.Equal(0, sonuc.EslesmeyenKayit);
    }

    [Fact]
    public void Taninmayan_bicim_ve_bozuk_xml_anlasilir_hata_verir()
    {
        Assert.Throws<InvalidOperationException>(() => BankaHareketOkuyucu.Oku(new MemoryStream(Encoding.UTF8.GetBytes("merhaba dünya"))));
        var ex = Assert.Throws<InvalidOperationException>(() => BankaHareketOkuyucu.Oku(new MemoryStream(Encoding.UTF8.GetBytes("<Document><Stmt>"))));
        Assert.Contains("XML", ex.Message);
    }
}
