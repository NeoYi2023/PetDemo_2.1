// SPEC §9.14.8 第 1 点（v3.158）：创角界面「亲密度」页签好友列表配置表加载。
// 数据源：CSV Resources/Configs/TopFriends.csv，运行时经 Resources.Load<TextAsset> + CsvTable.Parse 解析并缓存。
// 缺表/全表非法时回退 FriendCatalog.BuildDefault()。
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.Core
{
    /// <summary>
    /// SPEC §9.14.8 第 1 点：亲密度好友列表配置目录。
    /// `Load` 解析并缓存全表为 <see cref="FriendProfile"/>；缺资源回退默认目录。
    /// </summary>
    public static class TopFriendCatalog
    {
        public const string ResCsvPath = "Configs/TopFriends";

        private static List<FriendProfile> cache;

        /// <summary>读取并缓存全部好友配置（缺资源/全非法时回退 <see cref="FriendCatalog.BuildDefault"/>）。</summary>
        public static List<FriendProfile> Load()
        {
            if (cache != null)
                return cache;

            var asset = Resources.Load<TextAsset>(ResCsvPath);
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                Debug.LogWarning("[TopFriendCatalog] 缺少配置表 Resources/" + ResCsvPath + ".csv，回退默认好友目录。");
                cache = FriendCatalog.BuildDefault();
                return cache;
            }

            var table = CsvTable.Parse(asset.text);
            int idxId = table.IndexOfHeader("id");
            int idxName = table.IndexOfHeader("displayName");
            int idxAvatar = table.IndexOfHeader("avatar");
            int idxGender = table.IndexOfHeader("gender");
            int idxIntimacy = table.IndexOfHeader("intimacy");
            int idxInterrupted = table.IndexOfHeader("intimacyInterrupted");
            int idxOnline = table.IndexOfHeader("online");
            int idxAvatarFrame = table.IndexOfHeader("avatarFrame");
            int idxSpine = table.IndexOfHeader("spinePrefab");
            int idxHasPartner = table.IndexOfHeader("hasPartner");

            if (idxId < 0 || idxName < 0)
            {
                Debug.LogWarning("[TopFriendCatalog] 配置表缺少必需列 id/displayName，回退默认好友目录。");
                cache = FriendCatalog.BuildDefault();
                return cache;
            }

            var list = new List<FriendProfile>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                string id = row.Get(idxId);
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning("[TopFriendCatalog] 跳过 id 为空的行 (line " + row.lineNumber + ")。");
                    continue;
                }

                int intimacy = 0;
                if (idxIntimacy >= 0)
                    int.TryParse(row.Get(idxIntimacy), out intimacy);

                var profile = new FriendProfile
                {
                    id = id,
                    displayName = row.Get(idxName) ?? "",
                    avatarResource = idxAvatar >= 0 ? row.Get(idxAvatar) : "",
                    intimacy = Mathf.Clamp(intimacy, 0, FriendCatalog.IntimacyMax),
                    isFemale = idxGender >= 0 && ParseFemale(row.Get(idxGender)),
                    intimacyInterrupted = idxInterrupted >= 0 && ParseBool(row.Get(idxInterrupted)),
                    online = idxOnline >= 0 && ParseBool(row.Get(idxOnline)),
                    avatarFrameResource = idxAvatarFrame >= 0 ? (row.Get(idxAvatarFrame) ?? "") : "",
                    spinePrefabPath = idxSpine >= 0 ? (row.Get(idxSpine) ?? "") : "",
                    hasPartner = idxHasPartner >= 0 && ParseBool(row.Get(idxHasPartner)),
                };
                list.Add(profile);
            }

            if (list.Count == 0)
            {
                Debug.LogWarning("[TopFriendCatalog] 配置表无有效行，回退默认好友目录。");
                cache = FriendCatalog.BuildDefault();
                return cache;
            }

            cache = list;
            return cache;
        }

        /// <summary>按 id 查找好友配置（线性扫描缓存；未命中返回 false）。</summary>
        public static bool TryGetById(string id, out FriendProfile profile)
        {
            profile = null;
            if (string.IsNullOrEmpty(id))
                return false;

            var list = Load();
            if (list == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && string.Equals(list[i].id, id, System.StringComparison.Ordinal))
                {
                    profile = list[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// SPEC §9.14.2 / §9.8.18.3（v3.258）：新会话好友列表（CSV 深拷贝，可安全变更 intimacy 而不污染缓存）。
        /// </summary>
        public static List<FriendProfile> CreateSessionList()
        {
            var source = Load();
            var list = new List<FriendProfile>(source != null ? source.Count : 0);
            if (source == null)
                return list;
            for (int i = 0; i < source.Count; i++)
            {
                var src = source[i];
                if (src == null || string.IsNullOrEmpty(src.id))
                    continue;
                list.Add(CloneProfile(src));
            }
            return list;
        }

        /// <summary>
        /// SPEC §9.8.18.3（v3.258）：按 id 把 CSV 配置态字段写回会话好友（亲密度/在线等运行态保留）。
        /// </summary>
        public static void ApplyCsvStaticFields(IList<FriendProfile> friends)
        {
            if (friends == null || friends.Count == 0)
                return;

            var source = Load();
            if (source == null || source.Count == 0)
                return;

            var byId = new Dictionary<string, FriendProfile>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                var src = source[i];
                if (src == null || string.IsNullOrEmpty(src.id) || byId.ContainsKey(src.id))
                    continue;
                byId[src.id] = src;
            }

            for (int i = 0; i < friends.Count; i++)
            {
                var friend = friends[i];
                if (friend == null || string.IsNullOrEmpty(friend.id))
                    continue;
                if (!byId.TryGetValue(friend.id, out var cfg) || cfg == null)
                    continue;

                friend.isFemale = cfg.isFemale;
                friend.hasPartner = cfg.hasPartner;
                friend.intimacyInterrupted = cfg.intimacyInterrupted;
                friend.avatarFrameResource = cfg.avatarFrameResource ?? "";
                friend.spinePrefabPath = cfg.spinePrefabPath ?? "";
                if (!string.IsNullOrEmpty(cfg.displayName))
                    friend.displayName = cfg.displayName;
                if (!string.IsNullOrEmpty(cfg.avatarResource))
                    friend.avatarResource = cfg.avatarResource;
            }
        }

        /// <summary>清空缓存（便于编辑器下重载配置）。</summary>
        public static void ClearCache()
        {
            cache = null;
        }

        private static FriendProfile CloneProfile(FriendProfile src)
        {
            return new FriendProfile
            {
                id = src.id,
                displayName = src.displayName,
                avatarResource = src.avatarResource,
                online = src.online,
                intimacy = src.intimacy,
                isFemale = src.isFemale,
                intimacyInterrupted = src.intimacyInterrupted,
                avatarFrameResource = src.avatarFrameResource,
                spinePrefabPath = src.spinePrefabPath,
                hasPartner = src.hasPartner,
            };
        }

        private static bool ParseBool(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return false;
            raw = raw.Trim().ToLowerInvariant();
            return raw == "true" || raw == "1" || raw == "yes" || raw == "y";
        }

        private static bool ParseFemale(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return false;
            raw = raw.Trim().ToLowerInvariant();
            return raw == "female" || raw == "f" || raw == "woman" || raw == "女";
        }
    }
}
