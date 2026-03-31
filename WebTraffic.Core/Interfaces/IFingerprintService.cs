using System.Collections.Generic;
using System.Threading.Tasks;
using WebTraffic.Common.Enums;
using WebTraffic.Core.Models;

namespace WebTraffic.Core.Interfaces
{
    /// <summary>
    /// UA 获取、随机指纹生成。
    /// </summary>
    public interface IFingerprintService
    {
        /// <summary>
        /// 根据当前配置获取一个 UA 字符串（遵循 RandomUA / UseSelectedOnly 规则）。
        /// </summary>
        string GetNextUserAgent();

        /// <summary>
        /// 根据当前配置获取一个 <see cref="UAProfile"/>（遵循 RandomUA / UseSelectedOnly 规则）。
        /// 无可用 Profile 时返回 null。
        /// </summary>
        UAProfile GetNextUAProfile();

        /// <summary>
        /// 获取当前生效的指纹配置。
        /// </summary>
        FingerprintConfig GetConfig();

        /// <summary>
        /// 保存指纹配置到持久化存储。
        /// </summary>
        Task SaveConfigAsync(FingerprintConfig config);

        /// <summary>
        /// 获取所有 UA，可按设备类型和浏览器类型筛选（null 表示不过滤）。
        /// </summary>
        Task<List<UAProfile>> GetUAProfilesAsync(DeviceType? deviceType = null, string browserType = null);

        /// <summary>
        /// 导入自定义 UA 字符串列表，返回成功导入的数量。
        /// </summary>
        Task<int> ImportUAAsync(IEnumerable<string> userAgents);

        /// <summary>
        /// 设置指定 UA 的选中状态。
        /// </summary>
        Task SetUASelectedAsync(int uaId, bool isSelected);


        Task SetUASelectedBatchAsync(IEnumerable<(int id, bool isSelected)> items);

        /// <summary>
        /// 批量设置所有 UA 的选中状态。
        /// </summary>
        Task SetAllUASelectedAsync(bool isSelected);

        /// <summary>
        /// 重置会话 UA（任务停止时调用），下次 <see cref="GetNextUserAgent"/> 将重新随机选取。
        /// </summary>
        void ResetSession();
    }
}
