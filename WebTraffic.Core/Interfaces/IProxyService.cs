using System.Collections.Generic;
using System.Threading.Tasks;
using WebTraffic.Core.Models;

namespace WebTraffic.Core.Interfaces
{
    /// <summary>
    /// 代理导入、验证、获取、统计。
    /// </summary>
    public interface IProxyService
    {
        /// <summary>
        /// 批量导入代理字符串（支持 ip:port / ip:port:user:pass / socks5://ip:port）。
        /// 返回成功导入的数量。
        /// </summary>
        Task<int> ImportAsync(IEnumerable<string> proxyLines);

        /// <summary>
        /// 验证单个代理的可用性，并更新其状态与延迟。
        /// </summary>
        Task<bool> ValidateAsync(ProxyInfo proxy);

        /// <summary>
        /// 批量验证所有 Unchecked 或 Active 代理。
        /// </summary>
        Task ValidateAllAsync();

        /// <summary>
        /// 获取下一个可用代理（内部执行轮换策略）。
        /// 无可用代理时返回 null。
        /// </summary>
        ProxyInfo GetNext();

        /// <summary>
        /// 将指定代理标记为不可用。
        /// </summary>
        Task MarkInactiveAsync(int proxyId);

        /// <summary>
        /// 获取所有代理列表（分页）。
        /// </summary>
        Task<List<ProxyInfo>> GetAllAsync(int page, int pageSize);

        /// <summary>
        /// 删除指定代理。
        /// </summary>
        Task DeleteAsync(int proxyId);

        /// <summary>
        /// 清空所有代理。
        /// </summary>
        Task ClearAllAsync();

        /// <summary>
        /// 获取代理统计汇总。
        /// </summary>
        Task<ProxyStats> GetStatsAsync();

        /// <summary>
        /// 重置并重新加载内存代理池（清空失效记录后从数据库读取 Active 代理）。
        /// </summary>
        Task ReloadPoolAsync();
    }

    /// <summary>
    /// 代理池统计汇总。
    /// </summary>
    public class ProxyStats
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Inactive { get; set; }
        public int Unchecked { get; set; }
        public double AverageLatencyMs { get; set; }
    }
}
