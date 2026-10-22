using System.Data;
using Stokin.Application;
using Stokin.Application.Common;
using Stokin.Domain.Inventory;
using Stokin.Infrastructure.Database;
namespace Stokin.Infrastructure;

public sealed partial class SqlStokinRepository
{
    public Task<DataTable> ListDocumentsAsync(SessionContext s, string? type, int page = 1, CancellationToken ct = default) => db.TransactionAsync(async w =>
    {
        await Authorize(w, s); return await w.TableAsync("SELECT d.Id,d.Number Nomor,d.Type Jenis,d.Status,d.CreatedAt Dibuat,d.PostedAt Dibukukan,u.Username Pembuat FROM StockDocuments d JOIN Users u ON u.Id=d.CreatedBy WHERE (@type IS NULL OR d.Type=@type) ORDER BY d.Id DESC OFFSET @skip ROWS FETCH NEXT 50 ROWS ONLY", ("@type", type), ("@skip", checked((Math.Max(page, 1) - 1) * 50)));
    }, ct);
    static async Task<DataRow> Header(SqlWork w, long id)
    {
        var t = await w.TableAsync("SELECT * FROM StockDocuments WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id", ("@id", id)); if (t.Rows.Count == 0) throw new ValidationException("Dokumen tidak ditemukan."); return t.Rows[0];
    }
    static async Task<DocumentView> ReadDocument(SqlWork w, SessionContext s, long id, UserRole role, bool full = false)
    {
        var h = await Header(w, id); if (!full && role != UserRole.Admin && Sensitive(Str(h, "Type"))) throw new AuthorizationException("Detail stok awal/koreksi hanya untuk Admin.");
        bool costs = full || role == UserRole.Admin || (Str(h, "Type") == "IN" && Id(h["CreatedBy"]) == s.UserId);
        var t = await w.TableAsync("SELECT l.*,COALESCE(l.SKU,p.SKU) DisplaySKU,COALESCE(l.ProductName,p.Name) DisplayName,COALESCE(l.UnitName,u.Name) DisplayUnit FROM StockDocumentLines l JOIN Products p ON p.Id=l.ProductId JOIN Units u ON u.Id=p.UnitId WHERE l.DocumentId=@id ORDER BY l.Id", ("@id", id));
        var lines = t.Rows.Cast<DataRow>().Select(r => new DocumentLine(Id(r["ProductId"]), (int)r["Quantity"], costs && r["UnitCostInput"] is decimal c ? c : null, r["Direction"] as string, costs && r["TargetValue"] is decimal v ? v : null, costs ? r["BalanceVersion"] as byte[] : null, costs ? r["CalculationBasis"] as string : null, costs && r["SnapshotQuantity"] is int q ? q : null, costs && r["SnapshotValue"] is decimal sv ? sv : null)).ToArray();
        var display = new DataTable(); display.Columns.Add("SKU"); display.Columns.Add("Nama"); display.Columns.Add("Jumlah", typeof(int)); display.Columns.Add("Satuan"); if (costs) { display.Columns.Add("BiayaUnit", typeof(decimal)); display.Columns.Add("Nilai", typeof(decimal)); }
        foreach (DataRow r in t.Rows) { var row = display.NewRow(); row["SKU"] = r["DisplaySKU"]; row["Nama"] = r["DisplayName"]; row["Jumlah"] = r["Quantity"]; row["Satuan"] = r["DisplayUnit"]; if (costs) { row["BiayaUnit"] = r["UnitCostInput"]; row["Nilai"] = r["PostedValue"] is decimal pv ? pv : r["UnitCostInput"] is decimal c ? c * (int)r["Quantity"] : DBNull.Value; } display.Rows.Add(row); }
        return new(id, Str(h, "Number"), Str(h, "Type"), Str(h, "Status"), Id(h["CreatedBy"]), h["Reason"] as string, h["ExternalReference"] as string, h["ReferenceDocumentId"] is long rf ? rf : null, Version(h), lines, display);
    }
    public Task<DocumentView> GetDocumentAsync(SessionContext s, long id, CancellationToken ct = default) => db.TransactionAsync(async w => { var role = await Authorize(w, s); return await ReadDocument(w, s, id, role); }, ct);
    static async Task<long> CreateDraft(SqlWork w, SessionContext s, DocumentDraft d, long? stocktakeId = null)
    {
        long seq = Id(await w.ScalarAsync("SELECT NEXT VALUE FOR DocumentSequence")); string no = $"{d.Type}-{seq:D8}";
        long id = Id(await w.ScalarAsync("INSERT StockDocuments(Number,Type,CreatedBy,CreatedAt,Reason,ExternalReference,ReferenceDocumentId,StocktakeId) OUTPUT INSERTED.Id VALUES(@no,@type,@user,TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00'),@reason,@external,@reference,@op)", ("@no", no), ("@type", d.Type), ("@user", s.UserId), ("@reason", d.Reason), ("@external", d.ExternalReference), ("@reference", d.ReferenceDocumentId), ("@op", stocktakeId)));
        return id;
    }
    static async Task InsertLines(SqlWork w, long id, IReadOnlyList<DocumentLine> lines)
    {
        foreach (var l in lines)
        {
            if (Id(await w.ScalarAsync("SELECT COUNT(*) FROM Products WHERE Id=@id AND IsActive=1", ("@id", l.ProductId))) != 1) throw new ValidationException("Barang tidak aktif atau tidak ditemukan.");
            await w.ExecuteAsync("INSERT StockDocumentLines(DocumentId,ProductId,Quantity,Direction,UnitCostInput,TargetValue,BalanceVersion,CalculationBasis,SnapshotQuantity,SnapshotValue) VALUES(@doc,@product,@q,@dir,@cost,@target,@version,@basis,@sq,@sv)", ("@doc", id), ("@product", l.ProductId), ("@q", l.Quantity), ("@dir", l.Direction), ("@cost", l.UnitCost), ("@target", l.TargetValue), ("@version", l.BalanceVersion), ("@basis", l.CalculationBasis), ("@sq", l.SnapshotQuantity), ("@sv", l.SnapshotValue));
        }
    }
    public Task<DocumentView> SaveDraftAsync(SessionContext s, DocumentDraft d, CancellationToken ct = default)
    {
        Rules.Draft(d); return db.TransactionAsync(async w =>
        {
            var role = await Authorize(w, s, Sensitive(d.Type)); long id;
            if (d.Id.HasValue)
            {
                id = d.Id.Value; var h = await Header(w, id); Own(role, s, h); if (Str(h, "Status") != "DRAFT" || Str(h, "Type") != d.Type) throw new ValidationException("Dokumen ini tidak dapat diubah."); Match(d.Version, Version(h));
                await w.ExecuteAsync("UPDATE StockDocuments SET Reason=@r,ExternalReference=@e,ReferenceDocumentId=@ref WHERE Id=@id;DELETE StockDocumentLines WHERE DocumentId=@id", ("@r", d.Reason), ("@e", d.ExternalReference), ("@ref", d.ReferenceDocumentId), ("@id", id));
            }
            else id = await CreateDraft(w, s, d);
            await InsertLines(w, id, d.Lines); await Audit(w, s.UserId, "SAVE_DRAFT", "StockDocuments", id); return await ReadDocument(w, s, id, role);
        }, ct);
    }
    public async Task CancelDraftAsync(SessionContext s, long id, byte[] version, CancellationToken ct = default)
    {
        await db.TransactionAsync(async w => { var role = await Authorize(w, s); var h = await Header(w, id); Own(role, s, h); Match(version, Version(h)); if (Str(h, "Status") != "DRAFT") throw new ValidationException("Hanya draf yang dapat dibatalkan."); await w.ExecuteAsync("UPDATE StockDocuments SET Status='CANCELLED' WHERE Id=@id", ("@id", id)); await Audit(w, s.UserId, "CANCEL_DRAFT", "StockDocuments", id); return 0; }, ct);
    }
    public Task<PostingResult> PostAsync(SessionContext s, PostDocumentRequest request, CancellationToken ct = default) => db.TransactionAsync(async w => { var role = await Authorize(w, s); return await PostCore(w, s, role, request); }, ct);
    static async Task<PostingResult> PostCore(SqlWork w, SessionContext s, UserRole role, PostDocumentRequest request, bool stocktake = false)
    {
        if (request.RequestId == Guid.Empty) throw new ValidationException("RequestId wajib diisi."); await Lock(w, "POST:" + request.RequestId);
        var existing = await w.TableAsync("SELECT * FROM StockDocuments WHERE RequestId=@request", ("@request", request.RequestId));
        if (existing.Rows.Count > 0)
        {
            var r = existing.Rows[0]; if (Id(r["Id"]) != request.DocumentId || Str(r, "ContentHash") != request.ExpectedContentHash) throw new ValidationException("RequestId sudah dipakai untuk isi berbeda."); Own(role, s, r); return new(Id(r["Id"]), Str(r, "Number"), (DateTimeOffset)r["PostedAt"], true);
        }
        var h = await Header(w, request.DocumentId); Own(role, s, h); if (Str(h, "Status") != "DRAFT") throw new ValidationException("Dokumen sudah dibukukan/dibatalkan."); Match(request.DraftVersion, Version(h)); var d = await ReadDocument(w, s, request.DocumentId, role, true);
        if (Fingerprint(d) != request.ExpectedContentHash) throw new ConcurrencyException("Isi draf berubah. Tinjau dan konfirmasi kembali.");
        if (!stocktake) Rules.Draft(new(d.Id, d.Type, d.Reason, d.ExternalReference, d.ReferenceDocumentId, d.Lines, d.Version));
        var now = await Now(w);
        foreach (var l in d.Lines.OrderBy(x => x.ProductId))
        {
            var t = await w.TableAsync("SELECT b.Quantity,b.InventoryValue,b.Version,p.SKU,p.Name,p.IsActive,u.Name UnitName FROM StockBalances b WITH(UPDLOCK,HOLDLOCK) JOIN Products p ON p.Id=b.ProductId JOIN Units u ON u.Id=p.UnitId WHERE b.ProductId=@id", ("@id", l.ProductId)); if (t.Rows.Count != 1 || !(bool)t.Rows[0]["IsActive"]) throw new ValidationException("Barang tidak aktif/tidak ditemukan."); var b = t.Rows[0]; var before = new StockBalance((int)b["Quantity"], (decimal)b["InventoryValue"]);
            if (d.Type == "OPENING" && Id(await w.ScalarAsync("SELECT COUNT(*) FROM StockLedger WHERE ProductId=@id", ("@id", l.ProductId))) > 0) throw new ValidationException("Stok awal hanya sekali sebelum mutasi lain.");
            if (d.Type.Contains("ADJUSTMENT") && !stocktake)
            {
                if (Id(await w.ScalarAsync("SELECT COUNT(*) FROM StockDocuments d JOIN StockDocumentLines l ON l.DocumentId=d.Id WHERE d.Id=@doc AND d.Status='POSTED' AND l.ProductId=@product", ("@doc", d.ReferenceDocumentId), ("@product", l.ProductId))) != 1) throw new ValidationException("Dokumen asal harus sudah dibukukan dan memuat barang ini.");
            }
            if (d.Type == "VALUE_ADJUSTMENT") { Match(l.BalanceVersion, (byte[])b["Version"]); if (l.SnapshotQuantity != before.Quantity || l.SnapshotValue != before.InventoryValue) throw new ConcurrencyException("Snapshot saldo tidak sesuai. Hitung ulang koreksi."); }
            var m = d.Type switch
            {
                "OPENING" or "IN" => InventoryMath.Receive(before, l.Quantity, l.UnitCost ?? 0),
                "OUT" => InventoryMath.Issue(before, l.Quantity),
                "QUANTITY_ADJUSTMENT" => InventoryMath.AdjustQuantity(before, l.Direction == "OUT" ? -l.Quantity : l.Quantity, l.UnitCost),
                "VALUE_ADJUSTMENT" => InventoryMath.AdjustValue(before, l.TargetValue ?? 0),
                _ => throw new ValidationException("Jenis dokumen tidak didukung.")
            };
            long line = Id(await w.ScalarAsync("SELECT Id FROM StockDocumentLines WHERE DocumentId=@d AND ProductId=@p", ("@d", d.Id), ("@p", l.ProductId)));
            await w.ExecuteAsync("UPDATE StockDocumentLines SET SKU=@sku,ProductName=@name,UnitName=@unit,PostedUnitCost=@cost,PostedValue=@value WHERE Id=@line; INSERT StockLedger(DocumentLineId,ProductId,QuantityDelta,ValueDelta,QuantityAfter,ValueAfter,PostedAt) VALUES(@line,@p,@q,@v,@qa,@va,@now);UPDATE StockBalances SET Quantity=@qa,InventoryValue=@va WHERE ProductId=@p", ("@sku", b["SKU"]), ("@name", b["Name"]), ("@unit", b["UnitName"]), ("@cost", l.Quantity > 0 ? decimal.Round(Math.Abs(m.ValueDelta) / l.Quantity, 4, MidpointRounding.AwayFromZero) : 0m), ("@value", Math.Abs(m.ValueDelta)), ("@line", line), ("@p", l.ProductId), ("@q", m.QuantityDelta), ("@v", m.ValueDelta), ("@qa", m.After.Quantity), ("@va", m.After.InventoryValue), ("@now", now));
            await Audit(w, s.UserId, "POST_LINE", "StockDocuments", d.Id, new { l.ProductId, before.Quantity, before.InventoryValue }, new { l.ProductId, m.After.Quantity, m.After.InventoryValue, QuantityDelta = m.QuantityDelta, ValueDelta = m.ValueDelta });
        }
        await w.ExecuteAsync("UPDATE StockDocuments SET Status='POSTED',PostedAt=@now,PostedBy=@user,RequestId=@request,ContentHash=@hash WHERE Id=@id", ("@now", now), ("@user", s.UserId), ("@request", request.RequestId), ("@hash", request.ExpectedContentHash), ("@id", d.Id)); await Audit(w, s.UserId, "POST_DOCUMENT", "StockDocuments", d.Id, null, new { d.Number, d.Type, RequestId = request.RequestId }); return new(d.Id, d.Number, now, false);
    }
    public Task<PostingResult?> FindPostingAsync(SessionContext s, Guid requestId, CancellationToken ct = default) => db.TransactionAsync<PostingResult?>(async w =>
    {
        var role = await Authorize(w, s); var t = await w.TableAsync("SELECT * FROM StockDocuments WHERE RequestId=@id", ("@id", requestId)); if (t.Rows.Count == 0) return null; var r = t.Rows[0]; if (role != UserRole.Admin && Id(r["CreatedBy"]) != s.UserId) throw new AuthorizationException(); return new(Id(r["Id"]), Str(r, "Number"), (DateTimeOffset)r["PostedAt"], true);
    }, ct);
    public Task<BalancePreview> PreviewAsync(SessionContext s, long productId, CancellationToken ct = default) => db.TransactionAsync(async w =>
    {
        await Authorize(w, s, true); var t = await w.TableAsync("SELECT * FROM StockBalances WHERE ProductId=@id", ("@id", productId)); if (t.Rows.Count != 1) throw new ValidationException("Barang tidak ditemukan."); var r = t.Rows[0]; return new BalancePreview(productId, (int)r["Quantity"], (decimal)r["InventoryValue"], Version(r));
    }, ct);
    public async Task AddCorrectionNoteAsync(SessionContext s, long documentId, string note, CancellationToken ct = default)
    {
        note = Rules.Text(note, 500, "Catatan koreksi"); await db.TransactionAsync(async w => { await Authorize(w, s, true); var h = await Header(w, documentId); if (Str(h, "Status") != "POSTED") throw new ValidationException("Pilih dokumen yang sudah dibukukan."); await w.ExecuteAsync("INSERT CorrectionNotes(DocumentId,ActorId,CreatedAt,Note) VALUES(@doc,@user,@now,@note)", ("@doc", documentId), ("@user", s.UserId), ("@now", await Now(w)), ("@note", note)); await Audit(w, s.UserId, "CORRECTION_NOTE", "StockDocuments", documentId, null, note); return 0; }, ct);
    }
}
