using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace Stokin.Infrastructure.Database;

public sealed class SchemaMigrator(SqlStore store)
{
    public async Task ApplyAsync(CancellationToken ct = default)
    {
        await store.ReadAsync(async w => { await w.ExecuteAsync("IF OBJECT_ID('SchemaVersions') IS NULL CREATE TABLE SchemaVersions(Version int PRIMARY KEY, Checksum nvarchar(64) NOT NULL, AppliedAt datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET())"); return 0; }, ct);
        var asm = typeof(SchemaMigrator).Assembly;
        var resources = asm.GetManifestResourceNames().Where(n => n.EndsWith(".sql")).Order().ToArray();
        int installed = Convert.ToInt32(await store.ReadAsync(w => w.ScalarAsync("SELECT COALESCE(MAX(Version),0) FROM SchemaVersions"), ct));
        if (installed > resources.Length) throw new InvalidOperationException("Versi skema lebih baru daripada aplikasi. Gunakan versi aplikasi yang sesuai.");
        for (int i = 0; i < resources.Length; i++)
        {
            using var stream = asm.GetManifestResourceStream(resources[i])!; using var reader = new StreamReader(stream); var script = await reader.ReadToEndAsync(ct);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script))); int version = i + 1;
            await store.TransactionAsync(async w =>
            {
                var prior = await w.ScalarAsync("SELECT Checksum FROM SchemaVersions WITH(UPDLOCK,HOLDLOCK) WHERE Version=@v", ("@v", version));
                if (prior != null) { if (!Equals(prior, hash)) throw new InvalidOperationException("Checksum migrasi berbeda. Gunakan salinan database untuk pemeriksaan."); return 0; }
                foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)) if (!string.IsNullOrWhiteSpace(batch)) await w.ExecuteAsync(batch);
                await w.ExecuteAsync("INSERT SchemaVersions(Version,Checksum) VALUES(@v,@hash)", ("@v", version), ("@hash", hash)); return 0;
            }, ct);
        }
    }
    public static async Task CreateDatabaseAsync(string connectionString, CancellationToken ct = default)
    {
        var b = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString); var name = b.InitialCatalog;
        if (!Regex.IsMatch(name, @"^Stokin(?:_[A-Za-z0-9_]+)?$")) throw new ArgumentException("Nama database harus Stokin atau Stokin_nama; tidak menerima SQL bebas.");
        b.InitialCatalog = "master"; var admin = new SqlStore(b.ConnectionString);
        await admin.ReadAsync(async w => { var exists = await w.ScalarAsync("SELECT DB_ID(@name)", ("@name", name)); if (exists == null) await w.ExecuteAsync($"CREATE DATABASE [{name}]"); return 0; }, ct);
        await admin.ReadAsync(async w => { await w.ExecuteAsync($"ALTER DATABASE [{name}] SET ALLOW_SNAPSHOT_ISOLATION ON"); return 0; }, ct);
    }
}
