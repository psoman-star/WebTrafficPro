using System;

namespace WebTraffic.Core.Models
{
    public class CookieStore
    {
        public int Id { get; set; }
        public int ProfileId { get; set; }
        public string Domain { get; set; }
        public string CookieJson { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
