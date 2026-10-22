using Stokin.Testing;
namespace Stokin.IntegrationTests;

public class BackupTests
{
    [Test(sql: true, backup: true)]
    public async Task AT30_restore_into_separate_database_reconciles_snapshot()
    {
        await using var original = await SqlFixture.Create(); long id = await original.Product(); await original.Post("IN", [new(id, 10, 10000)]); string backup = Path.Combine(Environment.GetEnvironmentVariable("STOKIN_SQL_BACKUP_DIR")!, "stokin-test-" + Guid.NewGuid().ToString("N") + ".bak"); await original.Repository.BackupAsync(original.Admin.Context, backup); await original.Post("OUT", [new(id, 2)]);
        await using var target = await SqlFixture.Create(); var info = await target.Repository.InspectBackupAsync(target.Admin.Context, backup); var name = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(target.Store.ConnectionString).InitialCatalog; await target.Repository.RestoreAsync(target.Admin.Context, info, "PULIHKAN " + name); var balance = await target.Store.ReadAsync(w => w.TableAsync("SELECT Quantity,InventoryValue FROM StockBalances WHERE ProductId=@p", ("@p", id))); Assert.Equal(10, Convert.ToInt32(balance.Rows[0]["Quantity"])); Assert.Equal(100000m, Convert.ToDecimal(balance.Rows[0]["InventoryValue"]));
    }
}
