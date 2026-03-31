using NLog;
using NLog.Targets;
using WebTraffic.Common.Enums;
using NLogLevel = NLog.LogLevel;

namespace WebTraffic.Logging
{
    /// <summary>
    /// 自定义 NLog Target，将日志条目写入 <see cref="MemoryRingBuffer.Instance"/>。
    /// NLog 管道写完文件后同步写入此 Target，UI 面板通过事件感知新条目。
    /// </summary>
    [Target("MemoryRingBuffer")]
    public sealed class MemoryRingBufferTarget : TargetWithLayout
    {
        protected override void Write(LogEventInfo logEvent)
        {
            var level   = MapLevel(logEvent.Level);
            var message = RenderLogEvent(Layout, logEvent);

            MemoryRingBuffer.Instance.Write(level, message, logEvent.Exception);
        }

        private static AppLogLevel MapLevel(NLogLevel nlogLevel)
        {
            if (nlogLevel >= NLogLevel.Error) return AppLogLevel.Error;
            if (nlogLevel >= NLogLevel.Warn)  return AppLogLevel.Warn;
            return AppLogLevel.Info;
        }
    }
}
