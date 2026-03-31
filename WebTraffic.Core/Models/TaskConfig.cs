namespace WebTraffic.Core.Models
{
    public class TaskConfig
    {
        public int    Id        { get; set; }
        public string Name      { get; set; }  // 任务配置名称
        public string TargetUrl { get; set; }  // 换行符分隔的Url列表
        public string Keywords { get; set; }      // 换行符分隔的关键词列表
        public int VisitCount { get; set; }
        public int ThreadCount { get; set; }
        public int MinDelay { get; set; }         // 秒
        public int MaxDelay { get; set; }         // 秒
        public string SourceWeights { get; set; } // JSON 序列化的来源权重
    }
}
