using System;

namespace WebTraffic.Core.Interfaces
{
    /// <summary>
    /// 日志抽象接口，Engine / Data 层通过此接口写日志，不直接依赖 NLog。
    /// </summary>
    public interface IAppLogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception ex = null);
    }
}
