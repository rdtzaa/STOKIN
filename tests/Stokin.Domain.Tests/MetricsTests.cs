using Stokin.Testing;
using Stokin.Domain;
namespace Stokin.Domain.Tests;

public class MetricsTests
{
    [Test] public void P95_uses_nearest_rank() => Assert.Equal(95d, Metrics.Percentile(Enumerable.Range(1, 100).Select(x => (double)x).ToArray(), 0.95));
    [Test] public void Empty_measurement_is_not_reported_as_zero() => Assert.Throws<ArgumentException>(() => Metrics.Percentile([], 0.95));
}
