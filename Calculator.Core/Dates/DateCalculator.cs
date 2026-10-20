using System.Globalization;
using System.Text.RegularExpressions;

namespace Calculator.Core;

/// <summary>A proleptic Gregorian date with the same year range and clamping rules as LocalDate.</summary>
public readonly partial record struct CalendarDate
{
    public int Year { get; }
    public int Month { get; }
    public int Day { get; }
    public CalendarDate(int year, int month, int day)
    {
        if (year is < -999999999 or > 999999999 || month is < 1 or > 12 || day < 1 || day > DaysInMonth(year, month))
            throw new ArgumentOutOfRangeException(nameof(day), "Date is outside the supported range");
        Year = year; Month = month; Day = day;
    }
    public static CalendarDate Today => FromDateTime(DateTime.Today);
    public static CalendarDate FromDateTime(DateTime date) => new(date.Year, date.Month, date.Day);
    public DateTime ToDateTime() => new(Year, Month, Day);
    public static bool IsLeapYear(int year) => year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
    public static int DaysInMonth(int year, int month) => month switch
    {
        2 => IsLeapYear(year) ? 29 : 28,
        4 or 6 or 9 or 11 => 30,
        >= 1 and <= 12 => 31,
        _ => throw new ArgumentOutOfRangeException(nameof(month))
    };

    [GeneratedRegex(@"\A([+-]?[0-9]{4,9})-([0-9]{2})-([0-9]{2})\z", RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();
    public static CalendarDate Parse(string text)
    {
        var match = DatePattern().Match(text.Trim());
        if (!match.Success) throw new FormatException("Use an ISO date such as 2026-10-19");
        return new(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
    }
    public override string ToString()
    {
        string year = Math.Abs(Year).ToString("D4", CultureInfo.InvariantCulture);
        return (Year < 0 ? "-" : Year > 9999 ? "+" : "") + year + "-" +
            Month.ToString("D2", CultureInfo.InvariantCulture) + "-" + Day.ToString("D2", CultureInfo.InvariantCulture);
    }

    public long ToEpochDay()
    {
        long year = Year - (Month <= 2 ? 1L : 0L);
        long era = FloorDivide(year, 400);
        long yearOfEra = year - era * 400;
        long dayOfYear = (153L * (Month + (Month > 2 ? -3 : 9)) + 2) / 5 + Day - 1;
        return era * 146097 + yearOfEra * 365 + yearOfEra / 4 - yearOfEra / 100 + dayOfYear - 719468;
    }

    public static CalendarDate FromEpochDay(long epochDay)
    {
        long z = checked(epochDay + 719468);
        long era = FloorDivide(z, 146097);
        long dayOfEra = z - era * 146097;
        long yearOfEra = (dayOfEra - dayOfEra / 1460 + dayOfEra / 36524 - dayOfEra / 146096) / 365;
        long year = yearOfEra + era * 400;
        long dayOfYear = dayOfEra - (365 * yearOfEra + yearOfEra / 4 - yearOfEra / 100);
        long monthPrime = (5 * dayOfYear + 2) / 153;
        int day = (int)(dayOfYear - (153 * monthPrime + 2) / 5 + 1);
        int month = (int)(monthPrime + (monthPrime < 10 ? 3 : -9));
        year += month <= 2 ? 1 : 0;
        if (year is < -999999999 or > 999999999) throw new ArgumentOutOfRangeException(nameof(epochDay));
        return new((int)year, month, day);
    }

    public CalendarDate PlusYears(long years)
    {
        long year = checked(Year + years);
        if (year is < -999999999 or > 999999999) throw new ArgumentOutOfRangeException(nameof(years));
        return new((int)year, Month, Math.Min(Day, DaysInMonth((int)year, Month)));
    }
    public CalendarDate PlusMonths(long months)
    {
        long monthCount = checked(Year * 12L + Month - 1 + months);
        long year = FloorDivide(monthCount, 12);
        if (year is < -999999999 or > 999999999) throw new ArgumentOutOfRangeException(nameof(months));
        int month = (int)(monthCount - year * 12 + 1);
        return new((int)year, month, Math.Min(Day, DaysInMonth((int)year, month)));
    }
    public CalendarDate PlusDays(long days) => FromEpochDay(checked(ToEpochDay() + days));
    private static long FloorDivide(long value, long divisor) => value / divisor - (value % divisor < 0 ? 1 : 0);
}

public sealed class DateCalculator
{
    public sealed record Difference(long Days, long Weeks, long RemainingDays);
    public Difference Between(CalendarDate from, CalendarDate to)
    {
        long days = to.ToEpochDay() - from.ToEpochDay();
        return new(days, days / 7, days % 7);
    }
    public CalendarDate Adjust(CalendarDate date, int years, int months, int days, bool subtract = false)
    {
        if (years is < 0 or > 9999 || months is < 0 or > 1200 || days is < 0 or > 100000)
            throw new ArgumentOutOfRangeException(nameof(years), "Date adjustment is outside the supported range");
        long sign = subtract ? -1 : 1;
        return date.PlusYears(sign * years).PlusMonths(sign * months).PlusDays(sign * days);
    }
}
