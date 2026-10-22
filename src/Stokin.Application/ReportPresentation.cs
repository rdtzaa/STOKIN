using System.Data;
namespace Stokin.Application;

public static class ReportPresentation
{
    public static void AppendValuationTotal(DataTable table)
    {
        decimal value = table.Rows.Cast<DataRow>().Sum(r => r["NilaiPersediaan"] is decimal v ? v : 0); var total = table.NewRow(); total["SKU"] = "TOTAL"; total["Nama"] = "Total nilai persediaan"; total["NilaiPersediaan"] = value; table.Rows.Add(total);
    }
    public static DateTimeOffset StoreTime(DateTimeOffset instant, string zone) => TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(zone));
    public static object ExportMetadata(ReportSnapshot snapshot, DateTimeOffset exportedAt) => new { Store = snapshot.StoreName, SnapshotId = snapshot.Id, SnapshotAt = snapshot.AsOf, ExportedAt = StoreTime(exportedAt, snapshot.TimeZone), TimeZone = snapshot.TimeZone, Filters = snapshot.Query };
}
