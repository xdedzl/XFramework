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


            /// <summary>
            /// 执行一个方法并返回它的执行时间
            /// </summary>
            /// <param name="action"></param>
            /// <returns>执行时间</returns>
            public static long GetActionRunTime(Action action)
            {
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                action();
                sw.Stop();
                return sw.ElapsedMilliseconds;
            }
        }
    }
}
