namespace EquipmentBorrowing.Application.Services;

public static class TimeProviderExtensions
{
    // One definition of "today" (local calendar date) for the whole app.
    public static DateOnly GetLocalToday(this TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
}