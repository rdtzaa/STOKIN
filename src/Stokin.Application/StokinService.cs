using System.Data;
using Stokin.Application.Common;
using Stokin.Domain.Inventory;
namespace Stokin.Application;
/// <summary>Application boundary shared by WPF and acceptance tests. SQL repositories recheck permissions inside write transactions.</summary>
public sealed class StokinService(IStokinRepository repository) : IStokinRepository
{
    readonly IStokinRepository data = repository;
    public IStokinRepository Operations => this;
    public Task<bool> IsSetupAsync(CancellationToken ct = default) => data.IsSetupAsync(ct);
    public Task SetupAsync(SetupRequest request, CancellationToken ct = default) => data.SetupAsync(request, ct);
    public Task<UserSession> LoginAsync(string username, string password, CancellationToken ct = default) => data.LoginAsync(username, password, ct);
    public Task LogoutAsync(SessionContext s, CancellationToken ct = default) => data.LogoutAsync(s, ct);
    public Task<UserRole> CheckAsync(SessionContext s, bool admin = false, bool activity = true, CancellationToken ct = default) => data.CheckAsync(s, admin, activity, ct);
    public Task<IReadOnlyList<LookupRow>> LookupsAsync(SessionContext s, string kind, CancellationToken ct = default) => data.LookupsAsync(s, kind, ct);
    public Task SaveLookupAsync(SessionContext s, string kind, long? id, string name, bool active, byte[]? version, CancellationToken ct = default) => data.SaveLookupAsync(s, kind, id, name, active, version, ct);
    public Task DeleteMasterAsync(SessionContext s, string kind, long id, CancellationToken ct = default) => data.DeleteMasterAsync(s, kind, id, ct);
    public Task<PageResult<DataRow>> ProductsAsync(SessionContext s, ProductFilter f, PageRequest page, CancellationToken ct = default) => data.ProductsAsync(s, f, page, ct);
    public Task<DataTable> ListDocumentsAsync(SessionContext s, string? type, int page = 1, CancellationToken ct = default) => data.ListDocumentsAsync(s, type, page, ct);
    public Task<DocumentView> GetDocumentAsync(SessionContext s, long id, CancellationToken ct = default) => data.GetDocumentAsync(s, id, ct);
    public Task CancelDraftAsync(SessionContext s, long id, byte[] version, CancellationToken ct = default) => data.CancelDraftAsync(s, id, version, ct);
    public Task<PostingResult> PostAsync(SessionContext s, PostDocumentRequest request, CancellationToken ct = default) => data.PostAsync(s, request, ct);
    public Task<PostingResult?> FindPostingAsync(SessionContext s, Guid requestId, CancellationToken ct = default) => data.FindPostingAsync(s, requestId, ct);
    public Task<BalancePreview> PreviewAsync(SessionContext s, long productId, CancellationToken ct = default) => data.PreviewAsync(s, productId, ct);
    public Task AddCorrectionNoteAsync(SessionContext s, long documentId, string note, CancellationToken ct = default) => data.AddCorrectionNoteAsync(s, documentId, note, ct);
    public Task<DataTable> ListStocktakesAsync(SessionContext s, int page = 1, CancellationToken ct = default) => data.ListStocktakesAsync(s, page, ct);
    public Task<StocktakeView> OpenStocktakeAsync(SessionContext s, IReadOnlyList<long> ids, CancellationToken ct = default) => data.OpenStocktakeAsync(s, ids, ct);
    public Task<StocktakeView> GetStocktakeAsync(SessionContext s, long id, CancellationToken ct = default) => data.GetStocktakeAsync(s, id, ct);
    public Task<StocktakeView> CountStocktakeAsync(SessionContext s, long id, byte[] version, IReadOnlyList<StocktakeCount> counts, CancellationToken ct = default) => data.CountStocktakeAsync(s, id, version, counts, ct);
    public Task<StocktakeView> ReloadStocktakeAsync(SessionContext s, long id, CancellationToken ct = default) => data.ReloadStocktakeAsync(s, id, ct);
    public Task<StocktakeView> PostStocktakeAsync(SessionContext s, long id, byte[] version, Guid requestId, CancellationToken ct = default) => data.PostStocktakeAsync(s, id, version, requestId, ct);
    public Task CancelStocktakeAsync(SessionContext s, long id, byte[] version, CancellationToken ct = default) => data.CancelStocktakeAsync(s, id, version, ct);
    public Task<DashboardView> DashboardAsync(SessionContext s, CancellationToken ct = default) => data.DashboardAsync(s, ct);
    public Task<ReportSnapshot> ReportAsync(SessionContext s, ReportQuery query, CancellationToken ct = default) => data.ReportAsync(s, query, ct);
    public Task RecordExportAsync(SessionContext s, ReportSnapshot snapshot, CancellationToken ct = default) => data.RecordExportAsync(s, snapshot, ct);
    public Task<DataTable> UsersAsync(SessionContext s, CancellationToken ct = default) => data.UsersAsync(s, ct);
    public Task SaveUserAsync(SessionContext s, UserEdit user, CancellationToken ct = default) => data.SaveUserAsync(s, user, ct);
    public Task<DataTable> AuditAsync(SessionContext s, int page = 1, CancellationToken ct = default) => data.AuditAsync(s, page, ct);
    public Task<Dictionary<string, string>> SettingsAsync(SessionContext s, CancellationToken ct = default) => data.SettingsAsync(s, ct);
    public Task SaveSettingsAsync(SessionContext s, string storeName, string timeZone, CancellationToken ct = default) => data.SaveSettingsAsync(s, storeName, timeZone, ct);
    public Task BackupAsync(SessionContext s, string path, CancellationToken ct = default) => data.BackupAsync(s, path, ct);
    public Task<BackupInfo> InspectBackupAsync(SessionContext s, string path, CancellationToken ct = default) => data.InspectBackupAsync(s, path, ct);
    public Task RestoreAsync(SessionContext s, BackupInfo confirmed, string confirmation, CancellationToken ct = default) => data.RestoreAsync(s, confirmed, confirmation, ct);

