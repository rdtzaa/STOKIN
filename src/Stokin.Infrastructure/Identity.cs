using Stokin.Application;
using Stokin.Application.Common;
using Stokin.Infrastructure.Security;
namespace Stokin.Infrastructure;

public sealed partial class SqlStokinRepository
{
    public Task<bool> IsSetupAsync(CancellationToken ct = default) => db.ReadAsync(async w => Id(await w.ScalarAsync("SELECT COUNT(*) FROM Users")) > 0, ct);
    public async Task SetupAsync(SetupRequest r, CancellationToken ct = default)
    {
        var name = Rules.Text(r.Username, 50, "Nama akun"); var toko = Rules.Text(r.StoreName, 100, "Nama toko"); var hash = PasswordHasher.Hash(r.Password);
        await db.TransactionAsync(async w =>
        {
            await MaintenanceGate(w); await Lock(w, "STOKIN_SETUP"); if (Id(await w.ScalarAsync("SELECT COUNT(*) FROM Users WITH(UPDLOCK,HOLDLOCK)")) != 0) throw new ValidationException("Setup sudah selesai. Gunakan akun yang tersedia.");
            var id = Id(await w.ScalarAsync("INSERT Users(Username,PasswordHash,RoleId) OUTPUT INSERTED.Id VALUES(@name,@hash,1)", ("@name", name), ("@hash", hash)));
            await w.ExecuteAsync("UPDATE Settings SET Value=@v WHERE [Key]='StoreName'", ("@v", toko)); await Audit(w, id, "SETUP", "Users", id, null, new { Username = name, Role = "Admin" }); return 0;
        }, ct);
    }
    public async Task<UserSession> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        UserSession? result = await db.TransactionAsync(async w =>
        {
            await MaintenanceGate(w); var rows = await w.TableAsync("SELECT * FROM Users WITH(UPDLOCK,HOLDLOCK) WHERE Username=@name", ("@name", username.Trim())); var now = await Now(w);
            if (rows.Rows.Count == 0) { PasswordHasher.Verify(password, DummyHash.Value); await Audit(w, null, "LOGIN", "Users", null, result: "FAILED"); return null; }
            var u = rows.Rows[0]; var id = Id(u["Id"]); bool locked = u["LockedUntil"] is DateTimeOffset until && until > now;
            bool valid = PasswordHasher.Verify(password, Str(u, "PasswordHash"));
            if (!(bool)u["IsActive"] || locked || !valid)
            {
                if (!locked)
                {
                    var window = u["FailureWindow"] is DateTimeOffset old && now - old < TimeSpan.FromMinutes(15) ? old : now;
                    int fails = window == now ? 1 : Convert.ToInt32(u["FailedCount"]) + 1;
                    await w.ExecuteAsync("UPDATE Users SET FailedCount=@n,FailureWindow=@w,LockedUntil=@until WHERE Id=@id", ("@n", fails), ("@w", window), ("@until", fails >= 5 ? now.AddMinutes(15) : null), ("@id", id));
                }
                await Audit(w, id, "LOGIN", "Users", id, result: "FAILED"); return null;
            }
            var session = Guid.NewGuid(); await w.ExecuteAsync("UPDATE Users SET FailedCount=0,FailureWindow=NULL,LockedUntil=NULL,LastLoginAt=@now WHERE Id=@id; INSERT Sessions(Id,UserId,LastActivity) VALUES(@session,@id,@now)", ("@now", now), ("@id", id), ("@session", session)); await Audit(w, id, "LOGIN", "Users", id);
            return new UserSession(new(session, id), Str(u, "Username"), (UserRole)(int)u["RoleId"]);
        }, ct);
        return result ?? throw new ValidationException("Login tidak berhasil. Periksa kredensial atau coba lagi setelah 15 menit.");
    }
    static readonly Lazy<string> DummyHash = new(() => PasswordHasher.Hash("DummyPassword123!"));
    public async Task LogoutAsync(SessionContext s, CancellationToken ct = default) { await db.ReadAsync(async w => { await w.ExecuteAsync("UPDATE Sessions SET IsClosed=1 WHERE Id=@id AND UserId=@user", ("@id", s.SessionId), ("@user", s.UserId)); return 0; }, ct); }
}
