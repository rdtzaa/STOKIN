using System.Data;
using Microsoft.Data.SqlClient;
namespace Stokin.Infrastructure.Database;

public sealed class SqlStore(string connectionString)
{
    public string ConnectionString { get; } = connectionString;
    public async Task<SqlConnection> OpenAsync(CancellationToken ct = default) { var c = new SqlConnection(ConnectionString); try { await c.OpenAsync(ct); return c; } catch { await c.DisposeAsync(); throw; } }
    public async Task<T> TransactionAsync<T>(Func<SqlWork, Task<T>> action, CancellationToken ct = default, IsolationLevel isolation = IsolationLevel.Serializable)
    {
        await using var c = await OpenAsync(ct); await using var tx = (SqlTransaction)await c.BeginTransactionAsync(isolation, ct);
        try { var result = await action(new(c, tx, ct)); await tx.CommitAsync(ct); return result; }
        catch { try { await tx.RollbackAsync(CancellationToken.None); } catch { } throw; }
    }
    public async Task<T> ReadAsync<T>(Func<SqlWork, Task<T>> action, CancellationToken ct = default) { await using var c = await OpenAsync(ct); return await action(new(c, null, ct)); }
}
public sealed class SqlWork(SqlConnection connection, SqlTransaction? transaction, CancellationToken ct)
{
    public SqlCommand Command(string sql, params (string Name, object? Value)[] values)
    {
        var cmd = new SqlCommand(sql, connection, transaction) { CommandTimeout = 30 };
        foreach (var (name, value) in values)
        {
            var p = new SqlParameter { ParameterName = name, Value = value ?? DBNull.Value };
            switch (value)
            {
                case int: p.SqlDbType = SqlDbType.Int; break;
                case long: p.SqlDbType = SqlDbType.BigInt; break;
                case bool: p.SqlDbType = SqlDbType.Bit; break;
                case decimal: p.SqlDbType = SqlDbType.Decimal; p.Precision = 19; p.Scale = 4; break;
                case Guid: p.SqlDbType = SqlDbType.UniqueIdentifier; break;
                case byte[] b: p.SqlDbType = SqlDbType.VarBinary; p.Size = b.Length; break;
                case DateTimeOffset: p.SqlDbType = SqlDbType.DateTimeOffset; break;
                case DateTime: p.SqlDbType = SqlDbType.DateTime2; break;
                default: p.SqlDbType = SqlDbType.NVarChar; p.Size = value is string s && s.Length <= 4000 ? Math.Max(s.Length, 1) : -1; break;
            }
            cmd.Parameters.Add(p);
        }
        return cmd;
    }
    public async Task<int> ExecuteAsync(string sql, params (string, object?)[] p) { using var cmd = Command(sql, p); return await cmd.ExecuteNonQueryAsync(ct); }
    public async Task<object?> ScalarAsync(string sql, params (string, object?)[] p) { using var cmd = Command(sql, p); var v = await cmd.ExecuteScalarAsync(ct); return v is DBNull ? null : v; }
    public async Task<DataTable> TableAsync(string sql, params (string, object?)[] p)
    {
        using var cmd = Command(sql, p); await using var reader = await cmd.ExecuteReaderAsync(ct); var table = new DataTable(); table.Load(reader); return table;
    }
}
