using System.Data;
using System.Reflection;
using Stokin.Application;
namespace Stokin.IntegrationTests;

public class ReportRepositoryDouble : DispatchProxy
{
    int quantity = 1;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name != nameof(IStokinRepository.ReportAsync)) throw new NotSupportedException(method.Name);
        var data = new DataTable(); data.Columns.Add("Stok", typeof(int)); data.Rows.Add(quantity++); return Task.FromResult(new ReportSnapshot(Guid.NewGuid(), 1, false, DateTimeOffset.UtcNow, (ReportQuery)args![1]!, data, "Toko", "Asia/Jakarta"));
    }
}
