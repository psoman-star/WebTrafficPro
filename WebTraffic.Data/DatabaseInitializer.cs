using System;
using System.Collections.Generic;
using System.IO;
using System.Data.SQLite;
using WebTraffic.Common.Enums;
using WebTraffic.Core.Models;

namespace WebTraffic.Data
{
    /// <summary>
    /// 负责数据库文件创建和全部表的初始化（幂等，可重复执行）。
    /// </summary>
    public class DatabaseInitializer
    {
        private readonly string _connectionString;

        public DatabaseInitializer(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException("connectionString");

            _connectionString = connectionString;
        }

        /// <summary>
        /// 确保数据库目录存在，并执行所有 CREATE TABLE IF NOT EXISTS 脚本。
        /// </summary>
        public void Initialize()
        {
            EnsureDirectoryExists();

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    CreateTaskConfigTable(conn, tx);
                    CreateTaskTemplateTable(conn, tx);
                    CreateScheduleJobTable(conn, tx);
                    CreateProxyListTable(conn, tx);
                    CreateExecutionLogTable(conn, tx);
                    CreateUAProfileTable(conn, tx);
                    CreateAppSettingsTable(conn, tx);
                    CreateCookieStoreTable(conn, tx);
                    tx.Commit();
                }

                SeedUAProfiles(conn);
                RunMigrations(conn);

                // 开启 WAL 模式提升并发写入性能
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA journal_mode=WAL;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "PRAGMA foreign_keys=ON;";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── 私有方法 ────────────────────────────────────────────────────

