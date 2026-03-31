using WebTraffic.Common.Enums;

namespace WebTraffic.Core.Models
{
    public class UAProfile
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string UserAgent { get; set; }
        public DeviceType DeviceType { get; set; }
        public string BrowserType { get; set; }   // Chrome / Firefox / Edge / Safari
        public bool IsSelected { get; set; }
    }
}
