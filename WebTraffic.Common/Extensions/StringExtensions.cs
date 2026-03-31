using System;
using System.Text;

namespace WebTraffic.Common.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// 判断字符串是否为 null 或空白。
        /// </summary>
        public static bool IsNullOrWhiteSpace(this string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// 判断字符串是否为 null 或空。
        /// </summary>
        public static bool IsNullOrEmpty(this string value)
        {
            return string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// 将多行文本（换行符分隔）拆分为非空行数组。
        /// </summary>
        public static string[] ToLines(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return new string[0];

            return value.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// 截断字符串到指定最大长度，超长时追加省略号。
        /// </summary>
        public static string Truncate(this string value, int maxLength, string suffix = "...")
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;

            return value.Substring(0, maxLength) + suffix;
        }

        /// <summary>
        /// 安全地将字符串解析为 int，失败返回默认值。
        /// </summary>
        public static int ToIntOrDefault(this string value, int defaultValue = 0)
        {
            int result;
            return int.TryParse(value, out result) ? result : defaultValue;
        }

        /// <summary>
        /// 安全地将字符串解析为 bool，失败返回默认值。
        /// </summary>
        public static bool ToBoolOrDefault(this string value, bool defaultValue = false)
        {
            bool result;
            return bool.TryParse(value, out result) ? result : defaultValue;
        }

        /// <summary>
        /// 将字符串转换为 Base64 编码（UTF-8）。
        /// </summary>
        public static string ToBase64(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var bytes = Encoding.UTF8.GetBytes(value);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// 从 Base64 解码字符串（UTF-8）。
        /// </summary>
        public static string FromBase64(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var bytes = Convert.FromBase64String(value);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// 将代理字符串（ip:port 或 ip:port:user:pass 或 socks5://ip:port）
        /// 解析为各部分元组，解析失败返回 null。
        /// </summary>
        public static ProxyParts ParseProxyString(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            value = value.Trim();

            // socks5://ip:port
            if (value.StartsWith("socks5://", StringComparison.OrdinalIgnoreCase))
            {
                var hostPort = value.Substring("socks5://".Length);
                var parts = hostPort.Split(':');
                if (parts.Length == 2)
                {
                    int port;
                    if (int.TryParse(parts[1], out port))
                        return new ProxyParts { Host = parts[0], Port = port, Scheme = "socks5" };
                }
                return null;
            }

            // ip:port or ip:port:user:pass
            var segments = value.Split(':');
            if (segments.Length == 2)
            {
                int port;
                if (int.TryParse(segments[1], out port))
                    return new ProxyParts { Host = segments[0], Port = port, Scheme = "http" };
            }
            else if (segments.Length == 4)
            {
                int port;
                if (int.TryParse(segments[1], out port))
                    return new ProxyParts
                    {
                        Host = segments[0],
                        Port = port,
                        Username = segments[2],
                        Password = segments[3],
                        Scheme = "http"
                    };
            }

            return null;
        }
    }

    /// <summary>
    /// 代理字符串解析结果。
    /// </summary>
    public class ProxyParts
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        /// <summary>http / socks5</summary>
        public string Scheme { get; set; }
    }
}
