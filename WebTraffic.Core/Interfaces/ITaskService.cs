using System;
using System.Threading;
using System.Threading.Tasks;
using WebTraffic.Common.Enums;
using WebTraffic.Core.Models;

namespace WebTraffic.Core.Interfaces
{
    /// <summary>
    /// 任务启动、暂停、停止、状态查询。
    /// </summary>
    public interface ITaskService
    {
        /// <summary>当单条请求执行完成时触发，携带执行日志。</summary>
        event EventHandler<ExecutionLog> RequestCompleted;

        /// <summary>当任务状态变更时触发。</summary>
        event EventHandler<JobStatus> StatusChanged;

        /// <summary>当任务发生异常时触发，携带错误消息。</summary>
        event EventHandler<string> TaskError;

        /// <summary>当前任务状态。</summary>
        JobStatus CurrentStatus { get; }

        /// <summary>启动任务。</summary>
        Task StartAsync(TaskConfig config, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>暂停正在运行的任务。</summary>
        void Pause();

        /// <summary>恢复已暂停的任务。</summary>
        void Resume();

        /// <summary>停止任务（不可恢复）。</summary>
        void Stop();

        /// <summary>获取当前实时统计快照。</summary>
        TaskStats GetStats();
    }

    /// <summary>
    /// 实时任务统计快照。
    /// </summary>
    public class TaskStats
    {
        public int TotalRequests { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double RequestsPerSecond { get; set; }
        public TimeSpan Elapsed { get; set; }
    }
}
