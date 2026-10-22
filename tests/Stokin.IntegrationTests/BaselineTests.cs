using System.Diagnostics;
using System.Text.Json;
using Stokin.Application;
using Stokin.Application.Common;
using Stokin.Domain;
using Stokin.Testing;
namespace Stokin.IntegrationTests;

public class BaselineTests
{
    [Test(sql: true, benchmark: true)]
    public async Task AT33_measures_100_reads_and_100_posts_on_baseline_dataset()
    {
        await using var f = await SqlFixture.Create(); var s = f.Admin.Context; await f.Repository.SaveLookupAsync(s, "Categories", null, "Benchmark", true, null); await f.Repository.SaveLookupAsync(s, "Units", null, "pcs", true, null); var cats = await f.Repository.LookupsAsync(s, "Categories"); var units = await f.Repository.LookupsAsync(s, "Units");
        string sql = await File.ReadAllTextAsync(Path.GetFullPath("database/benchmark-seed.sql")); await f.Store.ReadAsync(async w => { using var cmd = w.Command(sql, ("@user", s.UserId), ("@category", cats[0].Id), ("@unit", units[0].Id)); cmd.CommandTimeout = 300; await cmd.ExecuteNonQueryAsync(); return 0; });
        var counts = await f.Store.ReadAsync(w => w.TableAsync("SELECT (SELECT COUNT(*) FROM Products) Products,(SELECT COUNT(*) FROM StockLedger) Ledger")); Assert.Equal(10000, Convert.ToInt32(counts.Rows[0]["Products"])); Assert.Equal(100000, Convert.ToInt32(counts.Rows[0]["Ledger"]));
        var read = new List<double>(); var post = new List<double>(); var watch = new Stopwatch(); for (int i = 0; i < 100; i++) { watch.Restart(); await f.Repository.ProductsAsync(s, new("BENCH-" + i.ToString("D5")), new()); await f.Repository.DashboardAsync(s); read.Add(watch.Elapsed.TotalMilliseconds); }
        var products = await f.Repository.ProductsAsync(s, new(), new(1, 50)); var lines = products.Items.Select(r => new DocumentLine(Convert.ToInt64(r["Id"]), 1, 100)).ToArray(); for (int i = 0; i < 100; i++) { var d = await f.App.SaveDraftAsync(s, new(null, "IN", "Benchmark", null, null, lines)); watch.Restart(); await f.Repository.PostAsync(s, new(d.Id, d.Version, Guid.NewGuid(), DocumentFingerprint.Compute(d))); post.Add(watch.Elapsed.TotalMilliseconds); }
        var result = new { At = DateTimeOffset.UtcNow, OS = Environment.OSVersion.ToString(), Runtime = Environment.Version.ToString(), SqlVersion = await f.Store.ReadAsync(w => w.ScalarAsync("SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(100))")), Sku = 10000, Ledger = 100000, ReadSamples = 100, PostSamples = 100, ReadP95Ms = Metrics.Percentile(read, .95), PostP95Ms = Metrics.Percentile(post, .95), ReadMs = read, PostMs = post }; Directory.CreateDirectory("output/acceptance"); await File.WriteAllTextAsync("output/acceptance/performance.json", JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })); Assert.True(result.ReadP95Ms <= 2000, $"Read p95 {result.ReadP95Ms} > 2000ms"); Assert.True(result.PostP95Ms <= 3000, $"Post p95 {result.PostP95Ms} > 3000ms");
    }
}
