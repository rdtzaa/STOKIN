using Stokin.Testing;
using Stokin.Domain.Inventory;
using Stokin.Application.Common;
namespace Stokin.Domain.Tests;

public class InventoryTests
{
    [Test]
    public void PRD_moving_average_and_stocktake()
    {
        var b = InventoryMath.Receive(new(0, 0), 10, 10000).After;
        b = InventoryMath.Receive(b, 10, 14000).After;
        b = InventoryMath.Issue(b, 5).After;
        Assert.Equal(new StockBalance(15, 180000), b);
        Assert.Equal(new StockBalance(14, 168000), InventoryMath.AdjustQuantity(b, -1, null).After);
    }
    [Test]
    public void Last_issue_clears_rounding_residue()
    {
        var first = InventoryMath.Issue(new(3, 1), 1);
        Assert.Equal(-0.3333m, first.ValueDelta);
        Assert.Equal(0.6667m, first.After.InventoryValue);
        Assert.Equal(new StockBalance(0, 0), InventoryMath.Issue(first.After, 2).After);
    }
    [Test]
    [Case(5, 5, "Menipis")]
    [Case(0, 0, "Habis")]
    [Case(1, 0, "Aman")]
    public void Stock_status_boundaries(int q, int min, string expected) => Assert.Equal(expected, InventoryMath.Status(q, min));
    [Test] public void Rejects_negative_stock() => Assert.Throws<ArgumentException>(() => InventoryMath.Issue(new(10, 100), 11));
    [Test] public void Rejects_excess_precision() => Assert.Throws<ArgumentException>(() => InventoryMath.Receive(new(0, 0), 1, 0.00001m));
    [Test] public void Zero_stock_positive_adjustment_needs_cost() => Assert.Throws<ArgumentException>(() => InventoryMath.AdjustQuantity(new(0, 0), 2, null));
    [Test] public void Value_correction_keeps_quantity() => Assert.Equal(new StockBalance(15, 180000), InventoryMath.AdjustValue(new(15, 165000), 180000).After);
    [Test] public void Rejects_quantity_overflow() => Assert.Throws<OverflowException>(() => InventoryMath.Receive(new(int.MaxValue, 1), 1, 1));
    [Test]
    public void Page_defaults_and_domain_isolation()
    {
        Assert.Equal(50, new PageRequest().Size);
        Assert.DoesNotContain(typeof(InventoryMath).Assembly.GetReferencedAssemblies(), x => x.Name!.Contains("SqlClient") || x.Name.Contains("Desktop"));
    }
}