    public Task<long> SaveProductAsync(SessionContext s, ProductEdit p, CancellationToken ct = default)
    {
        Rules.Product(p); return data.SaveProductAsync(s, p with { SKU = p.SKU.Trim(), Name = p.Name.Trim(), Notes = p.Notes?.Trim() }, ct);
    }
    public Task<DocumentView> SaveDraftAsync(SessionContext s, DocumentDraft d, CancellationToken ct = default)
    {
        Rules.Draft(d); return data.SaveDraftAsync(s, d, ct);
    }
    public async Task ExportAsync(SessionContext s, ReportSnapshot snapshot, Stream destination, CancellationToken ct = default)
    {
        if (snapshot.OwnerId != s.UserId) throw new AuthorizationException();
        var role = await Operations.CheckAsync(s, ct: ct);
        if (snapshot.RequiresAdmin && role != UserRole.Admin) throw new AuthorizationException("Hak biaya berubah. Muat ulang laporan.");
        using var staging = new MemoryStream(); await CsvExporter.WriteAsync(snapshot, staging, ct);
        await Operations.RecordExportAsync(s, snapshot, ct); staging.Position = 0; await staging.CopyToAsync(destination, ct);
    }
}
public static class Rules
{
    public static string Text(string? value, int max, string label) { var s = value?.Trim() ?? ""; if (s.Length == 0 || s.Length > max) throw new ValidationException($"{label} wajib diisi, maksimum {max} karakter."); return s; }
    public static void Product(ProductEdit p)
    {
        Text(p.SKU, 30, "SKU"); Text(p.Name, 150, "Nama"); if (p.CategoryId <= 0 || p.UnitId <= 0) throw new ValidationException("Kategori dan satuan wajib dipilih."); if (p.Minimum < 0) throw new ValidationException("Minimum tidak boleh negatif."); if (p.Notes?.Length > 500) throw new ValidationException("Catatan maksimum 500 karakter."); if (p.SellingPrice.HasValue) InventoryMath.Money(p.SellingPrice.Value, true);
    }
    public static void Draft(DocumentDraft d)
    {
        if (!new[] { "OPENING", "IN", "OUT", "QUANTITY_ADJUSTMENT", "VALUE_ADJUSTMENT" }.Contains(d.Type)) throw new ValidationException("Jenis dokumen tidak valid.");
        if (d.Lines.Count == 0 || d.Lines.Count > 50) throw new ValidationException("Dokumen harus berisi 1 sampai 50 barang."); if (d.Lines.Select(x => x.ProductId).Distinct().Count() != d.Lines.Count) throw new ValidationException("SKU duplikat. Ubah baris yang sudah ada.");
        if (d.Reason?.Length > 500 || d.ExternalReference?.Length > 150) throw new ValidationException("Alasan/referensi terlalu panjang.");
        if (d.Type is "OUT" or "QUANTITY_ADJUSTMENT" or "VALUE_ADJUSTMENT") Text(d.Reason, 500, "Alasan");
        if (d.Type.Contains("ADJUSTMENT") && !d.ReferenceDocumentId.HasValue) throw new ValidationException("Dokumen asal wajib dipilih.");
        foreach (var line in d.Lines)
        {
            if (line.ProductId <= 0) throw new ValidationException("Barang wajib dipilih.");
            if (d.Type == "VALUE_ADJUSTMENT") { if (line.Quantity != 0 || line.TargetValue == null || line.BalanceVersion == null) throw new ValidationException("Pratinjau koreksi wajib dimuat ulang."); InventoryMath.Money(line.TargetValue.Value); Text(line.CalculationBasis, 500, "Dasar perhitungan"); }
            else if (line.Quantity <= 0) throw new ValidationException("Jumlah harus bilangan bulat lebih dari nol.");
            if (d.Type is "IN" or "OPENING") InventoryMath.Money(line.UnitCost ?? 0);
            if (d.Type == "QUANTITY_ADJUSTMENT" && line.Direction is not ("IN" or "OUT")) throw new ValidationException("Arah penyesuaian wajib IN atau OUT.");
            if (line.UnitCost.HasValue) InventoryMath.Money(line.UnitCost.Value);
        }
    }
}
