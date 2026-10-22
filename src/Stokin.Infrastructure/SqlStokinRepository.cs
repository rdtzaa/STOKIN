using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Stokin.Application;
using Stokin.Application.Common;
using Stokin.Infrastructure.Database;
namespace Stokin.Infrastructure;

public sealed partial class SqlStokinRepository(SqlStore store) : IStokinRepository
{
    readonly SqlStore db = store;
    static long Id(object? value) => Convert.ToInt64(value);
    static string Str(DataRow r, string name) => r[name] is DBNull ? "" : Convert.ToString(r[name])!;
    static byte[] Version(DataRow r) => (byte[])r["Version"];
    static async Task<DateTimeOffset> Now(SqlWork w) => (DateTimeOffset)(await w.ScalarAsync("SELECT TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00')"))!;
    static async Task Audit(SqlWork w, long? actor, string action, string entity, object? id, object? before = null, object? after = null, string result = "SUCCESS")
    {
        await w.ExecuteAsync("INSERT AuditLogs(ActorId,OccurredAt,Action,Entity,EntityId,BeforeValue,AfterValue,Result,CorrelationId) VALUES(@actor,TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00'),@action,@entity,@id,@before,@after,@result,@corr)", ("@actor", actor), ("@action", action), ("@entity", entity), ("@id", id?.ToString()), ("@before", before == null ? null : JsonSerializer.Serialize(before)), ("@after", after == null ? null : JsonSerializer.Serialize(after)), ("@result", result), ("@corr", Guid.NewGuid()));
    }
    static async Task MaintenanceGate(SqlWork w)
    {
        string database = Convert.ToString(await w.ScalarAsync("SELECT DB_NAME()"))!; long result = Id(await w.ScalarAsync("DECLARE @r int;EXEC @r=master.sys.sp_getapplock @Resource=@name,@LockMode='Shared',@LockOwner='Transaction',@LockTimeout=1000;SELECT @r", ("@name", "STOKIN:MAINTENANCE:" + database.ToUpperInvariant()))); if (result < 0) throw new ValidationException("Pemulihan database sedang berjalan. Tunggu operator menyelesaikan verifikasi.");
    }
    static async Task<UserRole> Authorize(SqlWork w, SessionContext s, bool admin = false, bool activity = true, bool maintenance = false, bool gate = true)
    {
        if (gate) await MaintenanceGate(w);
        var rows = await w.TableAsync("SELECT u.RoleId,u.IsActive,s.LastActivity,s.IsClosed FROM Sessions s JOIN Users u ON s.UserId=u.Id WHERE s.Id=@session AND u.Id=@user", ("@session", s.SessionId), ("@user", s.UserId));
        var now = await Now(w); if (rows.Rows.Count != 1 || !(bool)rows.Rows[0]["IsActive"] || (bool)rows.Rows[0]["IsClosed"] || now - (DateTimeOffset)rows.Rows[0]["LastActivity"] >= TimeSpan.FromMinutes(15)) throw new SessionExpiredException();
        var role = (UserRole)(int)rows.Rows[0]["RoleId"]; if (admin && role != UserRole.Admin) throw new AuthorizationException();
        if (!maintenance && Equals(await w.ScalarAsync("SELECT Value FROM Settings WHERE [Key]='Maintenance'"), "true")) throw new ValidationException("Mode pemeliharaan aktif. Pembukuan dihentikan.");
        if (activity) await w.ExecuteAsync("UPDATE Sessions SET LastActivity=@now WHERE Id=@id", ("@now", now), ("@id", s.SessionId)); return role;
    }
    public Task<UserRole> CheckAsync(SessionContext s, bool admin = false, bool activity = true, CancellationToken ct = default) => db.TransactionAsync(w => Authorize(w, s, admin, activity), ct);
    static void Match(byte[]? expected, byte[] actual) { if (expected == null || !expected.SequenceEqual(actual)) throw new ConcurrencyException(); }
    static async Task Lock(SqlWork w, string resource) { var result = Id(await w.ScalarAsync("DECLARE @r int; EXEC @r=sp_getapplock @Resource=@name,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; SELECT @r", ("@name", resource))); if (result < 0) throw new ConcurrencyException("Data sedang dipakai. Coba kembali."); }
    static bool Sensitive(string type) => type is "OPENING" or "QUANTITY_ADJUSTMENT" or "VALUE_ADJUSTMENT";
    static void Own(UserRole role, SessionContext s, DataRow doc) { if (role != UserRole.Admin && (Sensitive(Str(doc, "Type")) || Id(doc["CreatedBy"]) != s.UserId)) throw new AuthorizationException(); }
    static string Fingerprint(DocumentView d) => DocumentFingerprint.Compute(d);
    public static string ContentHash(DocumentView d) => Fingerprint(d);
}
