using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using WebTraffic.Core.Models;

namespace WebTraffic.Data.Repositories
{
    public class ExecutionLogRepository : BaseRepository
    {
        public ExecutionLogRepository(string connectionString)
            : base(connectionString)
        {
        }

        // ── 查询（禁止一次性加载全部记录，强制分页）────────────────────

        /// <summary>
        /// 分页查询执行日志，可按时间范围过滤。
        /// </summary>
        public List<ExecutionLog> GetPaged(
            int page,
            int pageSize,
            DateTime? from = null,
            DateTime? to   = null)
        {
            var list = new List<ExecutionLog>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                var where = BuildWhereClause(from, to);
                cmd.CommandText = $@"
SELECT * FROM execution_log
{where}
ORDER BY id DESC
LIMIT @limit OFFSET @offset;";

                cmd.Parameters.AddWithValue("@limit",  pageSize);
                cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
                AddDateParams(cmd, from, to);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(Map(reader));
                }
            }
            return list;
        }

        /// <summary>
        /// 返回符合条件的总记录数（用于分页计算）。
        /// </summary>
        public int GetCount(DateTime? from = null, DateTime? to = null)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                var where = BuildWhereClause(from, to);
                cmd.CommandText = $"SELECT COUNT(*) FROM execution_log {where};";
                AddDateParams(cmd, from, to);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // ── 写入 ────────────────────────────────────────────────────────

        /// <summary>单条插入，返回新 Id。</summary>
        public int Add(ExecutionLog log)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO execution_log
    (executed_at, proxy_ip, target_url, referer, elapsed_ms, http_status_code, is_success, is_injected)
VALUES
    (@executed_at, @proxy_ip, @target_url, @referer, @elapsed_ms, @http_status_code, @is_success, @is_injected);
