using System.Globalization;

namespace exs.Commons.Utils
{
    public enum EPeriod
    {
        Always = 0,
        OneHour = 10,
        OneDay = 20
    }

    public static class DateTimeHelper
    {
        public static DateTime EndOfPeriod(this DateTime dt, EPeriod period)
        {
            switch (period)
            {
                case EPeriod.OneHour:
                    return dt.EndOfHour();
                case EPeriod.OneDay:
                    return dt.EndOfDay();
                default:
                    return dt;
            }
        }

        public static DateTime BeginOfPeriod(this DateTime dt, EPeriod period)
        {
            switch (period)
            {
                case EPeriod.OneHour:
                    return dt.BeginOfHour();
                case EPeriod.OneDay:
                    return dt.BeginOfDay();
                default:
                    return dt;
            }
        }

        public static int WeekNumber(this DateTime dt)
        {
            return CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(DateTime.Now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        public static DateTime EndOfMonth(this DateTime dt)
        {
            return EndOfDay(new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month), 0, 0, 0, 0, dt.Kind));
        }

        public static DateTime BeginOfMonth(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, 0, dt.Kind);
        }

        public static DateTime EndOfWeek(this DateTime dt)
        {
            var fd = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            var res = dt.EndOfDay();
            if (res.DayOfWeek == fd)
            {
                res = res.AddDays(1);
            }
            while (res.DayOfWeek != fd)
            {
                res = res.AddDays(1);
            }
            return res.AddDays(-1);
        }

        public static DateTime BeginOfWeek(this DateTime dt)
        {
            var fd = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            var res = dt.BeginOfDay();
            while (res.DayOfWeek != fd)
            {
                res = res.AddDays(-1);
            }
            return res;
        }

        public static DateTime EndOfDay(this DateTime dt)
        {
            return dt.Date.AddDays(1).AddTicks(-1); 
        }

        public static DateTime BeginOfDay(this DateTime dt)
        {
            return dt.Date;
        }

        public static DateTime EndOfHour(this DateTime dt)
        {
            return dt.AddHours(1).BeginOfHour().AddTicks(-1);
        }

        public static DateTime BeginOfHour(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, 0, dt.Kind);
        }

        public static int Quarter(this DateTime dt)
        {
            return (dt.Month + 2) / 3;
        }

        public static DateTime EndOfYear(this DateTime dt)
        {
            return new DateTime(dt.Year, 12, 31, 0, 0, 0, 0, dt.Kind).AddDays(1).AddTicks(-1);
        }

        public static DateTime BeginOfYear(this DateTime dt)
        {
            return new DateTime(dt.Year, 1, 1, 0, 0, 0, 0, dt.Kind);
        }

        public static DateTime EndOfQuarter(this DateTime dt)
        {
            var qt = dt.Quarter() - 1;
            return new DateTime(dt.Year, 1, 1, 0, 0, 0, 0, dt.Kind).AddMonths(4 * qt).AddTicks(-1);
        }

        public static DateTime BeginOfQuarter(this DateTime dt)
        {
            var qt = dt.Quarter() - 1;
            return new DateTime(dt.Year, 1, 1, 0, 0, 0, 0, dt.Kind).AddMonths(3 * qt);
        }

        public static DateTime TrimMillis(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, 0, dt.Kind);
        }

        public static DateTime UnixTimeToUtcDateTime(long unixTime)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
        }

        public static DateTime UnixTimeToLocalDateTime(long unixTime)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime;
        }

        public static long UtcDateTimeToUnixTime(DateTime dateTime)
        {
            dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            return new DateTimeOffset(dateTime, TimeSpan.Zero).ToUnixTimeSeconds();
        }
        public static long OffsetDateTimeToUnixTime(DateTime dateTime, int offset)
        {
            return UnixTimeAddHours(UtcDateTimeToUnixTime(dateTime), offset);
        }
        public static long UnixTimeAddHours(long unixTime, int hours)
        {
            return unixTime + hours * 3600;
        }
        public static long UnixTimeAddMinutes(long unixTime, int mins)
        {
            return unixTime + mins * 60;
        }
        public static DateTime UnixTimeToOffsetDateTime(long unixTime, int offset)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime.AddHours(offset);
        }

        public static ushort CalcDaySection(DateTime dt)
        {
            return (ushort)dt.Subtract(mDaySectionStart).Days;
        }

        //public static string FormatDateForGeoTab(DateTime dateTime)
        //{
        //    return dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff") + "Z";
        //}
        private static DateTime mDaySectionStart = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
