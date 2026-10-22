using Stokin.Testing;
using Stokin.Application.Common;
namespace Stokin.Domain.Tests;

public class StorePeriodTests
{
    [Test] public void Makassar_calendar_day_starts_at_16_UTC_previous_day() => Assert.Equal(DateTimeOffset.Parse("2026-10-21T16:00:00Z"), StorePeriod.StartUtc("2026-10-22", "Asia/Makassar"));
    [Test] public void Jakarta_calendar_day_starts_at_17_UTC_previous_day() => Assert.Equal(DateTimeOffset.Parse("2026-10-21T17:00:00Z"), StorePeriod.StartUtc("2026-10-22", "Asia/Jakarta"));
}
