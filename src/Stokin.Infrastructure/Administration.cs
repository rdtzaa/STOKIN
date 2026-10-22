using System.Data;
using Microsoft.Data.SqlClient;
using Stokin.Application;
using Stokin.Application.Common;
using Stokin.Infrastructure.Database;
using Stokin.Infrastructure.Security;
namespace Stokin.Infrastructure;

public sealed partial class SqlStokinRepository
{
    public Task<DataTable> UsersAsync(SessionContext s, CancellationToken ct = default) => db.TransactionAsync(async w => { await Authorize(w, s, true); return await w.TableAsync("SELECT u.Id,u.Username NamaAkun,r.Name Peran,u.IsActive Aktif,u.LastLoginAt LoginTerakhir FROM Users u JOIN Roles r ON r.Id=u.RoleId ORDER BY u.Username"); }, ct);
    public async Task SaveUserAsync(SessionContext s, UserEdit user, CancellationToken ct = default)
    {
        var username = Rules.Text(user.Username, 50, "Nama akun"); if (user.Role is not (UserRole.Admin or UserRole.Petugas)) throw new ValidationException("Peran tidak valid."); string? hash = user.Password != null ? PasswordHasher.Hash(user.Password) : null;
        await db.TransactionAsync(async w =>
        {
            await Authorize(w, s, true); await Lock(w, "ACTIVE_ADMINS"); long id = user.Id ?? 0; object? before = null;
            if (user.Id.HasValue)
            {
                var old = await w.TableAsync("SELECT Id,Username,RoleId,IsActive FROM Users WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id", ("@id", id)); if (old.Rows.Count != 1) throw new ValidationException("Akun tidak ditemukan."); var r = old.Rows[0];
                if ((bool)r["IsActive"] && (int)r["RoleId"] == 1 && (!user.IsActive || user.Role != UserRole.Admin) && Id(await w.ScalarAsync("SELECT COUNT(*) FROM Users WITH(UPDLOCK,HOLDLOCK) WHERE RoleId=1 AND IsActive=1")) <= 1) throw new ValidationException("Admin aktif terakhir tidak boleh dinonaktifkan atau diturunkan perannya.");
                before = new { Username = Str(r, "Username"), RoleId = r["RoleId"], IsActive = r["IsActive"] }; await w.ExecuteAsync("UPDATE Users SET Username=@name,RoleId=@role,IsActive=@active,PasswordHash=COALESCE(@hash,PasswordHash),FailedCount=CASE WHEN @hash IS NULL THEN FailedCount ELSE 0 END,LockedUntil=CASE WHEN @hash IS NULL THEN LockedUntil ELSE NULL END WHERE Id=@id", ("@name", username), ("@role", (int)user.Role), ("@active", user.IsActive), ("@hash", hash), ("@id", id));
                if (hash != null || !user.IsActive) await w.ExecuteAsync("UPDATE Sessions SET IsClosed=1 WHERE UserId=@id", ("@id", id));
            }
            else { if (hash == null) throw new ValidationException("Kata sandi akun baru wajib diisi."); id = Id(await w.ScalarAsync("INSERT Users(Username,PasswordHash,RoleId,IsActive) OUTPUT INSERTED.Id VALUES(@name,@hash,@role,@active)", ("@name", username), ("@hash", hash), ("@role", (int)user.Role), ("@active", user.IsActive))); }
            await Audit(w, s.UserId, "SAVE_USER", "Users", id, before, new { Username = username, Role = user.Role.ToString(), user.IsActive, PasswordReset = hash != null }); return 0;
        }, ct);
    }
    public Task<DataTable> AuditAsync(SessionContext s, int page = 1, CancellationToken ct = default) => db.TransactionAsync(async w => { await Authorize(w, s, true); return await w.TableAsync("SELECT a.Id,a.OccurredAt Waktu,u.Username Pengguna,a.Action Aksi,a.Entity Entitas,a.EntityId Referensi,a.BeforeValue Sebelum,a.AfterValue Sesudah,a.Result Hasil,a.CorrelationId FROM AuditLogs a LEFT JOIN Users u ON u.Id=a.ActorId ORDER BY a.Id DESC OFFSET @skip ROWS FETCH NEXT 50 ROWS ONLY", ("@skip", checked((Math.Max(page, 1) - 1) * 50))); }, ct);
    public Task<Dictionary<string, string>> SettingsAsync(SessionContext s, CancellationToken ct = default) => db.TransactionAsync(async w => { await Authorize(w, s, true, maintenance: true); var t = await w.TableAsync("SELECT * FROM Settings"); return t.Rows.Cast<DataRow>().ToDictionary(r => Str(r, "Key"), r => Str(r, "Value")); }, ct);
    public async Task SaveSettingsAsync(SessionContext s, string storeName, string timeZone, CancellationToken ct = default)
    {
        storeName = Rules.Text(storeName, 100, "Nama toko"); TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        await db.TransactionAsync(async w => { await Authorize(w, s, true); var old = await w.ScalarAsync("SELECT Value FROM Settings WHERE [Key]='StoreName'"); await w.ExecuteAsync("UPDATE Settings SET Value=@name WHERE [Key]='StoreName';UPDATE Settings SET Value=@tz WHERE [Key]='TimeZone'", ("@name", storeName), ("@tz", timeZone)); await Audit(w, s.UserId, "SAVE_SETTINGS", "Settings", "Store", old, new { storeName, timeZone }); return 0; }, ct);
    }
    string DatabaseName => new SqlConnectionStringBuilder(db.ConnectionString).InitialCatalog;
    static string Quote(string name) => "[" + name.Replace("]", "]]") + "]";
    public async Task BackupAsync(SessionContext s, string path, CancellationToken ct = default)
    {
        Rules.Text(path, 1000, "Lokasi backup di server"); await CheckAsync(s, true, ct: ct);
        try
        {
            await db.ReadAsync(async w => { using var cmd = w.Command($"BACKUP DATABASE {Quote(DatabaseName)} TO DISK=@path WITH COPY_ONLY,INIT,CHECKSUM", ("@path", path)); cmd.CommandTimeout = 600; await cmd.ExecuteNonQueryAsync(ct); return 0; }, ct);
            await InspectBackupAsync(s, path, ct);
            await db.TransactionAsync(async w => { await Authorize(w, s, true); await w.ExecuteAsync("UPDATE Settings SET Value=@value WHERE [Key]='LastBackup';IF @@ROWCOUNT=0 INSERT Settings VALUES('LastBackup',@value)", ("@value", $"{await Now(w):O}|{path}")); await Audit(w, s.UserId, "BACKUP", "Database", DatabaseName, null, new { Path = path }); return 0; }, ct);
        }
        catch
        {
            try { await db.TransactionAsync(async w => { await Audit(w, s.UserId, "BACKUP", "Database", DatabaseName, null, new { Path = path }, "FAILED"); return 0; }, CancellationToken.None); } catch { }
            throw;
        }
    }
    public async Task<BackupInfo> InspectBackupAsync(SessionContext s, string path, CancellationToken ct = default)
    {
        await CheckAsync(s, true, ct: ct); return await db.ReadAsync(async w =>
        {
            using (var verify = w.Command("RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM", ("@path", path))) { verify.CommandTimeout = 600; await verify.ExecuteNonQueryAsync(ct); }
            var t = await w.TableAsync("RESTORE HEADERONLY FROM DISK=@path", ("@path", path)); if (t.Rows.Count != 1) throw new ValidationException("Backup harus berisi satu set lengkap. Pilih backup STOKIN yang dibuat aplikasi."); var r = t.Rows[0]; if (Convert.ToInt32(r["BackupType"]) != 1) throw new ValidationException("Pilih full database backup."); return new BackupInfo(Str(r, "DatabaseName"), r["BackupFinishDate"] is DateTime d ? new DateTimeOffset(d) : null, path);
        }, ct);
    }
    public async Task RestoreAsync(SessionContext s, BackupInfo confirmed, string confirmation, CancellationToken ct = default)
    {
        if (confirmation != "PULIHKAN " + DatabaseName) throw new ValidationException("Konfirmasi nama database tidak cocok.");
        var actual = await InspectBackupAsync(s, confirmed.ServerPath, ct); if (actual != confirmed) throw new ValidationException("Metadata backup berubah. Periksa kembali.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(confirmed.Database, @"^Stokin(?:_[A-Za-z0-9_]+)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) throw new ValidationException("Pilih backup database STOKIN.");
        var b = new SqlConnectionStringBuilder(db.ConnectionString) { InitialCatalog = "master" }; var master = new SqlStore(b.ConnectionString);
        // A master-scoped session lock survives replacement of the target database. Every application
        // authorization takes the matching shared transaction lock, including login.
        await master.ReadAsync(async w =>
        {
            long gateResult = Id(await w.ScalarAsync("DECLARE @r int;EXEC @r=master.sys.sp_getapplock @Resource=@name,@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=30000;SELECT @r", ("@name", "STOKIN:MAINTENANCE:" + DatabaseName.ToUpperInvariant())));
            if (gateResult < 0) throw new ValidationException("Sesi lain belum selesai. Coba pemulihan kembali.");
            try
            {
                string operatorName = "";
                await MaintenanceWorkflow.RunAsync(
                    restore: async () =>
                    {
                        await db.TransactionAsync(async tx =>
                        {
                            await Authorize(tx, s, true, maintenance: true, gate: false); operatorName = Convert.ToString(await tx.ScalarAsync("SELECT Username FROM Users WHERE Id=@id", ("@id", s.UserId)))!;
                            await tx.ExecuteAsync("UPDATE Settings SET Value='true' WHERE [Key]='Maintenance';UPDATE Sessions SET IsClosed=1 WHERE Id<>@id", ("@id", s.SessionId)); await Audit(tx, s.UserId, "RESTORE_BEGIN", "Database", DatabaseName, null, confirmed); return 0;
                        }, ct);
                        string prebackup = confirmed.ServerPath + ".before-restore-" + (await Now(w)).ToString("yyyyMMddHHmmss") + ".bak";
                        using (var backupCommand = w.Command($"BACKUP DATABASE {Quote(DatabaseName)} TO DISK=@path WITH COPY_ONLY,INIT,CHECKSUM;RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM;", ("@path", prebackup))) { backupCommand.CommandTimeout = 1200; await backupCommand.ExecuteNonQueryAsync(ct); }
                        var files = await w.TableAsync("SELECT physical_name,type FROM sys.master_files WHERE database_id=DB_ID(@name) ORDER BY file_id", ("@name", DatabaseName));
                        var backup = await w.TableAsync("RESTORE FILELISTONLY FROM DISK=@path", ("@path", confirmed.ServerPath)); if (files.Rows.Count != backup.Rows.Count) throw new ValidationException("Struktur file backup berbeda. Gunakan prosedur restore DBA.");
                        var parameters = new List<(string, object?)> { ("@path", confirmed.ServerPath) }; var moves = new List<string>();
                        for (int i = 0; i < backup.Rows.Count; i++)
                        {
                            if ((Str(backup.Rows[i], "Type") == "L") != (Convert.ToInt32(files.Rows[i]["type"]) == 1)) throw new ValidationException("Pemetaan file data/log berbeda. Gunakan operator database.");
                            parameters.Add(("@logical" + i, backup.Rows[i]["LogicalName"])); parameters.Add(("@physical" + i, files.Rows[i]["physical_name"])); moves.Add($"MOVE @logical{i} TO @physical{i}");
                        }
                        SqlConnection.ClearAllPools();
                        using var command = w.Command($"ALTER DATABASE {Quote(DatabaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;RESTORE DATABASE {Quote(DatabaseName)} FROM DISK=@path WITH REPLACE,RECOVERY,{string.Join(",", moves)};USE {Quote(DatabaseName)};UPDATE Settings SET Value='true' WHERE [Key]='Maintenance';UPDATE Sessions SET IsClosed=1;USE master;ALTER DATABASE {Quote(DatabaseName)} SET MULTI_USER;", parameters.ToArray());
                        command.CommandTimeout = 1200; await command.ExecuteNonQueryAsync(ct); SqlConnection.ClearAllPools();
                    },
                    verify: async () =>
                    {
                        await new SchemaMigrator(db).ApplyAsync(ct);
                        await db.TransactionAsync(async tx =>
                        {
                            var mismatch = Id(await tx.ScalarAsync("SELECT COUNT(*) FROM StockBalances b OUTER APPLY(SELECT COALESCE(SUM(CAST(QuantityDelta AS bigint)),0) Q,COALESCE(SUM(ValueDelta),0) V FROM StockLedger WHERE ProductId=b.ProductId) x WHERE b.Quantity<>x.Q OR b.InventoryValue<>x.V"));
                            if (mismatch != 0) throw new ValidationException("Rekonsiliasi gagal. Akses tetap diblokir; gunakan backup sebelum restore dan hubungi operator.");
                            await tx.ExecuteAsync("UPDATE Sessions SET IsClosed=1"); return 0;
                        }, ct);
                    },
                    release: async () =>
                    {
                        await db.TransactionAsync(async tx =>
                        {
                            var actor = await tx.ScalarAsync("SELECT Id FROM Users WHERE Username=@name", ("@name", operatorName)); await Audit(tx, actor == null ? null : Id(actor), "RESTORE_COMPLETE", "Database", DatabaseName, null, new { Backup = confirmed, Operator = operatorName }); await tx.ExecuteAsync("UPDATE Settings SET Value='false' WHERE [Key]='Maintenance'"); return 0;
                        }, ct);
                    },
                    retain: async () =>
                    {
                        try
                        {
                            using var blocked = w.Command($"USE master;ALTER DATABASE {Quote(DatabaseName)} SET MULTI_USER;USE {Quote(DatabaseName)};UPDATE Settings SET Value='true' WHERE [Key]='Maintenance';UPDATE Sessions SET IsClosed=1;USE master;"); blocked.CommandTimeout = 30; await blocked.ExecuteNonQueryAsync(CancellationToken.None);
                        }
                        catch
                        {
                            // If the restored schema is unusable, do not reopen it to normal sessions.
                            using var single = w.Command($"USE master;ALTER DATABASE {Quote(DatabaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;"); await single.ExecuteNonQueryAsync(CancellationToken.None);
                        }
                    });
                return 0;
            }
            finally
            {
                using var releaseLock = w.Command("USE master;EXEC master.sys.sp_releaseapplock @Resource=@name,@LockOwner='Session';", ("@name", "STOKIN:MAINTENANCE:" + DatabaseName.ToUpperInvariant())); await releaseLock.ExecuteNonQueryAsync(CancellationToken.None);
            }
        }, ct); // Explicit release happens only after success or the blocked recovery state is retained.
    }
}
