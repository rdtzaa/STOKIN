using Microsoft.Data.SqlClient;
using Stokin.Infrastructure.Database;
using Stokin.Testing;
namespace Stokin.IntegrationTests;

public class SchemaTests
{
    internal static string Connection => Environment.GetEnvironmentVariable("STOKIN_TEST_CONNECTION") ?? @"Server=.\SQLEXPRESS;Database=Stokin_Test_Development;Integrated Security=true;Encrypt=false;TrustServerCertificate=true;Connect Timeout=5";
    [Test(sql: true)]
    public async Task Migration_creates_integrity_constraints()
    {
        var connection = Environment.GetEnvironmentVariable("STOKIN_TEST_CONNECTION")!; if (!new SqlConnectionStringBuilder(connection).InitialCatalog.StartsWith("Stokin_Test_", StringComparison.Ordinal)) throw new Exception("Database tes harus berawalan Stokin_Test_."); await SchemaMigrator.CreateDatabaseAsync(connection); var store = new SqlStore(connection); await new SchemaMigrator(store).ApplyAsync();
        var version = await store.ReadAsync(w => w.ScalarAsync("SELECT MAX(Version) FROM SchemaVersions")); Assert.Equal(2, Convert.ToInt32(version));
    }
}
