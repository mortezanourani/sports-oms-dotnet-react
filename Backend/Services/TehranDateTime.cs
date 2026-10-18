namespace Backend.Services
{
    public static class TehranDateTime
    {
        private static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

        public static DateTime ToUtc(DateOnly date)
        {
            var localMidnight = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(localMidnight, TimeZone);
        }

        public static DateTime NormalizeUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        public static DateTime TodayUtc
        {
            get
            {
                var tehranNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);
                return ToUtc(DateOnly.FromDateTime(tehranNow));
            }
        }
    }
}
