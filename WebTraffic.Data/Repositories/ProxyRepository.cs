using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using WebTraffic.Common.Enums;
using WebTraffic.Core.Models;

namespace WebTraffic.Data.Repositories
{
    public class ProxyRepository : BaseRepository
    {
        public ProxyRepository(string connectionString)
            : base(connectionString)
        {
        }

        // ── 查询 ────────────────────────────────────────────────────────

        public List<ProxyInfo> GetAll(int page, int pageSize)
        {
            var list = new List<ProxyInfo>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT * FROM proxy_list
ORDER BY id DESC
LIMIT @limit OFFSET @offset;";
                cmd.Parameters.AddWithValue("@limit",  pageSize);
                cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(Map(reader));
                }
            }
            return list;
        }

        public List<ProxyInfo> GetByStatus(ProxyStatus status)
        {
            var list = new List<ProxyInfo>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM proxy_list WHERE status = @status ORDER BY id;";
                cmd.Parameters.AddWithValue("@status", (int)status);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(Map(reader));
                }
            }
            return list;
        }

        public int GetTotalCount()
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM proxy_list;";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // ── 批量写入 ────────────────────────────────────────────────────

        /// <summary>
        /// 批量插入代理列表，使用事务批处理，返回实际插入行数。
        /// </summary>
        public int BulkInsert(IEnumerable<ProxyInfo> proxies)
        {
            int count = 0;
            using (var conn = Open())
            using (var tx = conn.BeginTransaction())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
INSERT INTO proxy_list
    (host, port, type, username, password, latency_ms, status)
VALUES
    (@host, @port, @type, @username, @password, @latency_ms, @status);";

                    // 预先添加参数占位，复用 command 对象
                    var pHost      = cmd.Parameters.Add("@host",       DbType.String);
                    var pPort      = cmd.Parameters.Add("@port",       DbType.Int64);
                    var pType      = cmd.Parameters.Add("@type",       DbType.Int64);
                    var pUser      = cmd.Parameters.Add("@username",   DbType.String);
                    var pPass      = cmd.Parameters.Add("@password",   DbType.String);
                    var pLatency   = cmd.Parameters.Add("@latency_ms", DbType.Int64);
                    var pStatus    = cmd.Parameters.Add("@status",     DbType.Int64);

                    foreach (var p in proxies)
                    {
                        pHost.Value    = p.Host ?? string.Empty;
                        pPort.Value    = p.Port;
                        pType.Value    = (int)p.Type;
                        pUser.Value    = p.Username ?? string.Empty;
                        pPass.Value    = p.Password ?? string.Empty;
                        pLatency.Value = p.LatencyMs;
                        pStatus.Value  = (int)p.Status;

                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
                tx.Commit();
            }
            return count;
        }

        // ── 状态更新 ────────────────────────────────────────────────────

        public void UpdateStatus(int id, ProxyStatus status)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE proxy_list SET status = @status WHERE id = @id;";
                cmd.Parameters.AddWithValue("@status", (int)status);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateLatencyAndStatus(int id, int latencyMs, ProxyStatus status)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE proxy_list
SET latency_ms = @latency_ms, status = @status
WHERE id = @id;";
                cmd.Parameters.AddWithValue("@latency_ms", latencyMs);
                cmd.Parameters.AddWithValue("@status",     (int)status);
                cmd.Parameters.AddWithValue("@id",         id);
                cmd.ExecuteNonQuery();
            }
        }

        // ── 删除 ────────────────────────────────────────────────────────

        public void Delete(int id)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM proxy_list WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public void DeleteAll()
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM proxy_list;";
                cmd.ExecuteNonQuery();
            }
        }

        // ── 统计 ────────────────────────────────────────────────────────

        public ProxyCountStats GetCountStats()
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT
    COUNT(*)                                              AS total,
    SUM(CASE WHEN status = 0 THEN 1 ELSE 0 END)          AS active,
    SUM(CASE WHEN status = 1 THEN 1 ELSE 0 END)          AS inactive,
    SUM(CASE WHEN status = 2 THEN 1 ELSE 0 END)          AS unchecked,
    AVG(CASE WHEN status = 0 THEN latency_ms ELSE NULL END) AS avg_latency
FROM proxy_list;";

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new ProxyCountStats
                        {
                            Total           = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                            Active          = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                            Inactive        = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                            Unchecked       = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                            AverageLatencyMs = reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4),
                        };
                    }
                }
            }
            return new ProxyCountStats();
        }

        // ── 私有 ────────────────────────────────────────────────────────

        private static ProxyInfo Map(SQLiteDataReader r)
        {
            return new ProxyInfo
            {
                Id        = r.GetInt32(r.GetOrdinal("id")),
                Host      = r.GetString(r.GetOrdinal("host")),
                Port      = r.GetInt32(r.GetOrdinal("port")),
                Type      = (ProxyType)r.GetInt32(r.GetOrdinal("type")),
                Username  = r.GetString(r.GetOrdinal("username")),
                Password  = r.GetString(r.GetOrdinal("password")),
                LatencyMs = r.GetInt32(r.GetOrdinal("latency_ms")),
                Status    = (ProxyStatus)r.GetInt32(r.GetOrdinal("status")),
            };
        }
    }

    /// <summary>代理数量统计（不含平均延迟以外的聚合，轻量结构）。</summary>
    public class ProxyCountStats
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Inactive { get; set; }
        public int Unchecked { get; set; }
        public double AverageLatencyMs { get; set; }
    }
}
