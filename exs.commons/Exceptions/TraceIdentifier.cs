using exs.Commons.Utils;

namespace exs.Helpers
{
    public static class TraceIdentifier
    {
        public static string Create()
        {
            var curDate = DateTimeOffset.UtcNow;
            return THashKeyHelper.BuildHashKey(curDate.ToUnixTimeMilliseconds()) + curDate.Day.ToString("00") + curDate.Month.ToString("00");
        }
    }
}
