using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebTraffic.Core.Models;

namespace WebTraffic.Core.Interfaces
{
    /// <summary>
    /// 定时任务 CRUD 及触发管理。
    /// </summary>
    public interface IScheduleService
    {
        /// <summary>当定时任务被触发时触发，携带对应的 ScheduleJob。</summary>
        event EventHandler<ScheduleJob> JobTriggered;

        /// <summary>
        /// 启动调度器，开始监听所有已启用的定时任务。
        /// </summary>
        void Start();

        /// <summary>
        /// 停止调度器。
        /// </summary>
        void Stop();

        /// <summary>
        /// 获取所有定时任务。
        /// </summary>
        Task<List<ScheduleJob>> GetAllAsync();

        /// <summary>
        /// 新增定时任务，返回新记录 Id。
        /// </summary>
        Task<int> AddAsync(ScheduleJob job);

        /// <summary>
        /// 更新定时任务（包括启用/禁用）。
        /// </summary>
        Task UpdateAsync(ScheduleJob job);

        /// <summary>
        /// 删除定时任务。
        /// </summary>
        Task DeleteAsync(int jobId);

        /// <summary>
        /// 启用或禁用指定任务。
        /// </summary>
        Task SetEnabledAsync(int jobId, bool isEnabled);

        /// <summary>
        /// 手动立即触发指定任务（忽略 Cron 计划）。
        /// </summary>
        Task TriggerNowAsync(int jobId);
    }
}
