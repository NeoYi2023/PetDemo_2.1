// SPEC §B.24 (v3.208)：解锁说明配置表（仅展示）。
// 表 Resources/Configs/Farm/role_level_unlocks.csv；缺表/解析失败时回退 BuildDefault()。
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.Core
{
    /// <summary>SPEC §B.24：单条解锁说明。</summary>
    public sealed class RoleLevelUnlockConfig
    {
        public string unlockId;
        public int requiredLevel;
        public string iconPath;
        public string title;
        public string description;
        /// <summary>CSV 有效行序（0 起）。</summary>
        public int rowOrder;
    }

    public static class RoleLevelUnlockCatalog
    {
        public const string ResCsvPath = "Configs/Farm/role_level_unlocks";

        private static List<RoleLevelUnlockConfig> cache;

        public static List<RoleLevelUnlockConfig> Load()
        {
            if (cache != null)
                return cache;

            cache = new List<RoleLevelUnlockConfig>();
            var asset = Resources.Load<TextAsset>(ResCsvPath);
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                Debug.LogWarning(
                    "[RoleLevelUnlockCatalog] 缺少配置表 Resources/" + ResCsvPath +
                    ".csv，使用 BuildDefault。");
                cache = BuildDefault();
                return cache;
            }

            var table = CsvTable.Parse(asset.text);
            int iId = table.IndexOfHeader("unlockId");
            int iLevel = table.IndexOfHeader("requiredLevel");
            int iIcon = table.IndexOfHeader("iconPath");
            int iTitle = table.IndexOfHeader("title");
            int iDesc = table.IndexOfHeader("description");
            if (iId < 0 || iLevel < 0 || iIcon < 0 || iTitle < 0 || iDesc < 0)
            {
                Debug.LogWarning("[RoleLevelUnlockCatalog] 表头缺必需列，使用 BuildDefault。");
                cache = BuildDefault();
                return cache;
            }

            int rowOrder = 0;
            for (int r = 0; r < table.rows.Count; r++)
            {
                var row = table.rows[r];
                string unlockId = (row.Get(iId) ?? string.Empty).Trim();
                string title = (row.Get(iTitle) ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(unlockId) || string.IsNullOrEmpty(title))
                {
                    Debug.LogWarning("[RoleLevelUnlockCatalog] 跳过缺 unlockId/title 的行 L" + row.lineNumber);
                    continue;
                }

                if (!int.TryParse((row.Get(iLevel) ?? string.Empty).Trim(), out int level) || level < 1)
                {
                    Debug.LogWarning("[RoleLevelUnlockCatalog] 跳过 requiredLevel 非法的行 L" + row.lineNumber);
                    continue;
                }

                cache.Add(new RoleLevelUnlockConfig
                {
                    unlockId = unlockId,
                    requiredLevel = level,
                    iconPath = (row.Get(iIcon) ?? string.Empty).Trim(),
                    title = title,
                    description = (row.Get(iDesc) ?? string.Empty).Trim(),
                    rowOrder = rowOrder,
                });
                rowOrder++;
            }

            if (cache.Count == 0)
            {
                Debug.LogWarning("[RoleLevelUnlockCatalog] 有效行为空，使用 BuildDefault。");
                cache = BuildDefault();
            }

            return cache;
        }

        /// <summary>按 CSV 行序返回 requiredLevel == level 的解锁项。</summary>
        public static List<RoleLevelUnlockConfig> GetUnlocksForLevel(int level)
        {
            var all = Load();
            var result = new List<RoleLevelUnlockConfig>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].requiredLevel == level)
                    result.Add(all[i]);
            }

            result.Sort((a, b) => a.rowOrder.CompareTo(b.rowOrder));
            return result;
        }

        public static void ClearCache()
        {
            cache = null;
        }

        public static List<RoleLevelUnlockConfig> BuildDefault()
        {
            return new List<RoleLevelUnlockConfig>
            {
                Make("unlock_lv2_train", 2, "AirUI/Skill_1001", "训练入门", "解锁基础训练课程入口（展示）", 0),
                Make("unlock_lv3_arena", 3, "AirUI/WF_JJC", "竞技场预告", "解锁竞技场玩法预告（展示）", 1),
                Make("unlock_lv3_team", 3, "AirUI/WF_ZuDui", "组队预告", "解锁组队玩法预告（展示）", 2),
                Make("unlock_lv5_bounty", 5, "AirUI/WF_XuanShang", "悬赏预告", "解锁悬赏玩法预告（展示）", 3),
                Make("unlock_lv5_manor", 5, "AirUI/WF_ZhuangYuan", "庄园预告", "解锁庄园玩法预告（展示）", 4),
            };
        }

        private static RoleLevelUnlockConfig Make(
            string id, int level, string icon, string title, string desc, int order)
        {
            return new RoleLevelUnlockConfig
            {
                unlockId = id,
                requiredLevel = level,
                iconPath = icon,
                title = title,
                description = desc,
                rowOrder = order,
            };
        }
    }
}
