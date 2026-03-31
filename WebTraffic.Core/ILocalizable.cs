namespace WebTraffic.Core
{
    /// <summary>
    /// 实现此接口的窗体/控件将在语言切换时自动刷新所有 UI 文本。
    /// </summary>
    public interface ILocalizable
    {
        /// <summary>将所有 UI 控件的 Text 属性更新为当前语言。</summary>
        void ApplyLocalization();
    }
}
