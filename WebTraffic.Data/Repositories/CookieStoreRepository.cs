using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SQLite;
using WebTraffic.Core.Models;

namespace WebTraffic.Data.Repositories
{
    public class CookieStoreRepository : BaseRepository
    {
        public CookieStoreRepository(string connectionString)
            : base(connectionString)
        {
        }

        /// <summary>
        /// 保存 Cookie：已存在（profileId + domain 相同）则更新，否则插入。
        /// </summary>
        public async Task SaveCookiesAsync(int profileId, string domain, string cookieJson)
        {
            await Task.Run(() =>
            {
                using (var conn = Open())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
INSERT INTO cookie_store (profile_id, domain, cookie_json, updated_at)
VALUES (@profile_id, @domain, @cookie_json, @updated_at)
ON CONFLICT(profile_id, domain) DO UPDATE SET
    cookie_json = excluded.cookie_json,
    updated_at  = excluded.updated_at;";

                    cmd.Parameters.AddWithValue("@profile_id",  profileId);
                    cmd.Parameters.AddWithValue("@domain",      domain ?? string.Empty);
                    cmd.Parameters.AddWithValue("@cookie_json", cookieJson ?? string.Empty);
                    cmd.Parameters.AddWithValue("@updated_at",  DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 获取指定 profileId 的所有 Cookie 记录，按 domain 排序。
        /// </summary>
        public async Task<List<CookieStore>> GetCookiesAsync(int profileId)
        {
            return await Task.Run(() =>
            {
                var list = new List<CookieStore>();
                using (var conn = Open())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT id, profile_id, domain, cookie_json, updated_at
FROM cookie_store
WHERE profile_id = @profile_id
ORDER BY domain;";

                    cmd.Parameters.AddWithValue("@profile_id", profileId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            list.Add(Map(reader));
                    }
                }
                return list;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 删除 updated_at 早于 30 天前的过期记录。
        /// </summary>
        public async Task DeleteExpiredAsync()
        {
            await Task.Run(() =>
            {
                using (var conn = Open())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM cookie_store WHERE updated_at < @cutoff;";
                    cmd.Parameters.AddWithValue("@cutoff",
                        DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }
            }).ConfigureAwait(false);
        }

        // ── 私有 ────────────────────────────────────────────────────────

        private static CookieStore Map(SQLiteDataReader r)
        {
            return new CookieStore
            {
                Id         = r.GetInt32(r.GetOrdinal("id")),
                ProfileId  = r.GetInt32(r.GetOrdinal("profile_id")),
                Domain     = r.GetString(r.GetOrdinal("domain")),
                CookieJson = r.GetString(r.GetOrdinal("cookie_json")),
                UpdatedAt  = DateTime.Parse(r.GetString(r.GetOrdinal("updated_at"))),
            };
        }
    }
}
