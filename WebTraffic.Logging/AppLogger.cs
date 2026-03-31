using System;
using NLog;
using WebTraffic.Core.Interfaces;

namespace WebTraffic.Logging
{
    /// <summary>
    /// <see cref="IAppLogger"/> 的 NLog 实现。
    /// 通过 <see cref="LoggerFactory.Create"/> 获取实例，不要直接 new。
    /// </summary>
    public sealed class AppLogger : IAppLogger
    {
        private readonly Logger _logger;

        internal AppLogger(Logger logger)
        {
            _logger = logger ?? throw new ArgumentNullException("logger");
        }

        public void Info(string message)
        {
            _logger.Info(message);
        }

        public void Warn(string message)
        {
            _logger.Warn(message);
        }

        public void Error(string message, Exception ex = null)
        {
            if (ex != null)
                _logger.Error(ex, message);
            else
                _logger.Error(message);
        }
    }
}
