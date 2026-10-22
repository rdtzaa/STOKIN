using System.Data;
using Stokin.Application;
using Stokin.Testing;
namespace Stokin.Domain.Tests;

public class ReportPresentationTests
{
    [Test]
    public void Valuation_total_sums_values_without_adding_mixed_units()
    {
        var t = new DataTable(); t.Columns.Add("SKU"); t.Columns.Add("Nama"); t.Columns.Add("Stok", typeof(int)); t.Columns.Add("Satuan"); t.Columns.Add("NilaiPersediaan", typeof(decimal)); t.Rows.Add("A", "Air", 2, "botol", 20000m); t.Rows.Add("B", "Biskuit", 3, "pcs", 15000m); ReportPresentation.AppendValuationTotal(t); Assert.Equal(3, t.Rows.Count); Assert.Equal(35000m, (decimal)t.Rows[2]["NilaiPersediaan"]); Assert.True(t.Rows[2]["Stok"] is DBNull, "Mixed units must not be totalled.");
    }
    [Test]
    public void UTC_instants_are_presented_in_configured_store_zone()
    {
        var result = ReportPresentation.StoreTime(DateTimeOffset.Parse("2026-10-22T00:00:00Z"), "Asia/Makassar"); Assert.Equal(8, result.Hour); Assert.Equal(TimeSpan.FromHours(8), result.Offset);
    }
}
