using System.Data;
using Stokin.Application;
using Stokin.Application.Common;
namespace Stokin.Infrastructure;

public sealed partial class SqlStokinRepository
{
    public Task<DashboardView> DashboardAsync(SessionContext s, CancellationToken ct = default) => db.TransactionAsync(async w =>
    {
        var role = await Authorize(w, s, activity: false); var t = await w.TableAsync("SELECT COUNT(*) Total,COALESCE(SUM(CASE WHEN b.Quantity>0 AND b.Quantity<=p.Minimum THEN 1 ELSE 0 END),0) Low,COALESCE(SUM(CASE WHEN b.Quantity=0 THEN 1 ELSE 0 END),0) OutStock,COALESCE(SUM(b.InventoryValue),0) Value FROM Products p JOIN StockBalances b ON b.ProductId=p.Id WHERE p.IsActive=1"); var r = t.Rows[0];
        int cats = Convert.ToInt32(await w.ScalarAsync("SELECT COUNT(*) FROM Categories WHERE IsActive=1")); var priority = await w.TableAsync("SELECT TOP(100) p.Id,p.SKU,p.Name Nama,b.Quantity Stok,u.Name Satuan,p.Minimum,CASE WHEN b.Quantity=0 THEN N'Habis' ELSE N'Menipis' END Status FROM Products p JOIN StockBalances b ON p.Id=b.ProductId JOIN Units u ON u.Id=p.UnitId WHERE p.IsActive=1 AND (b.Quantity=0 OR b.Quantity<=p.Minimum) ORDER BY CASE WHEN b.Quantity=0 THEN 0 ELSE 1 END,CASE WHEN p.Minimum>0 THEN CAST(b.Quantity AS decimal(19,6))/p.Minimum ELSE 0 END,p.SKU");
        var latest = await w.TableAsync("SELECT TOP(10) d.Id,d.Number Nomor,d.Type Jenis,d.PostedAt Waktu,u.Username Pembuat FROM StockDocuments d JOIN Users u ON u.Id=d.CreatedBy WHERE d.Status='POSTED' ORDER BY d.PostedAt DESC,d.Id DESC"); return new DashboardView((int)r["Total"], cats, (int)r["Low"], (int)r["OutStock"], role == UserRole.Admin ? (decimal)r["Value"] : null, priority, latest, await Now(w), Convert.ToString(await w.ScalarAsync("SELECT Value FROM Settings WHERE [Key]='TimeZone'"))!);
    }, ct, isolation: IsolationLevel.Snapshot);
    public Task<ReportSnapshot> ReportAsync(SessionContext s, ReportQuery q, CancellationToken ct = default) => db.TransactionAsync(async w =>
    {
        var role = await Authorize(w, s); bool costs = role == UserRole.Admin; if (q.Start.HasValue && q.End.HasValue && q.End <= q.Start) throw new ValidationException("Akhir periode harus setelah awal periode."); if (q.Kind == "Valuation" && !costs) throw new AuthorizationException();
        (string, object?)[] p = [("@product", q.ProductId), ("@cat", q.CategoryId), ("@active", q.IsActive), ("@status", q.StockStatus), ("@start", q.Start), ("@end", q.End), ("@type", q.DocumentType), ("@user", q.UserId), ("@op", q.StocktakeId), ("@search", q.Search.Trim())]; DataTable t;
        const string filter = " WHERE (@product IS NULL OR p.Id=@product) AND (@cat IS NULL OR p.CategoryId=@cat) AND (@active IS NULL OR p.IsActive=@active) AND (@search='' OR CHARINDEX(@search,p.SKU)>0 OR CHARINDEX(@search,p.Name)>0)";
        if (q.Kind is "CurrentStock" or "CriticalStock" or "Valuation")
        {
            string extra = q.Kind == "Valuation" ? ",CASE WHEN b.Quantity=0 THEN 0 ELSE b.InventoryValue/b.Quantity END BiayaRataRata,b.InventoryValue NilaiPersediaan" : "";
            t = await w.TableAsync("SELECT p.SKU,p.Name Nama,c.Name Kategori,u.Name Satuan,b.Quantity Stok,p.Minimum,CASE WHEN b.Quantity=0 THEN N'Habis' WHEN b.Quantity<=p.Minimum THEN N'Menipis' ELSE N'Aman' END Status" + extra + " FROM Products p JOIN StockBalances b ON b.ProductId=p.Id JOIN Categories c ON c.Id=p.CategoryId JOIN Units u ON u.Id=p.UnitId" + filter + (q.Kind == "CriticalStock" ? " AND (b.Quantity=0 OR b.Quantity<=p.Minimum)" : "") + " AND (@status IS NULL OR (@status=N'Habis' AND b.Quantity=0) OR (@status=N'Menipis' AND b.Quantity>0 AND b.Quantity<=p.Minimum) OR (@status=N'Aman' AND b.Quantity>p.Minimum)) ORDER BY p.SKU", p);
        }
        else if (q.Kind == "StockCard")
        {
            if (!q.ProductId.HasValue) throw new ValidationException("Pilih barang untuk kartu stok.");
            t = await w.TableAsync("SELECT l.PostedAt Waktu,d.Number Nomor,d.Type Jenis,l.QuantityDelta Perubahan,l.QuantityAfter Saldo,u.Username Pengguna" + (costs ? ",l.ValueDelta PerubahanNilai,l.ValueAfter SaldoNilai" : "") + " FROM StockLedger l JOIN StockDocumentLines dl ON dl.Id=l.DocumentLineId JOIN StockDocuments d ON d.Id=dl.DocumentId JOIN Users u ON u.Id=d.CreatedBy WHERE l.ProductId=@product AND (@start IS NULL OR l.PostedAt>=@start) AND (@end IS NULL OR l.PostedAt<@end) ORDER BY l.Id", p);
            var opening = await w.TableAsync("SELECT COALESCE(SUM(QuantityDelta),0) Q,COALESCE(SUM(ValueDelta),0) V FROM StockLedger WHERE ProductId=@product AND @start IS NOT NULL AND PostedAt<@start", p); var row = t.NewRow(); row["Nomor"] = "SALDO AWAL PERIODE"; row["Jenis"] = "OPENING_BALANCE"; row["Perubahan"] = 0; row["Saldo"] = opening.Rows[0]["Q"]; if (costs) { row["PerubahanNilai"] = 0m; row["SaldoNilai"] = opening.Rows[0]["V"]; }
            t.Rows.InsertAt(row, 0);
        }
        else if (q.Kind == "Transactions")
        {
            t = await w.TableAsync("SELECT d.Number Nomor,d.Type Jenis,d.Status,d.PostedAt Waktu,u.Username Pembuat,CASE WHEN @costs=1 OR d.Type NOT IN('QUANTITY_ADJUSTMENT','VALUE_ADJUSTMENT') THEN d.Reason ELSE NULL END Alasan,d.ExternalReference Referensi FROM StockDocuments d JOIN Users u ON u.Id=d.CreatedBy WHERE (@start IS NULL OR d.PostedAt>=@start) AND (@end IS NULL OR d.PostedAt<@end) AND (@type IS NULL OR d.Type=@type) AND (@user IS NULL OR d.CreatedBy=@user) ORDER BY d.Id", p.Concat(new (string, object?)[] { ("@costs", costs) }).ToArray());
        }
        else if (q.Kind == "StocktakeResults")
        {
            t = await w.TableAsync("SELECT st.Number Sesi,st.Status,p.SKU,p.Name Nama,l.QuantitySnapshot Acuan,l.CountedQuantity Fisik,l.CountedQuantity-l.QuantitySnapshot Selisih,l.Reason Alasan,uc.Username Pemeriksa,up.Username Pembuku,d.Number Dokumen FROM StocktakeLines l JOIN Stocktakes st ON st.Id=l.StocktakeId JOIN Products p ON p.Id=l.ProductId LEFT JOIN Users uc ON uc.Id=l.CountedBy LEFT JOIN Users up ON up.Id=st.PostedBy LEFT JOIN StockDocuments d ON d.StocktakeId=st.Id WHERE (@op IS NULL OR st.Id=@op) AND (@start IS NULL OR st.CreatedAt>=@start) AND (@end IS NULL OR st.CreatedAt<@end) ORDER BY st.Id,p.SKU", p);
        }
        else throw new ValidationException("Jenis laporan tidak valid.");
        string toko = (string)(await w.ScalarAsync("SELECT Value FROM Settings WHERE [Key]='StoreName'"))!, tz = (string)(await w.ScalarAsync("SELECT Value FROM Settings WHERE [Key]='TimeZone'"))!;
        if (q.Kind == "Valuation") ReportPresentation.AppendValuationTotal(t);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(tz); foreach (DataColumn col in t.Columns) if (col.DataType == typeof(DateTimeOffset)) foreach (DataRow row in t.Rows) if (row[col] is DateTimeOffset dt) row[col] = TimeZoneInfo.ConvertTime(dt, zone);
        return new ReportSnapshot(Guid.NewGuid(), s.UserId, costs && (q.Kind is "Valuation" or "StockCard"), await Now(w), q, t, toko, tz, costs && q.Kind == "Transactions");
    }, ct, isolation: IsolationLevel.Snapshot);
    public async Task RecordExportAsync(SessionContext s, ReportSnapshot snapshot, CancellationToken ct = default)
    {
        await db.TransactionAsync(async w => { var role = await Authorize(w, s); if (snapshot.OwnerId != s.UserId || (snapshot.RequiresAdmin && role != UserRole.Admin)) throw new AuthorizationException(); await Audit(w, s.UserId, "EXPORT", "Reports", snapshot.Id, null, new { snapshot.Query, snapshot.AsOf, Rows = snapshot.Data.Rows.Count }); return 0; }, ct);
    }
}