        private void EnsureDirectoryExists()
        {
            var builder = new SQLiteConnectionStringBuilder(_connectionString);
            var dbPath = builder.DataSource;
            if (!string.IsNullOrEmpty(dbPath) && dbPath != ":memory:")
            {
                var dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
        }

        private static void Exec(SQLiteConnection conn, SQLiteTransaction tx, string sql)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private static void CreateTaskConfigTable(SQLiteConnection conn, SQLiteTransaction tx)
        {
            Exec(conn, tx, @"
CREATE TABLE IF NOT EXISTS task_config (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    target_url      TEXT    NOT NULL,
    keywords        TEXT    NOT NULL DEFAULT '',
    visit_count     INTEGER NOT NULL DEFAULT 1,
    thread_count    INTEGER NOT NULL DEFAULT 1,
    min_delay       INTEGER NOT NULL DEFAULT 1,
    max_delay       INTEGER NOT NULL DEFAULT 5,
    source_weights  TEXT    NOT NULL DEFAULT '{}'
);");
        }

        private static void CreateTaskTemplateTable(SQLiteConnection conn, SQLiteTransaction tx)
        {
            Exec(conn, tx, @"
CREATE TABLE IF NOT EXISTS task_template (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    name        TEXT    NOT NULL,
    config_json TEXT    NOT NULL DEFAULT '{}',
    created_at  TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);");
        }

        private static void CreateScheduleJobTable(SQLiteConnection conn, SQLiteTransaction tx)
        {
            Exec(conn, tx, @"
CREATE TABLE IF NOT EXISTS schedule_job (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    name             TEXT    NOT NULL,
    cron_expression  TEXT    NOT NULL,
    last_run_at      TEXT,
    is_enabled       INTEGER NOT NULL DEFAULT 1,
    task_config_id   INTEGER NOT NULL,
    FOREIGN KEY (task_config_id) REFERENCES task_config(id) ON DELETE CASCADE
);");
        }

        private static void CreateProxyListTable(SQLiteConnection conn, SQLiteTransaction tx)
        {
            Exec(conn, tx, @"
CREATE TABLE IF NOT EXISTS proxy_list (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    host        TEXT    NOT NULL,
    port        INTEGER NOT NULL,
    type        INTEGER NOT NULL DEFAULT 0,
    username    TEXT    NOT NULL DEFAULT '',
    password    TEXT    NOT NULL DEFAULT '',
    country     TEXT    NOT NULL DEFAULT '',
    latency_ms  INTEGER NOT NULL DEFAULT 0,
    anonymity   TEXT    NOT NULL DEFAULT '',
    status      INTEGER NOT NULL DEFAULT 2
);");
            // 加速按状态查询
            Exec(conn, tx, "CREATE INDEX IF NOT EXISTS idx_proxy_status ON proxy_list(status);");
        }

        private static void CreateExecutionLogTable(SQLiteConnection conn, SQLiteTransaction tx)
        {
            Exec(conn, tx, @"
CREATE TABLE IF NOT EXISTS execution_log (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    executed_at      TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    proxy_ip         TEXT    NOT NULL DEFAULT '',
    target_url       TEXT    NOT NULL DEFAULT '',
    referer          TEXT    NOT NULL DEFAULT '',
    elapsed_ms       INTEGER NOT NULL DEFAULT 0,
    http_status_code INTEGER NOT NULL DEFAULT 0,
    is_success       INTEGER NOT NULL DEFAULT 0,
    is_injected      INTEGER NOT NULL DEFAULT 0
);");
            Exec(conn, tx, "CREATE INDEX IF NOT EXISTS idx_log_executed_at ON execution_log(executed_at);");
        }

        private static void CreateUAProfileTable(SQLiteConnection conn, SQLiteTransaction tx)
        {
            Exec(conn, tx, @"
CREATE TABLE IF NOT EXISTS ua_profile (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    name         TEXT    NOT NULL,
    user_agent   TEXT    NOT NULL,
    device_type  INTEGER NOT NULL DEFAULT 0,
    browser_type TEXT    NOT NULL DEFAULT '',
    is_selected  INTEGER NOT NULL DEFAULT 1
);");
        }

        /// <summary>
        /// 当 ua_profile 表为空时，插入 500 条预置 UA 记录（幂等）。
        /// </summary>
        private static void SeedUAProfiles(SQLiteConnection conn)
        {
            long count;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM ua_profile;";
                count = (long)cmd.ExecuteScalar();
            }
            if (count > 0) return;

            var profiles = BuildSeedProfiles();

            using (var tx = conn.BeginTransaction())
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction  = tx;
                cmd.CommandText  = @"
INSERT INTO ua_profile (name, user_agent, device_type, browser_type, is_selected)
VALUES (@name, @ua, @device, @browser, 1);";

                var pName    = cmd.Parameters.Add("@name",    System.Data.DbType.String);
                var pUa      = cmd.Parameters.Add("@ua",      System.Data.DbType.String);
                var pDevice  = cmd.Parameters.Add("@device",  System.Data.DbType.Int64);
                var pBrowser = cmd.Parameters.Add("@browser", System.Data.DbType.String);

                foreach (var p in profiles)
                {
                    pName.Value    = p.Name;
                    pUa.Value      = p.UserAgent;
                    pDevice.Value  = (int)p.DeviceType;
                    pBrowser.Value = p.BrowserType;
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
        }

        /// <summary>
        /// 通过 平台 × 浏览器版本 组合式生成，确保全局去重，最终输出恰好 500 条。
        /// <para>
        /// 比例分配：Desktop 80%=400，Mobile 10%=50，Tablet 10%=50。
        /// 生成策略（各段精确设计，天然满足目标数，HashSet 仅作安全兜底）：
        ///
        ///   Desktop 400 = Chrome 14ver×12plat(168) + Firefox 15ver×8plat(120)
        ///                 + Edge 14ver×4plat(56)   + Safari 8ver×7macOS(56)
        ///
        ///   Mobile   50 = Chrome(Android)4ver×4dev(16) + Chrome(iPhone)4ver×2iOS(8)
        ///                 + Firefox 4ver×3OS(12) + Edge 4ver×2plat(8) + Safari 6
        ///
        ///   Tablet   50 = Chrome(Android)4ver×3dev(12) + Chrome(iPad)4ver×2iOS(8)
        ///                 + Firefox 4ver×3OS(12) + Edge 4ver×2plat(8) + Safari 10
        /// </para>
        /// </summary>
        private static List<UAProfile> BuildSeedProfiles()
        {
            const int Target = 500;  // Desktop 400(80%) + Mobile 50(10%) + Tablet 50(10%)

            var seen     = new HashSet<string>(StringComparer.Ordinal);
            var profiles = new List<UAProfile>(Target);

            // 将一条 UA 字符串加入列表（已存在则跳过）
            Action<string, DeviceType, string> add = (ua, device, browser) =>
            {
                if (!seen.Add(ua)) return;
                profiles.Add(new UAProfile
                {
                    Name        = MakeUAName(ua, device, browser),
                    UserAgent   = ua,
                    DeviceType  = device,
                    BrowserType = browser,
                    IsSelected  = true,
                });
            };

            // ════════════════════════════════════════════════════════════
            // DESKTOP — 目标 400 条
            // ════════════════════════════════════════════════════════════

            // Chrome 桌面（14 版本 × 12 平台 = 168）
            // 版本：121-134，覆盖 Windows / macOS / Linux / ChromeOS
            int[] chromaDeskVers = { 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134 };
            string[] chromeDeskPlats =
            {
                "Windows NT 10.0; Win64; x64",          // Windows 10/11 64-bit
                "Windows NT 10.0; WOW64",               // Windows 10/11 32-bit
                "Macintosh; Intel Mac OS X 10_15_7",    // macOS Catalina
                "Macintosh; Intel Mac OS X 11_7_10",    // macOS Big Sur
                "Macintosh; Intel Mac OS X 12_7_5",     // macOS Monterey
                "Macintosh; Intel Mac OS X 13_6_7",     // macOS Ventura
                "Macintosh; Intel Mac OS X 14_4_1",     // macOS Sonoma
                "Macintosh; Intel Mac OS X 14_0",       // macOS Sonoma (early)
                "X11; Linux x86_64",                    // Linux generic
                "X11; Ubuntu; Linux x86_64",            // Ubuntu
                "X11; Fedora; Linux x86_64",            // Fedora
                "X11; CrOS x86_64 15633.69.0",         // ChromeOS
            };
            foreach (var ver in chromaDeskVers)
                foreach (var plat in chromeDeskPlats)
                    add(string.Format(
                        "Mozilla/5.0 ({0}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{1}.0.0.0 Safari/537.36",
                        plat, ver),
                        DeviceType.Desktop, "Chrome");
            // 小计：14 × 12 = 168

            // Firefox 桌面（15 版本 × 8 平台格式 = 120）
            // Firefox 将版本号嵌入 platform 字符串 rv:{ver}.0，每个组合均唯一
            int[] ffDeskVers = { 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134 };
            string[] ffDeskPlatFmts =
            {
                "Windows NT 10.0; Win64; x64; rv:{0}.0",
                "Windows NT 10.0; WOW64; rv:{0}.0",
                "Macintosh; Intel Mac OS X 10.15; rv:{0}.0",
                "Macintosh; Intel Mac OS X 14.0; rv:{0}.0",
                "Macintosh; Intel Mac OS X 13.6; rv:{0}.0",
                "X11; Ubuntu; Linux x86_64; rv:{0}.0",
                "X11; Linux x86_64; rv:{0}.0",
                "X11; Fedora; Linux x86_64; rv:{0}.0",
            };
            foreach (var ver in ffDeskVers)
                foreach (var platFmt in ffDeskPlatFmts)
                    add(string.Format(
                        "Mozilla/5.0 ({0}) Gecko/20100101 Firefox/{1}.0",
                        string.Format(platFmt, ver), ver),
                        DeviceType.Desktop, "Firefox");
            // 小计：15 × 8 = 120

            // Edge 桌面（14 版本 × 4 平台 = 56）
            // Edg/{ver} 后缀与 Chrome 桌面 UA 天然区分
            string[] edgeDeskPlats =
            {
                "Windows NT 10.0; Win64; x64",
                "Windows NT 10.0; WOW64",
                "Macintosh; Intel Mac OS X 10_15_7",
                "Macintosh; Intel Mac OS X 14_0",
            };
            foreach (var ver in chromaDeskVers)
                foreach (var plat in edgeDeskPlats)
                    add(string.Format(
                        "Mozilla/5.0 ({0}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{1}.0.0.0 Safari/537.36 Edg/{1}.0.0.0",
                        plat, ver),
                        DeviceType.Desktop, "Edge");
            // 小计：14 × 4 = 56

            // Safari 桌面（8 Safari 版本 × 7 macOS = 56）
            // 按 Safari 版本对应的 macOS 范围选取，避免明显不合理组合：
            //   Safari 17.x → macOS Sonoma (14.x) / Ventura (13.x)
            //   Safari 16.x → macOS Ventura (13.x) / Monterey (12.x)
            //   Safari 15.x → macOS Monterey (12.x) / Big Sur (11.x)
            string[] safariDeskVers = { "17.5", "17.4.1", "17.3", "17.1", "16.6", "16.4", "15.6", "15.4" };
            string[] safariDeskMacOs =
            {
                "14_4_1",   // macOS Sonoma 14.4
                "14_3",     // macOS Sonoma 14.3
                "14_1",     // macOS Sonoma 14.1
                "13_6",     // macOS Ventura 13.6
                "13_5",     // macOS Ventura 13.5
                "12_7",     // macOS Monterey 12.7
                "12_5",     // macOS Monterey 12.5
            };
            foreach (var safVer in safariDeskVers)
                foreach (var macOs in safariDeskMacOs)
                    add(string.Format(
                        "Mozilla/5.0 (Macintosh; Intel Mac OS X {0}) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/{1} Safari/605.1.15",
                        macOs, safVer),
                        DeviceType.Desktop, "Safari");
            // 小计：8 × 7 = 56

            // Desktop 合计：168 + 120 + 56 + 56 = 400

            // ════════════════════════════════════════════════════════════
            // MOBILE — 目标 50 条
            // ════════════════════════════════════════════════════════════

            // 移动端使用较少的版本数（4 个），精确控制总量
            int[] chromaMobVers = { 122, 123, 124, 125 };
            int[] ffMobVers     = { 122, 123, 124, 125 };
            int[] edgeMobVers   = { 122, 123, 124, 125 };
            int[] androidOsVers = { 14, 13, 12 };

            // Chrome 移动端 / Android（4 ver × 4 设备 = 16）
            string[] chromeAndroidPhones =
            {
                "Linux; Android 14; Pixel 8",
                "Linux; Android 14; Pixel 7a",
                "Linux; Android 13; SM-G991B",
                "Linux; Android 13; SM-S918B",
            };
            foreach (var ver in chromaMobVers)
                foreach (var model in chromeAndroidPhones)
                    add(string.Format(
                        "Mozilla/5.0 ({0}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{1}.0.0.0 Mobile Safari/537.36",
                        model, ver),
                        DeviceType.Mobile, "Chrome");
            // 小计：4 × 4 = 16

            // Chrome 移动端 / iPhone CriOS（4 ver × 2 iOS = 8）
            string[] chromeIphoneIosVers = { "17_4_1", "16_7" };
            foreach (var ver in chromaMobVers)
                foreach (var iosVer in chromeIphoneIosVers)
                    add(string.Format(
                        "Mozilla/5.0 (iPhone; CPU iPhone OS {0} like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/{1}.0.0.0 Mobile/15E148 Safari/604.1",
                        iosVer, ver),
                        DeviceType.Mobile, "Chrome");
            // 小计：4 × 2 = 8

            // Firefox 移动端 / Android（4 ver × 3 Android OS = 12）
            foreach (var ver in ffMobVers)
                foreach (var androidVer in androidOsVers)
                    add(string.Format(
                        "Mozilla/5.0 (Android {0}; Mobile; rv:{1}.0) Gecko/{1}.0 Firefox/{1}.0",
                        androidVer, ver),
                        DeviceType.Mobile, "Firefox");
            // 小计：4 × 3 = 12

            // Edge 移动端（4 ver × 2 平台 = 8）
            // EdgA = Edge for Android；EdgiOS = Edge for iOS
            foreach (var ver in edgeMobVers)
            {
                add(string.Format(
                    "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{0}.0.0.0 Mobile Safari/537.36 EdgA/{0}.0.0.0",
                    ver),
                    DeviceType.Mobile, "Edge");
                add(string.Format(
                    "Mozilla/5.0 (iPhone; CPU iPhone OS 17_4_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 EdgiOS/{0}.0.0.0 Mobile/15E148 Safari/604.1",
                    ver),
                    DeviceType.Mobile, "Edge");
            }
            // 小计：4 × 2 = 8

            // Safari 移动端 / iPhone（6 iOS×Safari 对应组合）
            // iOS 版本与 Safari 版本一一对应
            var safariIphoneCombos = new (string IosVer, string SafVer)[]
            {
                ("17_4_1", "17.4.1"), ("17_3", "17.3"), ("17_1", "17.1"),
                ("16_7",   "16.7"),   ("16_5", "16.5"), ("15_8", "15.8"),
            };
            foreach (var combo in safariIphoneCombos)
                add(string.Format(
                    "Mozilla/5.0 (iPhone; CPU iPhone OS {0} like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/{1} Mobile/15E148 Safari/604.1",
                    combo.IosVer, combo.SafVer),
                    DeviceType.Mobile, "Safari");
            // 小计：6

            // Mobile 合计：16 + 8 + 12 + 8 + 6 = 50

            // ════════════════════════════════════════════════════════════
            // TABLET — 目标 50 条
            // ════════════════════════════════════════════════════════════

            // 平板端使用与移动端相同的 4 个版本子集
            int[] chromaTabVers = { 122, 123, 124, 125 };
            int[] ffTabVers     = { 122, 123, 124, 125 };
            int[] edgeTabVers   = { 122, 123, 124, 125 };

            // Chrome 平板 / Android（4 ver × 3 设备 = 12）
            // 设备型号与移动端完全不同，避免 UA 字符串冲突
            string[] chromeAndroidTablets =
            {
                "Linux; Android 14; Pixel Tablet",
                "Linux; Android 13; SM-X900",
                "Linux; Android 13; SM-T870",
            };
            foreach (var ver in chromaTabVers)
                foreach (var model in chromeAndroidTablets)
                    add(string.Format(
                        "Mozilla/5.0 ({0}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{1}.0.0.0 Safari/537.36",
                        model, ver),
                        DeviceType.Tablet, "Chrome");
            // 小计：4 × 3 = 12

            // Chrome 平板 / iPad CriOS（4 ver × 2 iOS = 8）
            // "iPad" 关键字与移动端 "iPhone" 天然区分
            string[] chromeIpadIosVers = { "17_4_1", "16_7" };
            foreach (var ver in chromaTabVers)
                foreach (var iosVer in chromeIpadIosVers)
                    add(string.Format(
                        "Mozilla/5.0 (iPad; CPU OS {0} like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/{1}.0.0.0 Mobile/15E148 Safari/604.1",
                        iosVer, ver),
                        DeviceType.Tablet, "Chrome");
            // 小计：4 × 2 = 8

            // Firefox 平板 / Android（4 ver × 3 Android OS = 12）
            // "Tablet" 关键字与移动端 "Mobile" 天然区分
            foreach (var ver in ffTabVers)
                foreach (var androidVer in androidOsVers)
                    add(string.Format(
                        "Mozilla/5.0 (Android {0}; Tablet; rv:{1}.0) Gecko/{1}.0 Firefox/{1}.0",
                        androidVer, ver),
                        DeviceType.Tablet, "Firefox");
            // 小计：4 × 3 = 12

            // Edge 平板（4 ver × 2 平台 = 8）
            // 设备型号（SM-X900）与移动端（Pixel 8）不同
            foreach (var ver in edgeTabVers)
            {
                add(string.Format(
                    "Mozilla/5.0 (Linux; Android 13; SM-X900) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{0}.0.0.0 Safari/537.36 EdgA/{0}.0.0.0",
                    ver),
                    DeviceType.Tablet, "Edge");
                add(string.Format(
                    "Mozilla/5.0 (iPad; CPU OS 17_4_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 EdgiOS/{0}.0.0.0 Mobile/15E148 Safari/604.1",
                    ver),
                    DeviceType.Tablet, "Edge");
            }
            // 小计：4 × 2 = 8

            // Safari 平板 / iPad（10 iOS×Safari 对应组合）
            var safariIpadCombos = new (string IosVer, string SafVer)[]
            {
                ("17_4_1", "17.4.1"), ("17_3", "17.3"), ("17_1",   "17.1"),
                ("17_0",   "17.0"),   ("16_7", "16.7"), ("16_5",   "16.5"),
                ("16_4",   "16.4"),   ("15_8", "15.8"), ("15_7",   "15.7"),
                ("15_6",   "15.6"),
            };
            foreach (var combo in safariIpadCombos)
                add(string.Format(
                    "Mozilla/5.0 (iPad; CPU OS {0} like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/{1} Mobile/15E148 Safari/604.1",
                    combo.IosVer, combo.SafVer),
                    DeviceType.Tablet, "Safari");
            // 小计：10

            // Tablet 合计：12 + 8 + 12 + 8 + 10 = 50

            // ── 合计 500，去重后精确返回 ─────────────────────────────────
            // 设计上各组合天然唯一，HashSet 仅作安全兜底；
            // 若因意外重复导致 count < Target，GetRange 会返回实际数量。
            return profiles.Count > Target
                ? profiles.GetRange(0, Target)
                : profiles;
        }

        private static string MakeUAName(string ua, DeviceType device, string browser)
        {
            // 从 UA 字符串提取平台标签（用于 Name 字段，无需唯一）
            string platform;
            var u = ua.ToUpperInvariant();
            if (u.Contains("IPHONE"))                                    platform = "iPhone";
            else if (u.Contains("IPAD"))                                 platform = "iPad";
            else if (u.Contains("PIXEL TABLET"))                         platform = "Pixel Tablet";
            else if (u.Contains("PIXEL"))                                platform = "Pixel";
            else if (u.Contains("SM-X") || u.Contains("SM-T"))          platform = "Samsung Tab";
            else if (u.Contains("SM-"))                                  platform = "Samsung";
            else if (u.Contains("SM-F"))                                 platform = "Samsung Fold";
            else if (u.Contains("MACINTOSH") || u.Contains("MAC OS X")) platform = "macOS";
            else if (u.Contains("X11"))                                  platform = "Linux";
            else if (u.Contains("ANDROID"))                              platform = "Android";
            else                                                         platform = "Windows";

            return string.Format("{0} {1} ({2})", browser, device.ToString(), platform);
        }

        /// <summary>
        /// 对已存在数据库执行增量字段迁移（每条 ALTER TABLE 独立 try/catch）。
        /// </summary>
        private static void RunMigrations(SQLiteConnection conn)
        {
            var migrations = new[]
            {
                // task_config 新增 name 列
                "ALTER TABLE task_config ADD COLUMN name TEXT NOT NULL DEFAULT '';",
                // schedule_job 新增执行参数列
                "ALTER TABLE schedule_job ADD COLUMN visit_count  INTEGER NOT NULL DEFAULT 100;",
                "ALTER TABLE schedule_job ADD COLUMN thread_count INTEGER NOT NULL DEFAULT 3;",
                "ALTER TABLE schedule_job ADD COLUMN min_delay    INTEGER NOT NULL DEFAULT 2;",
                "ALTER TABLE schedule_job ADD COLUMN max_delay    INTEGER NOT NULL DEFAULT 8;",
            };

            foreach (var sql in migrations)
            {
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.ExecuteNonQuery();
                    }
                }
                catch
                {
                    // 列已存在时 SQLite 会抛出异常，忽略即可（幂等）
                }
            }
        }

        private static void CreateCookieStoreTable(SQLiteConnection conn, SQLiteTransaction tx)
        {
            Exec(conn, tx, @"
CREATE TABLE IF NOT EXISTS cookie_store (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    profile_id  INTEGER NOT NULL,
    domain      TEXT    NOT NULL,
    cookie_json TEXT    NOT NULL,
    updated_at  TEXT    NOT NULL,
    UNIQUE (profile_id, domain)
);");
            Exec(conn, tx, "CREATE INDEX IF NOT EXISTS idx_cookie_profile ON cookie_store(profile_id);");
        }

        private static void CreateAppSettingsTable(SQLiteConnection conn, SQLiteTransaction tx)
        {
            Exec(conn, tx, @"
CREATE TABLE IF NOT EXISTS app_settings (
    key     TEXT PRIMARY KEY NOT NULL,
    value   TEXT NOT NULL DEFAULT ''
);");

            // 写入默认配置（INSERT OR IGNORE 保证幂等）
            var defaults = new[]
            {
                // 基础
                ("global.timeout_seconds",     "60"),
                ("global.max_retry",           "3"),
                ("global.max_threads",         "10"),
                ("global.dns_timeout_seconds", "5"),
                ("global.keep_alive",          "true"),
                ("global.ignore_ssl_errors",   "true"),
                ("global.gzip",                "true"),
                // Selenium
                ("selenium.chrome_path",       ""),
                ("selenium.driver_path",       ""),
                ("selenium.headless",          "true"),
                ("task.smart_mouse",           "true"),
                ("task.page_interaction",      "true"),
                ("selenium.disable_images",    "true"),
                ("selenium.disable_gpu",       "true"),
                ("selenium.disable_extensions","true"),
                ("selenium.max_memory_mb",     "512"),
                // 指纹
                ("fingerprint.hide_webdriver",     "true"),
                ("fingerprint.inject_plugin_list", "false"),
                ("fingerprint.disable_automation_bar", "true"),
                ("fingerprint.disable_webrtc",     "false"),
                ("fingerprint.canvas_noise",       "false"),
                ("fingerprint.webgl_spoof",        "false"),
                ("fingerprint.random_resolution",  "false"),
                ("fingerprint.random_timezone",    "false"),
                ("fingerprint.random_language",    "false"),
                ("fingerprint.random_ua",          "true"),
                ("fingerprint.rotate_per_session", "true"),
                ("fingerprint.use_selected_only",  "false"),
                // 代理轮换
                ("proxy.rotation_mode",        "sequential"),
            };

            foreach (var (key, value) in defaults)
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT OR IGNORE INTO app_settings(key, value) VALUES (@k, @v);";
                    cmd.Parameters.AddWithValue("@k", key);
                    cmd.Parameters.AddWithValue("@v", value);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
