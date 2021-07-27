using System;

namespace XFramework
{
    public partial class Utility
    {
        public static class Time
        {
            public static readonly DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            public static long GetCurrentTimeStamp()
            {
                return Convert.ToInt64(GetCurrentTime());
            }

            public static double GetCurrentTime()
            {
                TimeSpan ts = DateTime.UtcNow - dateTime;
                return ts.TotalMilliseconds;
            }
        }
    }
}
