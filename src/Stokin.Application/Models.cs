using System.Data;
using Stokin.Application.Common;
namespace Stokin.Application;

public enum UserRole { Admin = 1, Petugas = 2 }
public record UserSession(SessionContext Context, string Username, UserRole Role);
public record SetupRequest(string StoreName, string Username, string Password);
public record LookupRow(long Id, string Name, bool IsActive, byte[] Version);
public record ProductEdit(long? Id, string SKU, string Name, long CategoryId, long UnitId, int Minimum, decimal? SellingPrice, string? Notes, bool IsActive, byte[]? Version = null);
public record ProductFilter(string Search = "", long? CategoryId = null, string? Status = null, bool? IsActive = true);
public record DocumentLine(long ProductId, int Quantity, decimal? UnitCost = null, string? Direction = null, decimal? TargetValue = null, byte[]? BalanceVersion = null, string? CalculationBasis = null, int? SnapshotQuantity = null, decimal? SnapshotValue = null);
public record DocumentDraft(long? Id, string Type, string? Reason, string? ExternalReference, long? ReferenceDocumentId, IReadOnlyList<DocumentLine> Lines, byte[]? Version = null);
public record DocumentView(long Id, string Number, string Type, string Status, long CreatedBy, string? Reason, string? ExternalReference, long? ReferenceDocumentId, byte[] Version, IReadOnlyList<DocumentLine> Lines, DataTable Display);
public record PostDocumentRequest(long DocumentId, byte[] DraftVersion, Guid RequestId, string ExpectedContentHash);
public record PostingResult(long DocumentId, string DocumentNo, DateTimeOffset PostedAt, bool Replayed);
public record BalancePreview(long ProductId, int Quantity, decimal Value, byte[] Version) { public decimal AverageCost => Quantity == 0 ? 0 : Value / Quantity; }
public record StocktakeCount(long ProductId, int Quantity, string? Reason, decimal? UnitCost = null);
public record StocktakeView(long Id, string Number, string Status, byte[] Version, DataTable Lines);
public record DashboardView(int TotalProducts, int TotalCategories, int LowStock, int OutOfStock, decimal? InventoryValue, DataTable Priority, DataTable Latest, DateTimeOffset LoadedAt, string TimeZone = "Asia/Jakarta");
public record ReportQuery(string Kind, long? ProductId = null, long? CategoryId = null, string? StockStatus = null, bool? IsActive = true, DateTimeOffset? Start = null, DateTimeOffset? End = null, string? DocumentType = null, long? UserId = null, long? StocktakeId = null, string Search = "");
public record ReportSnapshot(Guid Id, long OwnerId, bool HasCosts, DateTimeOffset AsOf, ReportQuery Query, DataTable Data, string StoreName, string TimeZone, bool RestrictedDetails = false) { public bool RequiresAdmin => HasCosts || RestrictedDetails; }
public record UserEdit(long? Id, string Username, UserRole Role, bool IsActive, string? Password = null);
public record BackupInfo(string Database, DateTimeOffset? FinishedAt, string ServerPath);
public interface IStokinRepository
{
    Task<bool> IsSetupAsync(CancellationToken ct = default);
    Task SetupAsync(SetupRequest request, CancellationToken ct = default);
    Task<UserSession> LoginAsync(string username, string password, CancellationToken ct = default);
    Task LogoutAsync(SessionContext s, CancellationToken ct = default);
    Task<UserRole> CheckAsync(SessionContext s, bool admin = false, bool activity = true, CancellationToken ct = default);
    Task<IReadOnlyList<LookupRow>> LookupsAsync(SessionContext s, string kind, CancellationToken ct = default);
    Task SaveLookupAsync(SessionContext s, string kind, long? id, string name, bool active, byte[]? version, CancellationToken ct = default);
    Task DeleteMasterAsync(SessionContext s, string kind, long id, CancellationToken ct = default);
    Task<long> SaveProductAsync(SessionContext s, ProductEdit p, CancellationToken ct = default);
    Task<PageResult<DataRow>> ProductsAsync(SessionContext s, ProductFilter f, PageRequest page, CancellationToken ct = default);
    Task<DataTable> ListDocumentsAsync(SessionContext s, string? type, int page = 1, CancellationToken ct = default);
    Task<DocumentView> SaveDraftAsync(SessionContext s, DocumentDraft d, CancellationToken ct = default);
    Task<DocumentView> GetDocumentAsync(SessionContext s, long id, CancellationToken ct = default);
    Task CancelDraftAsync(SessionContext s, long id, byte[] version, CancellationToken ct = default);
    Task<PostingResult> PostAsync(SessionContext s, PostDocumentRequest request, CancellationToken ct = default);
    Task<PostingResult?> FindPostingAsync(SessionContext s, Guid requestId, CancellationToken ct = default);
    Task<BalancePreview> PreviewAsync(SessionContext s, long productId, CancellationToken ct = default);
    Task AddCorrectionNoteAsync(SessionContext s, long documentId, string note, CancellationToken ct = default);
    Task<DataTable> ListStocktakesAsync(SessionContext s, int page = 1, CancellationToken ct = default);
    Task<StocktakeView> OpenStocktakeAsync(SessionContext s, IReadOnlyList<long> ids, CancellationToken ct = default);
    Task<StocktakeView> GetStocktakeAsync(SessionContext s, long id, CancellationToken ct = default);
    Task<StocktakeView> CountStocktakeAsync(SessionContext s, long id, byte[] version, IReadOnlyList<StocktakeCount> counts, CancellationToken ct = default);
    Task<StocktakeView> ReloadStocktakeAsync(SessionContext s, long id, CancellationToken ct = default);
    Task<StocktakeView> PostStocktakeAsync(SessionContext s, long id, byte[] version, Guid requestId, CancellationToken ct = default);
    Task CancelStocktakeAsync(SessionContext s, long id, byte[] version, CancellationToken ct = default);
    Task<DashboardView> DashboardAsync(SessionContext s, CancellationToken ct = default);
    Task<ReportSnapshot> ReportAsync(SessionContext s, ReportQuery query, CancellationToken ct = default);
    Task RecordExportAsync(SessionContext s, ReportSnapshot snapshot, CancellationToken ct = default);
    Task<DataTable> UsersAsync(SessionContext s, CancellationToken ct = default);
    Task SaveUserAsync(SessionContext s, UserEdit user, CancellationToken ct = default);
    Task<DataTable> AuditAsync(SessionContext s, int page = 1, CancellationToken ct = default);
    Task<Dictionary<string, string>> SettingsAsync(SessionContext s, CancellationToken ct = default);
    Task SaveSettingsAsync(SessionContext s, string storeName, string timeZone, CancellationToken ct = default);
    Task BackupAsync(SessionContext s, string path, CancellationToken ct = default);
    Task<BackupInfo> InspectBackupAsync(SessionContext s, string path, CancellationToken ct = default);
    Task RestoreAsync(SessionContext s, BackupInfo confirmed, string confirmation, CancellationToken ct = default);
}
