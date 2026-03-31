using WebTraffic.Common.Enums;

namespace WebTraffic.Core.Models
{
    public class ProxyInfo
    {
        public int Id { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public ProxyType Type { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int LatencyMs { get; set; }
        public ProxyStatus Status { get; set; }
    }
}
