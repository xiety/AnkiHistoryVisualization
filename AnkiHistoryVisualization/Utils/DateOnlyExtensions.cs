namespace System;

public static class DateOnlyExtensions
{
    public static IEnumerable<DateOnly> EnumerateToInclusive(this DateOnly from, DateOnly to)
    {
        var currentDate = from;

        while (currentDate <= to)
        {
            yield return currentDate;
            currentDate = currentDate.AddDays(1);
        }
    }
}
