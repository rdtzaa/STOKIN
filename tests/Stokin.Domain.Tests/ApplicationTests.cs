using System.Data;
using System.Text;
using Stokin.Testing;
using Stokin.Application;
using Stokin.Application.Common;
namespace Stokin.Domain.Tests;

public class ApplicationTests
{
    [Test] public void Master_validation_rejects_negative_minimum() => Assert.Throws<ValidationException>(() => Rules.Product(new(null, "ABC", "Barang", 1, 1, -1, null, null, true)));
    [Test] public void Draft_rejects_duplicate_SKUs() => Assert.Throws<ValidationException>(() => Rules.Draft(new(null, "IN", null, null, null, [new(1, 1, 100), new(1, 2, 100)])));
    [Test]
    public async Task Csv_preserves_unicode_quotes_newlines_and_guards_formula()
    {
        var t = new DataTable(); t.Columns.Add("Nama"); t.Columns.Add("Nilai", typeof(decimal)); t.Rows.Add(" =SUM(A1)\n\"Beras\"", -12.3456m);
        var s = new ReportSnapshot(Guid.NewGuid(), 1, true, DateTimeOffset.UtcNow, new("CurrentStock"), t, "Toko", "Asia/Jakarta"); using var m = new MemoryStream(); await CsvExporter.WriteAsync(s, m);
        var text = Encoding.UTF8.GetString(m.ToArray()); Assert.True(text.Contains("' =SUM(A1)")); Assert.True(text.Contains("\"\"Beras\"\"")); Assert.True(text.Contains("-12.3456")); Assert.True(!text.Contains("'-12.3456"));
    }
}
