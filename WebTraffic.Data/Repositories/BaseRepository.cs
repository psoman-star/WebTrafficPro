using System;
using System.Data.SQLite;

namespace WebTraffic.Data.Repositories
{
    /// <summary>
    /// 所有 Repository 的抽象基类。
    /// 负责：静态一次性 PRAGMA 初始化（WAL / busy_timeout / synchronous），
    /// 以及统一的 Open() 连接工厂。
    /// </summary>
    public abstract class BaseRepository
    {
        private static readonly object _pragmaLock = new object();
        private static bool _pragmaInitialized = false;

        protected readonly string _connectionString;

        protected BaseRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException("connectionString");

            _connectionString = connectionString;
            EnsurePragmaInitialized(connectionString);
        }

        /// <summary>
        /// 打开并返回一个已就绪的 SQLiteConnection，调用方负责 using 释放。
        /// busy_timeout 在每条连接上单独设置（PRAGMA 为连接级，不随连接字符串持久化）。
        /// </summary>
        protected SQLiteConnection Open()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA busy_timeout=5000;";
                cmd.ExecuteNonQuery();
            }
            return conn;
        }

        // ── 静态一次性初始化 ────────────────────────────────────────────

        private static void EnsurePragmaInitialized(string connectionString)
        {
            if (_pragmaInitialized) return;
            lock (_pragmaLock)
            {
                if (_pragmaInitialized) return;
                using (var conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA journal_mode=WAL;";
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA synchronous=NORMAL;";
                        cmd.ExecuteNonQuery();
                    }
                }
                _pragmaInitialized = true;
            }
        }
    }
}
