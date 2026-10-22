using System.Data;
using System.Globalization;
using System.Text;
namespace Stokin.Application;

public static class CsvExporter
{
    public static async Task WriteAsync(ReportSnapshot snapshot, Stream output, CancellationToken ct = default)
    {
        using var writer = new StreamWriter(output, new UTF8Encoding(true), 4096, leaveOpen: true);
        static string Text(string value)
        {
            var first = value.TrimStart(); if (first.Length > 0 && "=+-@".Contains(first[0])) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        await writer.WriteLineAsync(string.Join(",", snapshot.Data.Columns.Cast<DataColumn>().Select(c => Text(c.ColumnName))));
        foreach (DataRow row in snapshot.Data.Rows)
        {
            ct.ThrowIfCancellationRequested(); var cells = new List<string>();
            foreach (var cell in row.ItemArray) cells.Add(cell switch
            {
                null or DBNull => "",
                decimal d => d.ToString("0.0000", CultureInfo.InvariantCulture),
                int or long or short => Convert.ToString(cell, CultureInfo.InvariantCulture)!,
                DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
                DateTime d => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)).ToString("O"),
                _ => Text(cell.ToString() ?? "")
            });
            await writer.WriteLineAsync(string.Join(",", cells));
        }
        await writer.FlushAsync(ct);
    }
}
