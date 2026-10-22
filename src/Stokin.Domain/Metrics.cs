namespace Stokin.Domain;

public static class Metrics
{
    public static double Percentile(IReadOnlyList<double> values, double quantile)
    {
        if (values.Count == 0 || quantile <= 0 || quantile > 1 || values.Any(x => !double.IsFinite(x) || x < 0)) throw new ArgumentException("Sampel dan kuantil tidak valid.");
        var sorted = values.Order().ToArray(); return sorted[(int)Math.Ceiling(quantile * sorted.Length) - 1];
    }
}
