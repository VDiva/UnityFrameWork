using System;

namespace FrameWork.Script.Tool
{
    public static class TimeTool
    {
        public static string GetTime(this long time)
        {
            long timestamp = time;
            DateTime baseTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime utcDateTime = baseTime.AddSeconds(timestamp); // 如果是毫秒则用 AddMilliseconds
            DateTime localDateTime = utcDateTime.ToLocalTime();
            return localDateTime.ToString("yyyy-MM-dd HH:mm");
        }
        
        public static double GetDaysBetween(this long startTimestamp, long endTimestamp)
        {
            // 1. 将两个时间戳都转为 DateTimeOffset
            DateTimeOffset startTime = ConvertTimestampToDateTimeOffset(startTimestamp);
            DateTimeOffset endTime = ConvertTimestampToDateTimeOffset(endTimestamp);

            // 2. 计算时间差并返回总天数
            TimeSpan diff = endTime - startTime;
            return diff.TotalDays;
        }
        
        private static DateTimeOffset ConvertTimestampToDateTimeOffset(long timestamp)
        {
            // 大于 9999999999 视为毫秒级（13位），否则为秒级（10位）
            if (timestamp > 9999999999)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
            }
            else
            {
                return DateTimeOffset.FromUnixTimeSeconds(timestamp);
            }
        }

        public static long GetDatUnixTime(int day)
        {
            return day * 3600 * 24;
        }

        public static string GetTimeAsLong(this long date)
        {
            var m = (int)(date / 60f);
            var s = (int)(date % 60f);
            var mSrt=m>=10?m+"":"0"+m;
            var sSrt = s >= 10 ? s + "" : "0" + s;
            return $"{mSrt}:{sSrt}";
        }
        
        public static string GetTimeAsFloat(this float date)
        {
            var m = (int)(date / 60f);
            var s = (int)(date % 60f);
            var mSrt=m>=10?m+"":"0"+m;
            var sSrt = s >= 10 ? s + "" : "0" + s;
            return $"{mSrt}:{sSrt}";
        }


        public static string GetTo24Time()
        {
            var now = DateTime.Now;
            var to24 = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59, 999);
            return (to24 - now).ToString(@"hh\:mm\:ss");
        }

        public static string GetDaySrt()
        {
           return DateTime.Now.ToString("yyyyMMdd");
        }
    }
}