using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebTraffic.Core.Models;

namespace WebTraffic.Core.Interfaces
{
    /// <summary>
    /// 统计数据查询与汇总。
    /// </summary>
    public interface IStatsService
    {
        /// <summary>
        /// 获取全局汇总统计。
        /// </summary>
        Task<GlobalStats> GetGlobalStatsAsync();

        /// <summary>
        /// 获取指定时间范围内的请求量趋势（按小时或按天聚合）。
        /// </summary>
        Task<List<TrendPoint>> GetRequestTrendAsync(DateTime from, DateTime to, TrendGranularity granularity);

        /// <summary>
        /// 获取来源分布（Referer 分类占比）。
        /// </summary>
        Task<List<SourceDistribution>> GetSourceDistributionAsync(DateTime from, DateTime to);

        /// <summary>
        /// 分页查询执行日志。
        /// </summary>
        Task<PagedResult<ExecutionLog>> GetExecutionLogsAsync(int page, int pageSize, DateTime? from = null, DateTime? to = null);
    }

    public class GlobalStats
    {
        public long TotalRequests { get; set; }
        public double SuccessRate { get; set; }       // 0.0 ~ 1.0
        public int ProxiesConsumed { get; set; }
        public TimeSpan TotalRuntime { get; set; }
    }

    public class TrendPoint
    {
        public DateTime Time { get; set; }
        public int RequestCount { get; set; }
        public int SuccessCount { get; set; }
    }

    public class SourceDistribution
    {
        public string Source { get; set; }            // 来源名称，如 "Google"
        public int Count { get; set; }
        public double Percentage { get; set; }        // 0.0 ~ 100.0
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        public int TotalPages
        {
            get { return PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0; }
        }
    }

    public enum TrendGranularity
    {
        Hourly,
        Daily
    }
}
