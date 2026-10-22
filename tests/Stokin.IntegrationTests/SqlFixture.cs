using Microsoft.Data.SqlClient;
using Stokin.Application;
using Stokin.Application.Common;
using Stokin.Infrastructure;
using Stokin.Infrastructure.Database;
namespace Stokin.IntegrationTests;

internal sealed class SqlFixture : IAsyncDisposable
{
    public SqlStore Store { get; }
    public SqlStokinRepository Repository { get; }
    public StokinService App { get; }
    public UserSession Admin { get; private set; } = null!; public string Password { get; } = "Test-" + Guid.NewGuid().ToString("N"); readonly string name;
    SqlFixture(string cs) { Store = new(cs); Repository = new(Store); App = new(Repository); name = new SqlConnectionStringBuilder(cs).InitialCatalog; }
    public static async Task<SqlFixture> Create()
    {
        var b = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STOKIN_TEST_CONNECTION")); if (!b.InitialCatalog.StartsWith("Stokin_Test_", StringComparison.Ordinal)) throw new Exception("Target must begin Stokin_Test_."); b.InitialCatalog = "Stokin_Test_" + Guid.NewGuid().ToString("N"); var f = new SqlFixture(b.ConnectionString); await SchemaMigrator.CreateDatabaseAsync(b.ConnectionString); await new SchemaMigrator(f.Store).ApplyAsync(); await f.Repository.SetupAsync(new("Toko uji", "testadmin", f.Password)); f.Admin = await f.Repository.LoginAsync("testadmin", f.Password); return f;
    }
    public async Task<long> Product(string sku = "ABC01", int minimum = 5)
    {
        var s = Admin.Context; var cats = await Repository.LookupsAsync(s, "Categories"); if (cats.Count == 0) await Repository.SaveLookupAsync(s, "Categories", null, "Kategori", true, null); var units = await Repository.LookupsAsync(s, "Units"); if (units.Count == 0) await Repository.SaveLookupAsync(s, "Units", null, "pcs", true, null); cats = await Repository.LookupsAsync(s, "Categories"); units = await Repository.LookupsAsync(s, "Units"); return await App.SaveProductAsync(s, new(null, sku, "Barang " + sku, cats[0].Id, units[0].Id, minimum, 15000, null, true));
    }
    public async Task<(DocumentView Doc, PostingResult Posted)> Post(string type, IReadOnlyList<DocumentLine> lines, string reason = "Penerimaan/pengeluaran uji", long? reference = null, SessionContext? session = null)
    {
        var s = session ?? Admin.Context; var d = await App.SaveDraftAsync(s, new(null, type, reason, null, reference, lines)); var r = await Repository.PostAsync(s, new(d.Id, d.Version, Guid.NewGuid(), DocumentFingerprint.Compute(d))); return (d, r);
    }
    public async ValueTask DisposeAsync()
    {
        // Only the exact generated database belonging to this fixture can be removed.
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^Stokin_Test_[a-f0-9]{32}$")) return; var b = new SqlConnectionStringBuilder(Store.ConnectionString) { InitialCatalog = "master" }; SqlConnection.ClearAllPools(); await new SqlStore(b.ConnectionString).ReadAsync(async w => { await w.ExecuteAsync($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;DROP DATABASE [{name}]"); return 0; });
    }
}
