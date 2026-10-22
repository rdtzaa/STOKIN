using System.Data;
using Microsoft.Data.SqlClient;
using Stokin.Application;
using Stokin.Application.Common;
using Stokin.Testing;
namespace Stokin.IntegrationTests;

public class AcceptanceTests
{
    [Test(sql: true)]
    public async Task AT04_moving_average_and_AT24_stocktake_reconcile()
    {
        await using var f = await SqlFixture.Create(); long p = await f.Product(); await f.Post("OPENING", [new(p, 10, 10000)]); await f.Post("IN", [new(p, 10, 14000)]); await f.Post("OUT", [new(p, 5)]); var b = await f.Repository.PreviewAsync(f.Admin.Context, p); Assert.Equal(15, b.Quantity); Assert.Equal(180000m, b.Value);
        var take = await f.Repository.OpenStocktakeAsync(f.Admin.Context, [p]); take = await f.Repository.CountStocktakeAsync(f.Admin.Context, take.Id, take.Version, [new(p, 14, "Rusak")]); await f.Repository.PostStocktakeAsync(f.Admin.Context, take.Id, take.Version, Guid.NewGuid()); b = await f.Repository.PreviewAsync(f.Admin.Context, p); Assert.Equal(14, b.Quantity); Assert.Equal(168000m, b.Value);
        long mismatch = Convert.ToInt64(await f.Store.ReadAsync(w => w.ScalarAsync("SELECT COUNT(*) FROM StockBalances b OUTER APPLY(SELECT SUM(QuantityDelta) Q,SUM(ValueDelta) V FROM StockLedger WHERE ProductId=b.ProductId) x WHERE b.Quantity<>COALESCE(x.Q,0) OR b.InventoryValue<>COALESCE(x.V,0)"))); Assert.Equal(0L, mismatch);
    }
    [Test(sql: true)]
    public async Task AT01_dashboard_counts_and_AT07_duplicate_SKUs()
    {
        await using var f = await SqlFixture.Create(); long a = await f.Product("A"), b = await f.Product("B"), c = await f.Product("C"); await f.Post("IN", [new(b, 3, 100), new(c, 10, 100)]); await f.Repository.SaveLookupAsync(f.Admin.Context, "Categories", null, "Kategori kosong", true, null); var d = await f.Repository.DashboardAsync(f.Admin.Context); Assert.Equal(3, d.TotalProducts); Assert.Equal(2, d.TotalCategories); Assert.Equal(1, d.LowStock); Assert.Equal(1, d.OutOfStock);
        await Assert.ThrowsAsync<SqlException>(async () => await f.Product("a")); Assert.Equal(a, Convert.ToInt64(d.Priority.Rows[0]["Id"]));
    }
    [Test(sql: true)]
    public async Task AT15_competing_issues_never_make_negative_stock()
    {
        await using var f = await SqlFixture.Create(); long p = await f.Product(); await f.Post("IN", [new(p, 10, 100)]); var second = await f.Repository.LoginAsync("testadmin", f.Password); var a = await f.App.SaveDraftAsync(f.Admin.Context, new(null, "OUT", "Penjualan", null, null, [new(p, 7)])); var b = await f.App.SaveDraftAsync(second.Context, new(null, "OUT", "Penjualan", null, null, [new(p, 7)]));
        async Task<bool> Try(SessionContext s, DocumentView d) { try { await f.Repository.PostAsync(s, new(d.Id, d.Version, Guid.NewGuid(), DocumentFingerprint.Compute(d))); return true; } catch (ArgumentException) { return false; } catch (SqlException e) when (e.Number == 1205) { return false; } }
        var result = await Task.WhenAll(Try(f.Admin.Context, a), Try(second.Context, b)); Assert.Equal(1, result.Count(x => x)); Assert.Equal(3, (await f.Repository.PreviewAsync(f.Admin.Context, p)).Quantity);
    }
    [Test(sql: true)]
    public async Task AT16_request_replay_and_AT18_lookup_do_not_double_post()
    {
        await using var f = await SqlFixture.Create(); long p = await f.Product(); var d = await f.App.SaveDraftAsync(f.Admin.Context, new(null, "IN", null, null, null, [new(p, 10, 100)])); var request = new PostDocumentRequest(d.Id, d.Version, Guid.NewGuid(), DocumentFingerprint.Compute(d)); await f.Repository.PostAsync(f.Admin.Context, request); var replay = await f.Repository.PostAsync(f.Admin.Context, request); Assert.True(replay.Replayed); Assert.Equal(d.Id, (await f.Repository.FindPostingAsync(f.Admin.Context, request.RequestId))!.DocumentId); Assert.Equal(10, (await f.Repository.PreviewAsync(f.Admin.Context, p)).Quantity); await Assert.ThrowsAsync<ValidationException>(() => f.Repository.PostAsync(f.Admin.Context, request with { ExpectedContentHash = "different" }));
    }
    [Test(sql: true)]
    public async Task AT17_second_line_failure_rolls_back_every_side_effect()
    {
        await using var f = await SqlFixture.Create(); long a = await f.Product("A"), b = await f.Product("B"); var d = await f.App.SaveDraftAsync(f.Admin.Context, new(null, "IN", null, null, null, [new(a, 1, 100), new(b, 1, 100)]));
        await f.Store.ReadAsync(async w => { await w.ExecuteAsync($"CREATE TRIGGER TestFailure ON StockLedger AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE ProductId={b}) THROW 51999,'Simulated second line failure',1;"); return 0; });
        await Assert.ThrowsAsync<SqlException>(() => f.Repository.PostAsync(f.Admin.Context, new(d.Id, d.Version, Guid.NewGuid(), DocumentFingerprint.Compute(d)))); Assert.Equal(0, (await f.Repository.PreviewAsync(f.Admin.Context, a)).Quantity); Assert.Equal(0L, Convert.ToInt64(await f.Store.ReadAsync(w => w.ScalarAsync("SELECT COUNT(*) FROM StockLedger")))); Assert.Equal("DRAFT", (await f.Repository.GetDocumentAsync(f.Admin.Context, d.Id)).Status); Assert.Equal(0L, Convert.ToInt64(await f.Store.ReadAsync(w => w.ScalarAsync("SELECT COUNT(*) FROM AuditLogs WHERE Action='POST_DOCUMENT'"))));
    }
    [Test(sql: true)]
    public async Task AT20_value_correction_preserves_historical_issue_cost_and_AT22_conflicts()
    {
        await using var f = await SqlFixture.Create(); long p = await f.Product(); await f.Post("OPENING", [new(p, 10, 10000)]); var incoming = await f.Post("IN", [new(p, 10, 12000)]); var outgoing = await f.Post("OUT", [new(p, 5)]); var before = await f.Repository.PreviewAsync(f.Admin.Context, p);
        await f.Post("VALUE_ADJUSTMENT", [new(p, 0, TargetValue: 180000, BalanceVersion: before.Version, CalculationBasis: "15 tersisa × 12000", SnapshotQuantity: 15, SnapshotValue: 165000)], "Koreksi biaya", incoming.Doc.Id); var after = await f.Repository.PreviewAsync(f.Admin.Context, p); Assert.Equal(15, after.Quantity); Assert.Equal(180000m, after.Value); Assert.Equal(55000m, Convert.ToDecimal((await f.Repository.GetDocumentAsync(f.Admin.Context, outgoing.Doc.Id)).Display.Rows[0]["Nilai"]));
        var draft = await f.App.SaveDraftAsync(f.Admin.Context, new(null, "VALUE_ADJUSTMENT", "Uji konflik", null, incoming.Doc.Id, [new(p, 0, TargetValue: 150000, BalanceVersion: after.Version, CalculationBasis: "15 × 10000", SnapshotQuantity: 15, SnapshotValue: 180000)])); await f.Post("IN", [new(p, 1, 10000)]); await Assert.ThrowsAsync<ConcurrencyException>(() => f.Repository.PostAsync(f.Admin.Context, new(draft.Id, draft.Version, Guid.NewGuid(), DocumentFingerprint.Compute(draft))));
    }
    [Test(sql: true)]
    public async Task AT25_stale_stocktake_is_atomic_and_AT26_zero_difference_has_no_movement()
    {
        await using var f = await SqlFixture.Create(); long p = await f.Product(); var t = await f.Repository.OpenStocktakeAsync(f.Admin.Context, [p]); t = await f.Repository.CountStocktakeAsync(f.Admin.Context, t.Id, t.Version, [new(p, 0, null)]); await f.Repository.PostStocktakeAsync(f.Admin.Context, t.Id, t.Version, Guid.NewGuid()); Assert.Equal(0L, Convert.ToInt64(await f.Store.ReadAsync(w => w.ScalarAsync("SELECT COUNT(*) FROM StockLedger"))));
        t = await f.Repository.OpenStocktakeAsync(f.Admin.Context, [p]); t = await f.Repository.CountStocktakeAsync(f.Admin.Context, t.Id, t.Version, [new(p, 2, "Temuan fisik", 12000)]); await f.Post("IN", [new(p, 1, 10000)]); await Assert.ThrowsAsync<ConcurrencyException>(() => f.Repository.PostStocktakeAsync(f.Admin.Context, t.Id, t.Version, Guid.NewGuid())); Assert.Equal(1, (await f.Repository.PreviewAsync(f.Admin.Context, p)).Quantity);
    }
    [Test(sql: true)]
    public async Task AT28_cost_permissions_and_revoked_export_leave_output_empty()
    {
        await using var f = await SqlFixture.Create(); long p = await f.Product(); await f.Repository.SaveUserAsync(f.Admin.Context, new(null, "petugas", UserRole.Petugas, true, f.Password)); var staff = await f.Repository.LoginAsync("petugas", f.Password); var own = await f.Post("IN", [new(p, 2, 100)], session: staff.Context); Assert.Equal(100m, (await f.Repository.GetDocumentAsync(staff.Context, own.Doc.Id)).Lines[0].UnitCost!.Value);
        var other = await f.Post("IN", [new(p, 1, 200)]); Assert.Null((await f.Repository.GetDocumentAsync(staff.Context, other.Doc.Id)).Lines[0].UnitCost); await Assert.ThrowsAsync<AuthorizationException>(() => f.Repository.ReportAsync(staff.Context, new("Valuation")));
        var snapshot = await f.Repository.ReportAsync(f.Admin.Context, new("Valuation")); await f.Repository.SaveUserAsync(f.Admin.Context, new(null, "admin2", UserRole.Admin, true, f.Password)); await f.Repository.SaveUserAsync(f.Admin.Context, new(f.Admin.Context.UserId, "testadmin", UserRole.Petugas, true)); using var m = new MemoryStream(); await Assert.ThrowsAsync<AuthorizationException>(() => f.App.ExportAsync(f.Admin.Context, snapshot, m)); Assert.Equal(0L, m.Length);
    }
    [Test(sql: true)]
    public async Task AT29_lockout_expired_session_and_last_admin()
    {
        await using var f = await SqlFixture.Create(); await Assert.ThrowsAsync<ValidationException>(() => f.Repository.SaveUserAsync(f.Admin.Context, new(f.Admin.Context.UserId, "testadmin", UserRole.Petugas, true))); for (int i = 0; i < 5; i++) await Assert.ThrowsAsync<ValidationException>(() => f.Repository.LoginAsync("testadmin", "wrong")); await Assert.ThrowsAsync<ValidationException>(() => f.Repository.LoginAsync("testadmin", f.Password));
        await f.Store.ReadAsync(async w => { await w.ExecuteAsync("UPDATE Sessions SET LastActivity=DATEADD(minute,-16,TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00')) WHERE Id=@id", ("@id", f.Admin.Context.SessionId)); return 0; }); await Assert.ThrowsAsync<SessionExpiredException>(() => f.Repository.CheckAsync(f.Admin.Context));
    }
    [Test(sql: true)]
    public async Task AT27_report_snapshot_and_exclusive_end_remain_consistent()
    {
        await using var f = await SqlFixture.Create(); long p = await f.Product(); await f.Post("IN", [new(p, 10, 100)]); var outgoing = await f.Post("OUT", [new(p, 2)]); var card = await f.Repository.ReportAsync(f.Admin.Context, new("StockCard", ProductId: p, Start: outgoing.Posted.PostedAt, End: outgoing.Posted.PostedAt.AddMinutes(1))); Assert.Equal(10, Convert.ToInt32(card.Data.Rows[0]["Saldo"])); Assert.Equal(8, Convert.ToInt32(card.Data.Rows[1]["Saldo"]));
        var snap = await f.Repository.ReportAsync(f.Admin.Context, new("CurrentStock")); await f.Post("IN", [new(p, 5, 100)]); Assert.Equal(8, Convert.ToInt32(snap.Data.Rows[0]["Stok"])); var exclusive = await f.Repository.ReportAsync(f.Admin.Context, new("StockCard", ProductId: p, End: outgoing.Posted.PostedAt)); Assert.Equal(2, exclusive.Data.Rows.Count);
    }
}
