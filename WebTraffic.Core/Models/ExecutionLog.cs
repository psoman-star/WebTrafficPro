using System;

namespace WebTraffic.Core.Models
{
    public class ExecutionLog
    {
        public int Id { get; set; }
        public DateTime ExecutedAt { get; set; }
        public string ProxyIp { get; set; }
        public string TargetUrl { get; set; }
        public string Referer { get; set; }
        public int ElapsedMs { get; set; }
        public int HttpStatusCode { get; set; }
        public bool IsSuccess { get; set; }
        public bool IsInjected { get; set; }
    }
}
