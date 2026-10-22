using System.Data;
using Stokin.Application;
using Stokin.Application.Common;
namespace Stokin.Infrastructure;

public sealed partial class SqlStokinRepository
{
    static string LookupTable(string kind) => kind switch { "Categories" => "Categories", "Units" => "Units", _ => throw new ValidationException("Jenis master tidak valid.") };
    public Task<IReadOnlyList<LookupRow>> LookupsAsync(SessionContext s, string kind, CancellationToken ct = default) => db.TransactionAsync<IReadOnlyList<LookupRow>>(async w => { await Authorize(w, s); var t = await w.TableAsync($"SELECT * FROM {LookupTable(kind)} ORDER BY Name"); return t.Rows.Cast<DataRow>().Select(r => new LookupRow(Id(r["Id"]), Str(r, "Name"), (bool)r["IsActive"], Version(r))).ToArray(); }, ct);
    public async Task SaveLookupAsync(SessionContext s, string kind, long? id, string name, bool active, byte[]? version, CancellationToken ct = default)
    {
        var table = LookupTable(kind); name = Rules.Text(name, kind == "Units" ? 30 : 100, "Nama");
        await db.TransactionAsync(async w =>
        {
            await Authorize(w, s, true); string column = kind == "Categories" ? "CategoryId" : "UnitId";
            if (!active && id.HasValue && Id(await w.ScalarAsync($"SELECT COUNT(*) FROM Products WHERE {column}=@id AND IsActive=1", ("@id", id))) > 0) throw new ValidationException("Master masih dipakai barang aktif.");
            var old = id.HasValue ? await w.ScalarAsync($"SELECT Name FROM {table} WHERE Id=@id", ("@id", id)) : null;
            if (id.HasValue) { if (await w.ExecuteAsync($"UPDATE {table} SET Name=@name,IsActive=@active WHERE Id=@id AND Version=@version", ("@name", name), ("@active", active), ("@id", id), ("@version", version)) != 1) throw new ConcurrencyException(); }
            else id = Id(await w.ScalarAsync($"INSERT {table}(Name,IsActive) OUTPUT INSERTED.Id VALUES(@name,@active)", ("@name", name), ("@active", active)));
            await Audit(w, s.UserId, "SAVE_MASTER", table, id, old, new { Name = name, IsActive = active }); return 0;
        }, ct);
    }
    public async Task DeleteMasterAsync(SessionContext s, string kind, long id, CancellationToken ct = default)
    {
        if (kind is not ("Products" or "Categories" or "Units")) throw new ValidationException("Jenis master tidak valid.");
        await db.TransactionAsync(async w =>
        {
            await Authorize(w, s, true);
            if (kind == "Products") { if (Id(await w.ScalarAsync("SELECT COUNT(*) FROM StockDocumentLines WHERE ProductId=@id", ("@id", id))) > 0 || Id(await w.ScalarAsync("SELECT COUNT(*) FROM StocktakeLines WHERE ProductId=@id", ("@id", id))) > 0) throw new ValidationException("Barang sudah direferensikan. Nonaktifkan setelah saldo nol."); await w.ExecuteAsync("DELETE StockBalances WHERE ProductId=@id", ("@id", id)); }
            else { var col = kind == "Categories" ? "CategoryId" : "UnitId"; if (Id(await w.ScalarAsync($"SELECT COUNT(*) FROM Products WHERE {col}=@id", ("@id", id))) > 0) throw new ValidationException("Master masih dipakai barang."); }
            await w.ExecuteAsync($"DELETE {kind} WHERE Id=@id", ("@id", id)); await Audit(w, s.UserId, "DELETE_MASTER", kind, id); return 0;
        }, ct);
    }
    public Task<long> SaveProductAsync(SessionContext s, ProductEdit p, CancellationToken ct = default)
    {
        Rules.Product(p); return db.TransactionAsync(async w =>
        {
            await Authorize(w, s, true); if (Id(await w.ScalarAsync("SELECT COUNT(*) FROM Categories c CROSS JOIN Units u WHERE c.Id=@cat AND u.Id=@unit AND c.IsActive=1 AND u.IsActive=1", ("@cat", p.CategoryId), ("@unit", p.UnitId))) != 1) throw new ValidationException("Kategori dan satuan harus aktif.");
            long id = p.Id ?? 0; object? before = null;
            if (p.Id.HasValue)
            {
                var old = await w.TableAsync("SELECT p.*,b.Quantity FROM Products p WITH(UPDLOCK,HOLDLOCK) JOIN StockBalances b ON b.ProductId=p.Id WHERE p.Id=@id", ("@id", id)); if (old.Rows.Count == 0) throw new ValidationException("Barang tidak ditemukan."); var r = old.Rows[0]; Match(p.Version, Version(r)); before = new { SKU = Str(r, "SKU"), Name = Str(r, "Name"), Minimum = r["Minimum"], CategoryId = r["CategoryId"], SellingPrice = r["SellingPrice"] is DBNull ? null : r["SellingPrice"], IsActive = r["IsActive"] };
                bool used = Id(await w.ScalarAsync("SELECT COUNT(*) FROM StockLedger WHERE ProductId=@id", ("@id", id))) > 0;
                if (used && (!string.Equals(p.SKU.Trim(), Str(r, "SKU"), StringComparison.Ordinal) || p.UnitId != Id(r["UnitId"]))) throw new ValidationException("SKU dan satuan terkunci setelah mutasi.");
                if (!p.IsActive && (Convert.ToInt32(r["Quantity"]) != 0 || Id(await w.ScalarAsync("SELECT COUNT(*) FROM StockDocumentLines l JOIN StockDocuments d ON d.Id=l.DocumentId WHERE l.ProductId=@id AND d.Status='DRAFT'", ("@id", id))) > 0 || Id(await w.ScalarAsync("SELECT COUNT(*) FROM StocktakeLines l JOIN Stocktakes t ON t.Id=l.StocktakeId WHERE l.ProductId=@id AND t.Status='OPEN'", ("@id", id))) > 0)) throw new ValidationException("Barang memiliki saldo, draf, atau opname terbuka.");
                await w.ExecuteAsync("UPDATE Products SET SKU=@sku,Name=@name,CategoryId=@cat,UnitId=@unit,Minimum=@min,SellingPrice=@price,Notes=@notes,IsActive=@active WHERE Id=@id", ("@sku", p.SKU.Trim()), ("@name", p.Name.Trim()), ("@cat", p.CategoryId), ("@unit", p.UnitId), ("@min", p.Minimum), ("@price", p.SellingPrice), ("@notes", p.Notes), ("@active", p.IsActive), ("@id", id));
            }
            else
            {
                id = Id(await w.ScalarAsync("INSERT Products(SKU,Name,CategoryId,UnitId,Minimum,SellingPrice,Notes,IsActive) OUTPUT INSERTED.Id VALUES(@sku,@name,@cat,@unit,@min,@price,@notes,@active)", ("@sku", p.SKU.Trim()), ("@name", p.Name.Trim()), ("@cat", p.CategoryId), ("@unit", p.UnitId), ("@min", p.Minimum), ("@price", p.SellingPrice), ("@notes", p.Notes), ("@active", p.IsActive)));
                await w.ExecuteAsync("INSERT StockBalances(ProductId) VALUES(@id)", ("@id", id));
            }
            await Audit(w, s.UserId, "SAVE_PRODUCT", "Products", id, before, p with { Version = null }); return id;
        }, ct);
    }
    const string ProductSelect = "SELECT p.Id,p.SKU,p.Name Nama,c.Name Kategori,u.Name Satuan,b.Quantity Stok,p.Minimum,CASE WHEN b.Quantity=0 THEN N'Habis' WHEN b.Quantity<=p.Minimum THEN N'Menipis' ELSE N'Aman' END Status,p.SellingPrice HargaJual,p.Notes Catatan,p.IsActive Aktif,p.CategoryId,p.UnitId,p.Version FROM Products p JOIN Categories c ON p.CategoryId=c.Id JOIN Units u ON p.UnitId=u.Id JOIN StockBalances b ON b.ProductId=p.Id ";
    public Task<PageResult<DataRow>> ProductsAsync(SessionContext s, ProductFilter f, PageRequest page, CancellationToken ct = default) => db.TransactionAsync(async w =>
    {
        await Authorize(w, s); if (page.Page < 1 || page.Size < 1 || page.Size > 10000) throw new ValidationException("Halaman tidak valid.");
        const string where = " WHERE (@search='' OR CHARINDEX(@search,p.SKU)>0 OR CHARINDEX(@search,p.Name)>0) AND (@cat IS NULL OR p.CategoryId=@cat) AND (@active IS NULL OR p.IsActive=@active) AND (@status IS NULL OR (@status=N'Habis' AND b.Quantity=0) OR (@status=N'Menipis' AND b.Quantity>0 AND b.Quantity<=p.Minimum) OR (@status=N'Aman' AND b.Quantity>p.Minimum))";
        (string, object?)[] p = [("@search", f.Search.Trim()), ("@cat", f.CategoryId), ("@active", f.IsActive), ("@status", f.Status), ("@skip", checked((page.Page - 1) * page.Size)), ("@size", page.Size)];
        string sort = page.Sort switch { "Nama" => "p.Name", "Stok" => "b.Quantity", "Minimum" => "p.Minimum", _ => "p.SKU" }; string order = page.Descending ? " DESC" : " ASC";
        long total = Id(await w.ScalarAsync("SELECT COUNT(*) FROM Products p JOIN StockBalances b ON b.ProductId=p.Id" + where, p)); var t = await w.TableAsync(ProductSelect + where + $" ORDER BY {sort}{order},p.Id OFFSET @skip ROWS FETCH NEXT @size ROWS ONLY", p); return new PageResult<DataRow>(t.Rows.Cast<DataRow>().ToArray(), total, page.Page, page.Size);
    }, ct);
}
