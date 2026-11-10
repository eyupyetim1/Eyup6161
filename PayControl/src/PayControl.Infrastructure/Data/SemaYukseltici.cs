using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Serilog;

namespace PayControl.Infrastructure.Data;

/// <summary>
/// EnsureCreated mevcut veritabanını güncellemez. Bu sınıf, önceki sürümle oluşturulmuş
/// SQLite dosyasına eksik tabloları, kolonları ve indeksleri ekler; hiçbir veriyi silmez.
/// </summary>
public static class SemaYukseltici
{
    public static async Task<IReadOnlyList<string>> YukseltAsync(PayControlDbContext db, CancellationToken ct = default)
    {
        var yapilan = new List<string>();
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        var mevcutTablolar = await ListeAsync(conn, "SELECT name FROM sqlite_master WHERE type='table'", ct);
        var mevcutIndeksler = await ListeAsync(conn, "SELECT name FROM sqlite_master WHERE type='index'", ct);

        var script = db.Database.GenerateCreateScript();
        var ifadeler = script.Split(";", StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        // 1) Eksik tablolar
        foreach (var ifade in ifadeler.Where(s => s.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase)))
        {
            var ad = Regex.Match(ifade, "CREATE TABLE \"([^\"]+)\"").Groups[1].Value;
            if (mevcutTablolar.Contains(ad, StringComparer.OrdinalIgnoreCase))
                continue;
            await CalistirAsync(conn, ifade, ct);
            yapilan.Add($"Tablo eklendi: {ad}");
            mevcutTablolar.Add(ad);
        }

        // 2) Mevcut tablolarda eksik kolonlar
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var tablo = entity.GetTableName();
            if (tablo is null) continue;
            var store = StoreObjectIdentifier.Table(tablo, entity.GetSchema());
            var kolonlar = (await ListeAsync(conn, $"PRAGMA table_info(\"{tablo}\")", ct, kolonIndeksi: 1))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            object? ornek = null;
            try { ornek = Activator.CreateInstance(entity.ClrType); } catch { /* varsayılan yok */ }

            foreach (var prop in entity.GetProperties())
            {
                var kolon = prop.GetColumnName(store);
                if (kolon is null || kolonlar.Contains(kolon)) continue;

                var tip = prop.GetColumnType();
                var sql = $"ALTER TABLE \"{tablo}\" ADD COLUMN \"{kolon}\" {tip}";
                if (!prop.IsNullable)
                {
                    var varsayilan = prop.PropertyInfo is not null && ornek is not null
                        ? prop.PropertyInfo.GetValue(ornek)
                        : null;
                    sql += $" NOT NULL DEFAULT {SqlDegeri(varsayilan, prop.ClrType)}";
                }
                await CalistirAsync(conn, sql, ct);
                yapilan.Add($"Kolon eklendi: {tablo}.{kolon}");
            }
        }

        // 3) Eksik indeksler
        foreach (var ifade in ifadeler.Where(s => s.Contains("INDEX", StringComparison.OrdinalIgnoreCase) &&
                                                  s.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase)))
        {
            var ad = Regex.Match(ifade, "INDEX \"([^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrEmpty(ad) || mevcutIndeksler.Contains(ad, StringComparer.OrdinalIgnoreCase))
                continue;
            try
            {
                await CalistirAsync(conn, ifade, ct);
                yapilan.Add($"İndeks eklendi: {ad}");
            }
            catch (Exception ex)
            {
                // Eski veride benzersizlik ihlali varsa indeks atlanır; uygulama katmanı kontrolü devam eder.
                Log.Warning(ex, "İndeks oluşturulamadı: {Indeks}", ad);
                yapilan.Add($"İndeks atlandı (mevcut veri uyumsuz): {ad}");
            }
        }

        foreach (var y in yapilan)
            Log.Information("Şema yükseltme: {Islem}", y);
        return yapilan;
    }

    private static string SqlDegeri(object? deger, Type tip)
    {
        var t = Nullable.GetUnderlyingType(tip) ?? tip;
        if (deger is null)
        {
            if (t == typeof(string)) return "''";
            if (t == typeof(Guid)) return "'00000000-0000-0000-0000-000000000000'";
            return "0";
        }
        return deger switch
        {
            bool b => b ? "1" : "0",
            string s => "'" + s.Replace("'", "''") + "'",
            Guid g => "'" + g.ToString().ToUpperInvariant() + "'",
            Enum e => Convert.ToInt32(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            decimal d => "'" + d.ToString(CultureInfo.InvariantCulture) + "'",
            DateTime dt => "'" + dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'",
            DateOnly d => "'" + d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "'",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => "0"
        };
    }

    private static async Task CalistirAsync(System.Data.Common.DbConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<string>> ListeAsync(
        System.Data.Common.DbConnection conn, string sql, CancellationToken ct, int kolonIndeksi = 0)
    {
        var list = new List<string>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(r.GetString(kolonIndeksi));
        return list;
    }
}
