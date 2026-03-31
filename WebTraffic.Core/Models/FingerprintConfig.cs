namespace WebTraffic.Core.Models
{
    public class FingerprintConfig
    {
        // 浏览器标识
        public bool HideWebDriver { get; set; }
        public bool InjectPluginList { get; set; }
        public bool DisableAutomationBar { get; set; }
        public bool DisableWebRTC { get; set; }

        // 图形 & 环境
        public bool CanvasNoise { get; set; }
        public bool WebGLSpoof { get; set; }
        public bool RandomResolution { get; set; }
        public bool RandomTimezone { get; set; }
        public bool RandomLanguage { get; set; }

        // UA 轮换
        public bool RandomUA { get; set; }
        public bool RotatePerSession { get; set; }
        public bool UseSelectedOnly { get; set; }
    }
}
