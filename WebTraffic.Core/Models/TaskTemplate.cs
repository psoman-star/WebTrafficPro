using System;

namespace WebTraffic.Core.Models
{
    public class TaskTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ConfigJson { get; set; }    // TaskConfig 序列化后的 JSON
        public DateTime CreatedAt { get; set; }
    }
}
