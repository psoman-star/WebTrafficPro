using System;
using System.Collections.Generic;
using System.Data.SQLite;
using WebTraffic.Core.Models;

namespace WebTraffic.Data.Repositories
{
    public class ScheduleJobRepository : BaseRepository
    {
        public ScheduleJobRepository(string connectionString)
            : base(connectionString)
        {
        }

        // ── 查询 ────────────────────────────────────────────────────────

        public List<ScheduleJob> GetAll()
        {
            var list = new List<ScheduleJob>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM schedule_job ORDER BY id;";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(Map(reader));
                }
            }
            return list;
        }

        public ScheduleJob GetById(int id)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM schedule_job WHERE id = @id LIMIT 1;";
                cmd.Parameters.AddWithValue("@id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        public List<ScheduleJob> GetEnabled()
        {
            var list = new List<ScheduleJob>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM schedule_job WHERE is_enabled = 1 ORDER BY id;";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(Map(reader));
                }
            }
            return list;
        }

        // ── 写入 ────────────────────────────────────────────────────────

        /// <summary>新增定时任务，返回新 Id。</summary>
        public int Add(ScheduleJob job)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO schedule_job (name, cron_expression, last_run_at, is_enabled, task_config_id, visit_count, thread_count, min_delay, max_delay)
VALUES (@name, @cron_expression, @last_run_at, @is_enabled, @task_config_id, @visit_count, @thread_count, @min_delay, @max_delay);
SELECT last_insert_rowid();";

                BindParams(cmd, job);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public void Update(ScheduleJob job)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE schedule_job SET
    name             = @name,
    cron_expression  = @cron_expression,
    last_run_at      = @last_run_at,
    is_enabled       = @is_enabled,
    task_config_id   = @task_config_id,
    visit_count      = @visit_count,
    thread_count     = @thread_count,
    min_delay        = @min_delay,
    max_delay        = @max_delay
WHERE id = @id;";

                cmd.Parameters.AddWithValue("@id", job.Id);
                BindParams(cmd, job);
                cmd.ExecuteNonQuery();
            }
        }

        public void SetEnabled(int id, bool isEnabled)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE schedule_job SET is_enabled = @v WHERE id = @id;";
                cmd.Parameters.AddWithValue("@v",  isEnabled ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>任务执行后更新最后运行时间。</summary>
        public void UpdateLastRunAt(int id, DateTime lastRunAt)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE schedule_job SET last_run_at = @t WHERE id = @id;";
                cmd.Parameters.AddWithValue("@t",  lastRunAt.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public void Delete(int id)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM schedule_job WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        // ── 私有 ────────────────────────────────────────────────────────

        private static void BindParams(SQLiteCommand cmd, ScheduleJob job)
        {
            cmd.Parameters.AddWithValue("@name",            job.Name ?? string.Empty);
            cmd.Parameters.AddWithValue("@cron_expression", job.CronExpression ?? string.Empty);
            cmd.Parameters.AddWithValue("@last_run_at",
                job.LastRunAt.HasValue
                    ? (object)job.LastRunAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
                    : DBNull.Value);
            cmd.Parameters.AddWithValue("@is_enabled",      job.IsEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@task_config_id",  job.TaskConfigId);
            cmd.Parameters.AddWithValue("@visit_count",     job.VisitCount);
            cmd.Parameters.AddWithValue("@thread_count",    job.ThreadCount);
            cmd.Parameters.AddWithValue("@min_delay",       job.MinDelay);
            cmd.Parameters.AddWithValue("@max_delay",       job.MaxDelay);
        }

        private static ScheduleJob Map(SQLiteDataReader r)
        {
            var lastRunRaw = r.GetOrdinal("last_run_at");
            return new ScheduleJob
            {
                Id             = r.GetInt32(r.GetOrdinal("id")),
                Name           = r.GetString(r.GetOrdinal("name")),
                CronExpression = r.GetString(r.GetOrdinal("cron_expression")),
                LastRunAt      = r.IsDBNull(lastRunRaw)
                                     ? (DateTime?)null
                                     : DateTime.Parse(r.GetString(lastRunRaw)),
                IsEnabled      = r.GetInt32(r.GetOrdinal("is_enabled")) == 1,
                TaskConfigId   = r.GetInt32(r.GetOrdinal("task_config_id")),
                VisitCount     = r.GetInt32(r.GetOrdinal("visit_count")),
                ThreadCount    = r.GetInt32(r.GetOrdinal("thread_count")),
                MinDelay       = r.GetInt32(r.GetOrdinal("min_delay")),
                MaxDelay       = r.GetInt32(r.GetOrdinal("max_delay")),
            };
        }
    }
}
