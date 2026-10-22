namespace Stokin.Domain.Inventory;

public record StockBalance(int Quantity, decimal InventoryValue);
public record MovementResult(StockBalance After, int QuantityDelta, decimal ValueDelta);
public static class InventoryMath
{
    public const decimal MaxMoney = 999999999999999.9999m;
    public static void Money(decimal value, bool zero = false)
    {
        if (value < 0 || (!zero && value == 0) || value > MaxMoney || decimal.Round(value, 4) != value) throw new ArgumentException("Nilai rupiah harus positif, maksimum 4 desimal, dan dalam batas yang diizinkan.");
    }
    static void Check(StockBalance b) { if (b.Quantity < 0 || b.InventoryValue < 0 || (b.Quantity == 0 && b.InventoryValue != 0)) throw new ArgumentException("Saldo tidak valid."); Money(b.InventoryValue, true); }
    public static MovementResult Receive(StockBalance b, int q, decimal c)
    {
        Check(b); if (q <= 0) throw new ArgumentException("Jumlah harus lebih dari nol."); Money(c);
        int next = checked(b.Quantity + q); decimal value = checked(b.InventoryValue + q * c); Money(value, true);
        return new(new(next, value), q, q * c);
    }
    public static MovementResult Issue(StockBalance b, int q)
    {
        Check(b); if (q <= 0 || q > b.Quantity) throw new ArgumentException("Jumlah keluar melebihi stok atau tidak valid.");
        decimal cost = q == b.Quantity ? b.InventoryValue : decimal.Round(q * (b.InventoryValue / b.Quantity), 4, MidpointRounding.AwayFromZero);
        return new(new(b.Quantity - q, b.InventoryValue - cost), -q, -cost);
    }
    public static MovementResult AdjustQuantity(StockBalance b, int delta, decimal? zeroStockUnitCost)
    {
        Check(b); if (delta == 0) throw new ArgumentException("Selisih tidak boleh nol."); if (delta < 0) return Issue(b, checked(-delta));
        if (b.Quantity == 0) return Receive(b, delta, zeroStockUnitCost ?? throw new ArgumentException("Biaya unit wajib untuk stok nol."));
        decimal value = decimal.Round(delta * (b.InventoryValue / b.Quantity), 4, MidpointRounding.AwayFromZero); Money(value, true); Money(b.InventoryValue + value, true);
        return new(new(checked(b.Quantity + delta), b.InventoryValue + value), delta, value);
    }
    public static MovementResult AdjustValue(StockBalance b, decimal target)
    {
        Check(b); Money(target); if (b.Quantity == 0 || b.InventoryValue == target) throw new ArgumentException("Koreksi memerlukan stok positif dan target berbeda.");
        return new(new(b.Quantity, target), 0, target - b.InventoryValue);
    }
    public static string Status(int q, int min)
    {
        if (q < 0 || min < 0) throw new ArgumentException("Stok dan minimum tidak boleh negatif."); return q == 0 ? "Habis" : q <= min ? "Menipis" : "Aman";
    }
}
