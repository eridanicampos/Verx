namespace CashFlow.Consolidation.Domain.Utils;

public static class BusinessCalendar
{
    public static DateOnly DateOf(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);
}