SELECT last_insert_rowid();";

                BindParams(cmd, log);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        /// <summary>
        /// 批量写入执行日志（事务批处理）。
        /// </summary>
        public void BulkInsert(IEnumerable<ExecutionLog> logs)
        {
            using (var conn = Open())
            using (var tx = conn.BeginTransaction())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
INSERT INTO execution_log
    (executed_at, proxy_ip, target_url, referer, elapsed_ms, http_status_code, is_success, is_injected)
VALUES
    (@executed_at, @proxy_ip, @target_url, @referer, @elapsed_ms, @http_status_code, @is_success, @is_injected);";

                    var pAt       = cmd.Parameters.Add("@executed_at",      DbType.String);
                    var pIp       = cmd.Parameters.Add("@proxy_ip",         DbType.String);
                    var pUrl      = cmd.Parameters.Add("@target_url",       DbType.String);
                    var pRef      = cmd.Parameters.Add("@referer",          DbType.String);
                    var pMs       = cmd.Parameters.Add("@elapsed_ms",       DbType.Int64);
                    var pCode     = cmd.Parameters.Add("@http_status_code", DbType.Int64);
                    var pSuccess  = cmd.Parameters.Add("@is_success",       DbType.Int64);
                    var pInjected = cmd.Parameters.Add("@is_injected",      DbType.Int64);

                    foreach (var log in logs)
                    {
                        pAt.Value       = log.ExecutedAt.ToString("yyyy-MM-dd HH:mm:ss");
                        pIp.Value       = log.ProxyIp ?? string.Empty;
                        pUrl.Value      = log.TargetUrl ?? string.Empty;
                        pRef.Value      = log.Referer ?? string.Empty;
                        pMs.Value       = log.ElapsedMs;
                        pCode.Value     = log.HttpStatusCode;
                        pSuccess.Value  = log.IsSuccess ? 1 : 0;
                        pInjected.Value = log.IsInjected ? 1 : 0;
                        cmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
        }

        // ── 统计汇总 ────────────────────────────────────────────────────

        public LogSummary GetSummary(DateTime? from = null, DateTime? to = null)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                var where = BuildWhereClause(from, to);
                cmd.CommandText = $@"
SELECT
    COUNT(*)                                               AS total,
    SUM(CASE WHEN is_success = 1 THEN 1 ELSE 0 END)       AS success,
    AVG(elapsed_ms)                                        AS avg_elapsed
FROM execution_log {where};";

                AddDateParams(cmd, from, to);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var total = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        return new LogSummary
                        {
                            Total       = total,
                            SuccessCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                            AvgElapsedMs = reader.IsDBNull(2) ? 0.0 : reader.GetDouble(2),
                        };
                    }
                }
            }
            return new LogSummary();
        }

        /// <summary>
        /// 按小时或按天聚合请求量（用于折线图）。
        /// </summary>
        public List<TrendRow> GetTrend(DateTime from, DateTime to, bool byHour)
        {
            var list = new List<TrendRow>();
            var fmt = byHour ? "%Y-%m-%d %H:00:00" : "%Y-%m-%d";

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
SELECT
    strftime('{fmt}', executed_at)           AS period,
    COUNT(*)                                 AS total,
    SUM(CASE WHEN is_success=1 THEN 1 ELSE 0 END) AS success
FROM execution_log
WHERE executed_at >= @from AND executed_at <= @to
GROUP BY period
ORDER BY period;";

                cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@to",   to.ToString("yyyy-MM-dd HH:mm:ss"));

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new TrendRow
                        {
                            Period       = reader.GetString(0),
                            TotalCount   = reader.GetInt32(1),
                            SuccessCount = reader.GetInt32(2),
                        });
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// 按 Referer 统计来源分布。
        /// </summary>
        public List<RefererCount> GetRefererDistribution(DateTime from, DateTime to)
        {
            var list = new List<RefererCount>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT referer, COUNT(*) AS cnt
FROM execution_log
WHERE executed_at >= @from AND executed_at <= @to
GROUP BY referer
ORDER BY cnt DESC
LIMIT 20;";

                cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@to",   to.ToString("yyyy-MM-dd HH:mm:ss"));

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(new RefererCount { Referer = reader.GetString(0), Count = reader.GetInt32(1) });
                }
            }
            return list;
        }

        // ── 清理 ────────────────────────────────────────────────────────

        /// <summary>删除早于指定日期的日志（防止数据库无限增长）。</summary>
        public void DeleteBefore(DateTime before)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM execution_log WHERE executed_at < @before;";
                cmd.Parameters.AddWithValue("@before", before.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }
        }

        // ── 私有 ────────────────────────────────────────────────────────

        private static string BuildWhereClause(DateTime? from, DateTime? to)
        {
            if (from.HasValue && to.HasValue)
                return "WHERE executed_at >= @from AND executed_at <= @to";
            if (from.HasValue)
                return "WHERE executed_at >= @from";
            if (to.HasValue)
                return "WHERE executed_at <= @to";
            return string.Empty;
        }

        private static void AddDateParams(SQLiteCommand cmd, DateTime? from, DateTime? to)
        {
            if (from.HasValue)
                cmd.Parameters.AddWithValue("@from", from.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            if (to.HasValue)
                cmd.Parameters.AddWithValue("@to", to.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        private static void BindParams(SQLiteCommand cmd, ExecutionLog log)
        {
            cmd.Parameters.AddWithValue("@executed_at",      log.ExecutedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@proxy_ip",         log.ProxyIp ?? string.Empty);
            cmd.Parameters.AddWithValue("@target_url",       log.TargetUrl ?? string.Empty);
            cmd.Parameters.AddWithValue("@referer",          log.Referer ?? string.Empty);
            cmd.Parameters.AddWithValue("@elapsed_ms",       log.ElapsedMs);
            cmd.Parameters.AddWithValue("@http_status_code", log.HttpStatusCode);
            cmd.Parameters.AddWithValue("@is_success",       log.IsSuccess ? 1 : 0);
            cmd.Parameters.AddWithValue("@is_injected",      log.IsInjected ? 1 : 0);
        }

        private static ExecutionLog Map(SQLiteDataReader r)
        {
            return new ExecutionLog
            {
                Id             = r.GetInt32(r.GetOrdinal("id")),
                ExecutedAt     = DateTime.Parse(r.GetString(r.GetOrdinal("executed_at"))),
                ProxyIp        = r.GetString(r.GetOrdinal("proxy_ip")),
                TargetUrl      = r.GetString(r.GetOrdinal("target_url")),
                Referer        = r.GetString(r.GetOrdinal("referer")),
                ElapsedMs      = r.GetInt32(r.GetOrdinal("elapsed_ms")),
                HttpStatusCode = r.GetInt32(r.GetOrdinal("http_status_code")),
                IsSuccess      = r.GetInt32(r.GetOrdinal("is_success")) == 1,
                IsInjected     = r.GetInt32(r.GetOrdinal("is_injected")) == 1,
            };
        }
    }

    public class LogSummary
    {
        public int Total { get; set; }
        public int SuccessCount { get; set; }
        public double AvgElapsedMs { get; set; }
    }

    public class TrendRow
    {
        public string Period { get; set; }
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
    }

    public class RefererCount
    {
        public string Referer { get; set; }
        public int Count { get; set; }
    }
}
