using System;
using System.Collections.Generic;
using System.Data.SQLite;
using WebTraffic.Core.Models;

namespace WebTraffic.Data.Repositories
{
    public class TaskConfigRepository : BaseRepository
    {
        public TaskConfigRepository(string connectionString)
            : base(connectionString)
        {
        }

        // ── CRUD ────────────────────────────────────────────────────────

        public List<TaskConfig> GetAll()
        {
            var list = new List<TaskConfig>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM task_config ORDER BY id DESC;";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(Map(reader));
                }
            }
            return list;
        }

        public TaskConfig GetById(int id)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM task_config WHERE id = @id LIMIT 1;";
                cmd.Parameters.AddWithValue("@id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        /// <summary>
        /// 新增任务配置，返回新记录 Id。
        /// </summary>
        public int Add(TaskConfig config)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO task_config
    (name, target_url, keywords, visit_count, thread_count, min_delay, max_delay, source_weights)
VALUES
    (@name, @target_url, @keywords, @visit_count, @thread_count, @min_delay, @max_delay, @source_weights);
SELECT last_insert_rowid();";

                BindParams(cmd, config);
                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result);
            }
        }

        public void Update(TaskConfig config)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE task_config SET
    name           = @name,
    target_url     = @target_url,
    keywords       = @keywords,
    visit_count    = @visit_count,
    thread_count   = @thread_count,
    min_delay      = @min_delay,
    max_delay      = @max_delay,
    source_weights = @source_weights
WHERE id = @id;";

                cmd.Parameters.AddWithValue("@id", config.Id);
                BindParams(cmd, config);
                cmd.ExecuteNonQuery();
            }
        }

        public void Delete(int id)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM task_config WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        // ── 模板 ────────────────────────────────────────────────────────

        public List<TaskTemplate> GetAllTemplates()
        {
            var list = new List<TaskTemplate>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM task_template ORDER BY id DESC;";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(MapTemplate(reader));
                }
            }
            return list;
        }

        /// <summary>保存任务模板，返回新 Id。</summary>
        public int SaveTemplate(TaskTemplate template)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO task_template (name, config_json, created_at)
VALUES (@name, @config_json, @created_at);
SELECT last_insert_rowid();";

                cmd.Parameters.AddWithValue("@name", template.Name ?? string.Empty);
                cmd.Parameters.AddWithValue("@config_json", template.ConfigJson ?? string.Empty);
                cmd.Parameters.AddWithValue("@created_at", template.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result);
            }
        }

        public void DeleteTemplate(int id)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM task_template WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        // ── 私有 ────────────────────────────────────────────────────────

        private static void BindParams(SQLiteCommand cmd, TaskConfig c)
        {
            cmd.Parameters.AddWithValue("@name",           c.Name ?? string.Empty);
            cmd.Parameters.AddWithValue("@target_url",     c.TargetUrl ?? string.Empty);
            cmd.Parameters.AddWithValue("@keywords",       c.Keywords ?? string.Empty);
            cmd.Parameters.AddWithValue("@visit_count",    c.VisitCount);
            cmd.Parameters.AddWithValue("@thread_count",   c.ThreadCount);
            cmd.Parameters.AddWithValue("@min_delay",      c.MinDelay);
            cmd.Parameters.AddWithValue("@max_delay",      c.MaxDelay);
            cmd.Parameters.AddWithValue("@source_weights", c.SourceWeights ?? "{}");
        }

        private static TaskConfig Map(SQLiteDataReader r)
        {
            return new TaskConfig
            {
                Id            = r.GetInt32(r.GetOrdinal("id")),
                Name          = r.GetString(r.GetOrdinal("name")),
                TargetUrl     = r.GetString(r.GetOrdinal("target_url")),
                Keywords      = r.GetString(r.GetOrdinal("keywords")),
                VisitCount    = r.GetInt32(r.GetOrdinal("visit_count")),
                ThreadCount   = r.GetInt32(r.GetOrdinal("thread_count")),
                MinDelay      = r.GetInt32(r.GetOrdinal("min_delay")),
                MaxDelay      = r.GetInt32(r.GetOrdinal("max_delay")),
                SourceWeights = r.GetString(r.GetOrdinal("source_weights")),
            };
        }

        private static TaskTemplate MapTemplate(SQLiteDataReader r)
        {
            return new TaskTemplate
            {
                Id         = r.GetInt32(r.GetOrdinal("id")),
                Name       = r.GetString(r.GetOrdinal("name")),
                ConfigJson = r.GetString(r.GetOrdinal("config_json")),
                CreatedAt  = DateTime.Parse(r.GetString(r.GetOrdinal("created_at"))),
            };
        }
    }
}
