using System;
using WebTraffic.Common.Enums;

namespace WebTraffic.Logging
{
    /// <summary>
    /// UI 日志面板订阅 <see cref="MemoryRingBuffer.NewLogEntry"/> 事件时收到的数据载体。
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public AppLogLevel Level { get; set; }
        public string Message { get; set; }
        public string ExceptionText { get; set; }

        /// <summary>格式化为日志行，例如 [2026-03-16 14:30:00] [INFO] 消息内容</summary>
        public override string ToString()
        {
            var level = Level.ToString().ToUpperInvariant().PadRight(5);
            var ts    = Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            if (!string.IsNullOrEmpty(ExceptionText))
                return string.Format("[{0}] [{1}] {2}\r\n{3}", ts, level, Message, ExceptionText);
            return string.Format("[{0}] [{1}] {2}", ts, level, Message);
        }
    }
}
