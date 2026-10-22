using System.Data;
using Stokin.Application;
using Stokin.Application.Common;
using Stokin.Infrastructure.Database;
namespace Stokin.Infrastructure;

public sealed partial class SqlStokinRepository
{
    public Task<DataTable> ListStocktakesAsync(SessionContext s, int page = 1, CancellationToken ct = default) => db.TransactionAsync(async w => { await Authorize(w, s); return await w.TableAsync("SELECT t.Id,t.Number Nomor,t.Status,t.CreatedAt Dibuat,u.Username Pembuat,t.PostedAt Dibukukan FROM Stocktakes t JOIN Users u ON u.Id=t.CreatedBy ORDER BY t.Id DESC OFFSET @skip ROWS FETCH NEXT 50 ROWS ONLY", ("@skip", checked((Math.Max(page, 1) - 1) * 50))); }, ct);
    static async Task<DataRow> TakeHeader(SqlWork w, long id)
    {
        var t = await w.TableAsync("SELECT * FROM Stocktakes WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id", ("@id", id)); if (t.Rows.Count != 1) throw new ValidationException("Sesi opname tidak ditemukan."); return t.Rows[0];
    }
    static async Task<StocktakeView> ReadTake(SqlWork w, long id, UserRole role)
    {
        var h = await TakeHeader(w, id); var t = await w.TableAsync("SELECT l.ProductId,p.SKU,p.Name Nama,u.Name Satuan,l.QuantitySnapshot Acuan,l.CountedQuantity Fisik,l.CountedQuantity-l.QuantitySnapshot Selisih,l.Reason Alasan,l.ZeroStockUnitCost BiayaUnit,l.ValueSnapshot NilaiAcuan,l.CountedBy Pemeriksa FROM StocktakeLines l JOIN Products p ON p.Id=l.ProductId JOIN Units u ON u.Id=p.UnitId WHERE l.StocktakeId=@id ORDER BY p.SKU", ("@id", id));
        if (role != UserRole.Admin) { t.Columns.Remove("BiayaUnit"); t.Columns.Remove("NilaiAcuan"); }
        return new(id, Str(h, "Number"), Str(h, "Status"), Version(h), t);
    }
    public Task<StocktakeView> GetStocktakeAsync(SessionContext s, long id, CancellationToken ct = default) => db.TransactionAsync(async w => { var role = await Authorize(w, s); return await ReadTake(w, id, role); }, ct);
    public Task<StocktakeView> OpenStocktakeAsync(SessionContext s, IReadOnlyList<long> ids, CancellationToken ct = default) => db.TransactionAsync(async w =>
    {
        var role = await Authorize(w, s, true); if (ids.Count == 0 || ids.Count != ids.Distinct().Count()) throw new ValidationException("Pilih barang tanpa duplikat."); long seq = Id(await w.ScalarAsync("SELECT NEXT VALUE FOR DocumentSequence")); long id = Id(await w.ScalarAsync("INSERT Stocktakes(Number,CreatedBy,CreatedAt) OUTPUT INSERTED.Id VALUES(@no,@user,@now)", ("@no", $"OP-{seq:D8}"), ("@user", s.UserId), ("@now", await Now(w))));
        foreach (long p in ids.Order())
        {
            if (await w.ExecuteAsync("INSERT StocktakeLines(StocktakeId,ProductId,QuantitySnapshot,ValueSnapshot,BalanceVersion) SELECT @op,b.ProductId,b.Quantity,b.InventoryValue,b.Version FROM StockBalances b JOIN Products p ON p.Id=b.ProductId WHERE b.ProductId=@id AND p.IsActive=1", ("@op", id), ("@id", p)) != 1) throw new ValidationException("Barang harus aktif dan tersedia.");
        }
        await Audit(w, s.UserId, "OPEN_STOCKTAKE", "Stocktakes", id); return await ReadTake(w, id, role);
    }, ct);
    public Task<StocktakeView> CountStocktakeAsync(SessionContext s, long id, byte[] version, IReadOnlyList<StocktakeCount> counts, CancellationToken ct = default) => db.TransactionAsync(async w =>
    {
        var role = await Authorize(w, s); var h = await TakeHeader(w, id); Match(version, Version(h)); if (Str(h, "Status") != "OPEN") throw new ValidationException("Sesi sudah ditutup."); if (counts.Select(x => x.ProductId).Distinct().Count() != counts.Count) throw new ValidationException("Barang duplikat.");
        foreach (var c in counts)
        {
            if (c.Quantity < 0 || c.Reason?.Length > 500) throw new ValidationException("Jumlah fisik tidak boleh negatif; alasan maksimum 500 karakter."); if (c.UnitCost.HasValue && role != UserRole.Admin) throw new AuthorizationException(); if (c.UnitCost.HasValue) Stokin.Domain.Inventory.InventoryMath.Money(c.UnitCost.Value);
            if (await w.ExecuteAsync("UPDATE StocktakeLines SET CountedQuantity=@q,Reason=@reason,CountedBy=@user,ZeroStockUnitCost=CASE WHEN @admin=1 THEN @cost ELSE ZeroStockUnitCost END WHERE StocktakeId=@op AND ProductId=@p", ("@q", c.Quantity), ("@reason", c.Reason), ("@user", s.UserId), ("@admin", role == UserRole.Admin), ("@cost", c.UnitCost), ("@op", id), ("@p", c.ProductId)) != 1) throw new ValidationException("Barang tidak ada dalam sesi.");
        }
        await w.ExecuteAsync("UPDATE Stocktakes SET Status=Status WHERE Id=@id", ("@id", id)); await Audit(w, s.UserId, "COUNT_STOCKTAKE", "Stocktakes", id, null, counts); return await ReadTake(w, id, role);
    }, ct);
    public Task<StocktakeView> ReloadStocktakeAsync(SessionContext s, long id, CancellationToken ct = default) => db.TransactionAsync(async w =>
    {
        var role = await Authorize(w, s, true); var h = await TakeHeader(w, id); if (Str(h, "Status") != "OPEN") throw new ValidationException("Sesi sudah ditutup.");
        await w.ExecuteAsync("UPDATE l SET QuantitySnapshot=b.Quantity,ValueSnapshot=b.InventoryValue,BalanceVersion=b.Version,CountedQuantity=NULL,Reason=NULL,ZeroStockUnitCost=NULL,CountedBy=NULL FROM StocktakeLines l JOIN StockBalances b ON b.ProductId=l.ProductId WHERE l.StocktakeId=@id AND l.BalanceVersion<>b.Version;UPDATE Stocktakes SET Status=Status WHERE Id=@id", ("@id", id)); await Audit(w, s.UserId, "RELOAD_STOCKTAKE", "Stocktakes", id); return await ReadTake(w, id, role);
    }, ct);
    public Task<StocktakeView> PostStocktakeAsync(SessionContext s, long id, byte[] version, Guid requestId, CancellationToken ct = default) => db.TransactionAsync(async w =>
    {
        var role = await Authorize(w, s, true); if (requestId == Guid.Empty) throw new ValidationException("RequestId wajib."); await Lock(w, "TAKE:" + requestId); var h = await TakeHeader(w, id);
        if (Str(h, "Status") == "POSTED" && h["RequestId"] is Guid prior && prior == requestId) return await ReadTake(w, id, role);
        Match(version, Version(h)); if (Str(h, "Status") != "OPEN") throw new ValidationException("Sesi sudah ditutup.");
        var t = await w.TableAsync("SELECT l.*,b.Quantity CurrentQuantity,b.InventoryValue CurrentValue,b.Version CurrentVersion FROM StocktakeLines l JOIN StockBalances b WITH(UPDLOCK,HOLDLOCK) ON b.ProductId=l.ProductId WHERE l.StocktakeId=@id ORDER BY l.ProductId", ("@id", id)); var lines = new List<DocumentLine>();
        foreach (DataRow r in t.Rows)
        {
            Match((byte[])r["BalanceVersion"], (byte[])r["CurrentVersion"]); if (r["CountedQuantity"] is not int count) throw new ValidationException("Seluruh jumlah fisik wajib diisi ulang setelah konflik."); int delta = checked(count - (int)r["QuantitySnapshot"]); if (delta == 0) continue;
            Rules.Text(r["Reason"] as string, 500, "Alasan selisih"); lines.Add(new(Id(r["ProductId"]), Math.Abs(delta), r["ZeroStockUnitCost"] as decimal?, delta > 0 ? "IN" : "OUT"));
        }
        if (lines.Count > 0)
        {
            var d = new DocumentDraft(null, "QUANTITY_ADJUSTMENT", "Hasil opname " + Str(h, "Number"), null, null, lines); long doc = await CreateDraft(w, s, d, id); await InsertLines(w, doc, lines); var view = await ReadDocument(w, s, doc, role, true); await PostCore(w, s, role, new(doc, view.Version, requestId, Fingerprint(view)), true);
        }
        await w.ExecuteAsync("UPDATE Stocktakes SET Status='POSTED',PostedBy=@user,PostedAt=@now,RequestId=@request WHERE Id=@id", ("@user", s.UserId), ("@now", await Now(w)), ("@request", requestId), ("@id", id)); await Audit(w, s.UserId, "POST_STOCKTAKE", "Stocktakes", id, null, new { RequestId = requestId }); return await ReadTake(w, id, role);
    }, ct);
    public async Task CancelStocktakeAsync(SessionContext s, long id, byte[] version, CancellationToken ct = default)
    {
        await db.TransactionAsync(async w => { await Authorize(w, s, true); var h = await TakeHeader(w, id); Match(version, Version(h)); if (Str(h, "Status") != "OPEN") throw new ValidationException("Sesi sudah ditutup."); await w.ExecuteAsync("UPDATE Stocktakes SET Status='CANCELLED' WHERE Id=@id", ("@id", id)); await Audit(w, s.UserId, "CANCEL_STOCKTAKE", "Stocktakes", id); return 0; }, ct);
    }
}
