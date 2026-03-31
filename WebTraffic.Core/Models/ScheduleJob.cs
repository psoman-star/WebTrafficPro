using System;

namespace WebTraffic.Core.Models
{
    public class ScheduleJob
    {
        public int       Id             { get; set; }
        public string    Name           { get; set; }
        public string    CronExpression { get; set; }
        public DateTime? LastRunAt      { get; set; }
        public bool      IsEnabled      { get; set; }
        public int       TaskConfigId   { get; set; }
        public int       VisitCount     { get; set; }  // 本次执行访问次数
        public int       ThreadCount    { get; set; }  // 本次并发线程数
        public int       MinDelay       { get; set; }  // 最小延迟（秒）
        public int       MaxDelay       { get; set; }  // 最大延迟（秒）
    }
}
