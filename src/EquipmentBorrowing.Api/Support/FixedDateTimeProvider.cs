namespace EquipmentBorrowing.Api.Support;

/// <summary>
/// A frozen clock for acceptance tests only. Your services already take a TimeProvider
/// (GetLocalToday), so nothing in Application changes.
/// </summary>
public sealed class FixedDateTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedDateTimeProvider(DateOnly date)
    {
        _now = new DateTimeOffset(date.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
    }

    public override DateTimeOffset GetUtcNow() => _now;

    // UTC as "local" so GetLocalNow() returns exactly the fixed date.
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
