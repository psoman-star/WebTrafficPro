using System;
using System.IO;
using System.Data.SQLite;

namespace WebTraffic.Data.Repositories
{
    /// <summary>
    /// 读写 app_settings 键值表，同时提供数据库连接字符串解析。
    /// </summary>
    public class AppSettingsRepository : BaseRepository
    {
        private static readonly string DefaultDbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WebTrafficPro",
            "data.db");

        /// <summary>
        /// 使用默认数据库路径构造。
        /// </summary>
        public AppSettingsRepository()
            : this(BuildConnectionString(DefaultDbPath))
        {
        }

        /// <summary>
        /// 指定连接字符串构造，供测试或自定义路径使用。
        /// </summary>
        public AppSettingsRepository(string connectionString)
            : base(connectionString)
        {
        }

        // ── 公开方法 ────────────────────────────────────────────────────

        /// <summary>
        /// 返回当前使用的数据库连接字符串（供其他 Repository 使用）。
        /// </summary>
        public string ConnectionString { get { return _connectionString; } }

        /// <summary>
        /// 获取指定 key 的值，key 不存在时返回 <paramref name="defaultValue"/>。
        /// </summary>
        public string Get(string key, string defaultValue = "")
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT value FROM app_settings WHERE key = @k LIMIT 1;";
                cmd.Parameters.AddWithValue("@k", key);
                var result = cmd.ExecuteScalar();
                return result != null ? result.ToString() : defaultValue;
            }
        }

        /// <summary>
        /// 设置（INSERT OR REPLACE）指定 key 的值。
        /// </summary>
        public void Set(string key, string value)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT OR REPLACE INTO app_settings(key, value) VALUES (@k, @v);";
                cmd.Parameters.AddWithValue("@k", key);
                cmd.Parameters.AddWithValue("@v", value ?? string.Empty);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 获取布尔值，无法解析时返回 <paramref name="defaultValue"/>。
        /// </summary>
        public bool GetBool(string key, bool defaultValue = false)
        {
            var raw = Get(key);
            bool result;
            return bool.TryParse(raw, out result) ? result : defaultValue;
        }

        /// <summary>
        /// 获取整数值，无法解析时返回 <paramref name="defaultValue"/>。
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            var raw = Get(key);
            int result;
            return int.TryParse(raw, out result) ? result : defaultValue;
        }

        // ── 静态工厂 ────────────────────────────────────────────────────

        /// <summary>
        /// 从可选的 config.json 或默认路径构建数据库连接字符串。
        /// </summary>
        public static string BuildConnectionString(string dbPath)
        {
            return new SQLiteConnectionStringBuilder
            {
                DataSource = dbPath,
                Version    = 3,
            }.ToString();
        }
    }
}
