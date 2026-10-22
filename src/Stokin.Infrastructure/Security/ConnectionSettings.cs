using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Stokin.Application;
using Stokin.Infrastructure.Database;
namespace Stokin.Infrastructure.Security;

public static class ConnectionSettings
{
    static string Folder => Environment.GetEnvironmentVariable("STOKIN_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "STOKIN");
    static string FilePath => Path.Combine(Folder, "connection.dpapi");
    public static string? Load()
    {
        try { return File.Exists(FilePath) ? Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser)) : null; } catch { return null; }
    }
    public static void Save(string connectionString) { Directory.CreateDirectory(Folder); File.WriteAllBytes(FilePath, ProtectedData.Protect(Encoding.UTF8.GetBytes(connectionString), null, DataProtectionScope.CurrentUser)); }
    public static string Build(string server, string database, bool integrated, string username, string password, bool trustCertificate)
    {
        var b = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database, IntegratedSecurity = integrated, Encrypt = true, TrustServerCertificate = trustCertificate, ConnectTimeout = 8, ApplicationName = "STOKIN" }; if (!integrated) { b.UserID = username; b.Password = password; }
        return b.ConnectionString;
    }
    public static async Task<StokinService> ConnectAsync(string connectionString, bool create, CancellationToken ct = default)
    {
        if (create) await SchemaMigrator.CreateDatabaseAsync(connectionString, ct); var store = new SqlStore(connectionString); await using (var c = await store.OpenAsync(ct)) { }
        await new SchemaMigrator(store).ApplyAsync(ct); return new(new SqlStokinRepository(store));
    }
    public static string Log(Exception e)
    {
        var id = Guid.NewGuid().ToString("N"); try { Directory.CreateDirectory(Folder); File.AppendAllText(Path.Combine(Folder, "technical.log"), $"{DateTimeOffset.UtcNow:O} CorrelationId={id} Type={e.GetType().Name} SQL={(e is SqlException se ? se.Number : 0)}\n{e.StackTrace}\n"); } catch { }
        return id;
    }
}
