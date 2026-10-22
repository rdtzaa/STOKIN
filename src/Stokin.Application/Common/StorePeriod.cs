namespace Stokin.Application.Common;

public static class StorePeriod
{
    public static DateTimeOffset StartUtc(string date, string timeZone)
    {
        var local = DateTime.SpecifyKind(DateTime.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Unspecified); var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) throw new ArgumentException("Batas waktu tidak unik pada zona toko. Pilih tanggal lain."); return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }
}
