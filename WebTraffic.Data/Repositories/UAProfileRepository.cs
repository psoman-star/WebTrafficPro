using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using WebTraffic.Common.Enums;
using WebTraffic.Core.Models;

namespace WebTraffic.Data.Repositories
{
    public class UAProfileRepository : BaseRepository
    {
        public UAProfileRepository(string connectionString)
            : base(connectionString)
        {
        }

        // ── 查询 ────────────────────────────────────────────────────────

        public List<UAProfile> GetAll()
        {
            var list = new List<UAProfile>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM ua_profile ORDER BY id;";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(Map(reader));
                }
            }
            return list;
        }

        /// <summary>
        /// 按设备类型和浏览器类型过滤，null 表示不过滤该维度。
        /// </summary>
        public List<UAProfile> GetFiltered(DeviceType? deviceType, string browserType)
        {
            var list   = new List<UAProfile>();
            var where  = new List<string>();

            if (deviceType.HasValue)  where.Add("device_type = @device_type");
            if (!string.IsNullOrEmpty(browserType)) where.Add("browser_type = @browser_type");

            var sql = "SELECT * FROM ua_profile"
                    + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty)
                    + " ORDER BY id;";

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                if (deviceType.HasValue)
                    cmd.Parameters.AddWithValue("@device_type", (int)deviceType.Value);
                if (!string.IsNullOrEmpty(browserType))
                    cmd.Parameters.AddWithValue("@browser_type", browserType);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(Map(reader));
                }
            }
            return list;
        }

        public List<UAProfile> GetSelected()
        {
            var list = new List<UAProfile>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM ua_profile WHERE is_selected = 1 ORDER BY id;";
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
                cmd.CommandText = "SELECT COUNT(*) FROM ua_profile;";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public int GetSelectedCount()
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM ua_profile WHERE is_selected = 1;";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // ── 写入 ────────────────────────────────────────────────────────

        /// <summary>批量插入 UA，使用事务批处理，返回插入数量。</summary>
        public int BulkInsert(IEnumerable<UAProfile> profiles)
        {
            int count = 0;
            using (var conn = Open())
            using (var tx = conn.BeginTransaction())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
INSERT INTO ua_profile (name, user_agent, device_type, browser_type, is_selected)
VALUES (@name, @user_agent, @device_type, @browser_type, @is_selected);";

                    var pName    = cmd.Parameters.Add("@name",        DbType.String);
                    var pUa      = cmd.Parameters.Add("@user_agent",  DbType.String);
                    var pDevice  = cmd.Parameters.Add("@device_type", DbType.Int64);
                    var pBrowser = cmd.Parameters.Add("@browser_type",DbType.String);
                    var pSel     = cmd.Parameters.Add("@is_selected", DbType.Int64);

                    foreach (var p in profiles)
                    {
                        pName.Value    = p.Name ?? string.Empty;
                        pUa.Value      = p.UserAgent ?? string.Empty;
                        pDevice.Value  = (int)p.DeviceType;
                        pBrowser.Value = p.BrowserType ?? string.Empty;
                        pSel.Value     = p.IsSelected ? 1 : 0;
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
                tx.Commit();
            }
            return count;
        }

        public void SetSelected(int id, bool isSelected)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE ua_profile SET is_selected = @v WHERE id = @id;";
                cmd.Parameters.AddWithValue("@v",  isSelected ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public void SetAllSelected(bool isSelected)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE ua_profile SET is_selected = @v;";
                cmd.Parameters.AddWithValue("@v", isSelected ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }

        public void Delete(int id)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM ua_profile WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        // ── 私有 ────────────────────────────────────────────────────────

        private static UAProfile Map(SQLiteDataReader r)
        {
            return new UAProfile
            {
                Id          = r.GetInt32(r.GetOrdinal("id")),
                Name        = r.GetString(r.GetOrdinal("name")),
                UserAgent   = r.GetString(r.GetOrdinal("user_agent")),
                DeviceType  = (DeviceType)r.GetInt32(r.GetOrdinal("device_type")),
                BrowserType = r.GetString(r.GetOrdinal("browser_type")),
                IsSelected  = r.GetInt32(r.GetOrdinal("is_selected")) == 1,
            };
        }
    }
}
