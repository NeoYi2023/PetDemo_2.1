// SPEC §9.14.11 / §B.20：家园页签气泡文字配置表。
// 数据源：CSV Resources/Configs/HomeTabBubbles.csv；行序 = 展示优先级。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.Core
{
    /// <summary>SPEC §B.20：单条家园气泡配置。</summary>
    public sealed class HomeTabBubbleConfig
    {
        public string entryId;
        public string triggerCondition;
        public string roleAnim;
        public int animPlayCount;
        public string bubbleText;
        /// <summary>CSV 有效行序（0 起），越小优先级越高。</summary>
        public int rowOrder;
    }

    /// <summary>
    /// SPEC §B.20：家园页签气泡配置目录。
    /// <see cref="Load"/> 解析并缓存；<see cref="GetEligible"/> 按行序返回当前满足条件的条目。
    /// </summary>
    public static class HomeTabBubbleCatalog
    {
        public const string ResCsvPath = "Configs/HomeTabBubbles";
        public const string TriggerAlways = "Always";

        private static List<HomeTabBubbleConfig> cache;

        public static List<HomeTabBubbleConfig> Load()
        {
            if (cache != null)
                return cache;

            cache = new List<HomeTabBubbleConfig>();
            var asset = Resources.Load<TextAsset>(ResCsvPath);
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                Debug.LogWarning(
                    "[HomeTabBubbleCatalog] 缺少配置表 Resources/" + ResCsvPath +
                    ".csv，使用 BuildDefault。");
                cache = BuildDefault();
                return cache;
            }

            var table = CsvTable.Parse(asset.text);
            int iEntry = table.IndexOfHeader("entryId");
            int iCond = table.IndexOfHeader("triggerCondition");
            int iAnim = table.IndexOfHeader("roleAnim");
            int iCount = table.IndexOfHeader("animPlayCount");
            int iText = table.IndexOfHeader("bubbleText");
            if (iEntry < 0 || iCond < 0 || iCount < 0 || iText < 0)
            {
                Debug.LogWarning("[HomeTabBubbleCatalog] 表头缺必需列，使用 BuildDefault。");
                cache = BuildDefault();
                return cache;
            }

            int rowOrder = 0;
            for (int r = 0; r < table.rows.Count; r++)
            {
                var row = table.rows[r];
                string entryId = row.Get(iEntry);
                string text = row.Get(iText);
                if (string.IsNullOrEmpty(entryId) || string.IsNullOrEmpty(text))
                {
                    Debug.LogWarning("[HomeTabBubbleCatalog] 跳过缺 entryId/bubbleText 的行 L" + row.lineNumber);
                    continue;
                }

                int playCount = 0;
                int.TryParse(row.Get(iCount) ?? "0", out playCount);
                if (playCount < 0)
                    playCount = 0;

                string rawText = text.Replace("\\n", "\n");
                cache.Add(new HomeTabBubbleConfig
                {
                    entryId = entryId.Trim(),
                    triggerCondition = (row.Get(iCond) ?? string.Empty).Trim(),
                    roleAnim = iAnim >= 0 ? (row.Get(iAnim) ?? string.Empty).Trim() : string.Empty,
                    animPlayCount = playCount,
                    bubbleText = rawText,
                    rowOrder = rowOrder,
                });
                rowOrder++;
            }

            if (cache.Count == 0)
            {
                Debug.LogWarning("[HomeTabBubbleCatalog] 有效行为空，使用 BuildDefault。");
                cache = BuildDefault();
            }

            return cache;
        }

        /// <summary>按 CSV 行序返回当前满足触发条件的条目（本期仅识别 Always）。</summary>
        public static List<HomeTabBubbleConfig> GetEligible()
        {
            var all = Load();
            var result = new List<HomeTabBubbleConfig>();
            for (int i = 0; i < all.Count; i++)
            {
                var cfg = all[i];
                if (IsConditionMet(cfg.triggerCondition, cfg.entryId))
                    result.Add(cfg);
            }

            result.Sort((a, b) => a.rowOrder.CompareTo(b.rowOrder));
            return result;
        }

        public static void ClearCache()
        {
            cache = null;
        }

        public static List<HomeTabBubbleConfig> BuildDefault()
        {
            return new List<HomeTabBubbleConfig>
            {
                new HomeTabBubbleConfig
                {
                    entryId = "bubble_welcome",
                    triggerCondition = TriggerAlways,
                    roleAnim = "exclusive_2",
                    animPlayCount = 0,
                    bubbleText = "欢迎回来！点我继续听我说~",
                    rowOrder = 0,
                },
                new HomeTabBubbleConfig
                {
                    entryId = "bubble_tip_exp",
                    triggerCondition = TriggerAlways,
                    roleAnim = "standby_1",
                    animPlayCount = 2,
                    bubbleText = "下方可以看到等级和经验哦。",
                    rowOrder = 1,
                },
                new HomeTabBubbleConfig
                {
                    entryId = "bubble_tip_attrs",
                    triggerCondition = TriggerAlways,
                    roleAnim = string.Empty,
                    animPlayCount = 0,
                    bubbleText = "信息区可以查看角色六项属性。",
                    rowOrder = 2,
                },
            };
        }

        private static bool IsConditionMet(string condition, string entryId)
        {
            if (string.IsNullOrEmpty(condition))
                return false;

            if (string.Equals(condition, TriggerAlways, StringComparison.OrdinalIgnoreCase))
                return true;

            Debug.LogWarning(
                "[HomeTabBubbleCatalog] 未识别的触发条件 '" + condition +
                "'（entryId=" + entryId + "），视为不满足。");
            return false;
        }
    }
}
