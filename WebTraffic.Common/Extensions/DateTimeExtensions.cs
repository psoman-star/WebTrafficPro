using System;

namespace WebTraffic.Common.Extensions
{
    public static class DateTimeExtensions
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// 转换为 Unix 时间戳（秒）。
        /// </summary>
        public static long ToUnixTimeSeconds(this DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - UnixEpoch).TotalSeconds;
        }

        /// <summary>
        /// 转换为 Unix 时间戳（毫秒）。
        /// </summary>
        public static long ToUnixTimeMilliseconds(this DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - UnixEpoch).TotalMilliseconds;
        }

        /// <summary>
        /// 从 Unix 时间戳（秒）还原为本地时间。
        /// </summary>
        public static DateTime FromUnixTimeSeconds(long seconds)
        {
            return UnixEpoch.AddSeconds(seconds).ToLocalTime();
        }

        /// <summary>
        /// 格式化为日志友好字符串：yyyy-MM-dd HH:mm:ss
        /// </summary>
        public static string ToLogString(this DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// 格式化为文件名友好字符串：yyyy-MM-dd
        /// </summary>
        public static string ToDateString(this DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// 格式化为带毫秒的完整字符串：yyyy-MM-dd HH:mm:ss.fff
        /// </summary>
        public static string ToFullString(this DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        /// <summary>
        /// 返回当天的起始时刻（00:00:00.000）。
        /// </summary>
        public static DateTime StartOfDay(this DateTime dateTime)
        {
            return dateTime.Date;
        }

        /// <summary>
        /// 返回当天的结束时刻（23:59:59.999）。
        /// </summary>
        public static DateTime EndOfDay(this DateTime dateTime)
        {
            return dateTime.Date.AddDays(1).AddMilliseconds(-1);
        }

        /// <summary>
        /// 判断是否在指定的时间范围内（含边界）。
        /// </summary>
        public static bool IsBetween(this DateTime dateTime, DateTime start, DateTime end)
        {
            return dateTime >= start && dateTime <= end;
        }

        /// <summary>
        /// 将毫秒数格式化为可读耗时字符串，例如 "1h 23m 45s" 或 "45.123s"。
        /// </summary>
        public static string FormatElapsed(int milliseconds)
        {
            if (milliseconds < 0)
                return "0ms";

            var ts = TimeSpan.FromMilliseconds(milliseconds);

            if (ts.TotalHours >= 1)
                return string.Format("{0}h {1}m {2}s", (int)ts.TotalHours, ts.Minutes, ts.Seconds);

            if (ts.TotalMinutes >= 1)
                return string.Format("{0}m {1}s", ts.Minutes, ts.Seconds);

            if (ts.TotalSeconds >= 1)
                return string.Format("{0:F3}s", ts.TotalSeconds);

            return string.Format("{0}ms", milliseconds);
        }

        /// <summary>
        /// 计算与当前时间的友好相对描述，例如 "3 分钟前"。
        /// </summary>
        public static string ToRelativeString(this DateTime dateTime)
        {
            var diff = DateTime.Now - dateTime;

            if (diff.TotalSeconds < 60)
                return "刚刚";
            if (diff.TotalMinutes < 60)
                return string.Format("{0} 分钟前", (int)diff.TotalMinutes);
            if (diff.TotalHours < 24)
                return string.Format("{0} 小时前", (int)diff.TotalHours);
            if (diff.TotalDays < 30)
                return string.Format("{0} 天前", (int)diff.TotalDays);

            return dateTime.ToDateString();
        }
    }
}
