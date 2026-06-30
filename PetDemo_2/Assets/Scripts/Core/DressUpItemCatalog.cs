// SPEC §9.14.9：装扮商店道具配置表加载与排序（仅用于演示，不做真实购买/校验/存储）。
// 数据源：CSV Resources/Configs/DressUpItems.csv，运行时经 Resources.Load<TextAsset> 解析并缓存。
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.Core
{
    /// <summary>SPEC §9.14.9：单个售卖道具的配置项。</summary>
    public sealed class DressUpItemConfig
    {
        public string itemId;
        public int tabIndex;
        public string icon;
        public bool hasIntimacyRequire;
        public int intimacyRequire;
        public int price;
        public int sortOrder;
        public string description;

        /// <summary>解析时记录的 CSV 行序，用于 sortOrder 并列时的稳定排序。</summary>
        public int rowOrder;
    }

    /// <summary>
    /// SPEC §9.14.9：装扮商店道具配置目录。
    /// `Load` 解析并缓存全表；`GetItemsByTab` 返回指定 Tab、按 sortOrder 降序（并列按行序）排序的列表。
    /// </summary>
    public static class DressUpItemCatalog
    {
        public const string ResCsvPath = "Configs/DressUpItems";

        private static List<DressUpItemConfig> cache;

        /// <summary>读取并缓存全部道具配置（缺资源时返回空列表并告警）。</summary>
        public static List<DressUpItemConfig> Load()
        {
            if (cache != null)
                return cache;

            cache = new List<DressUpItemConfig>();

            var asset = Resources.Load<TextAsset>(ResCsvPath);
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                Debug.LogWarning("[DressUpItemCatalog] 缺少配置表 Resources/" + ResCsvPath + ".csv，道具列表为空。");
                return cache;
            }

            var lines = asset.text.Split('\n');
            bool headerSkipped = false;
            int rowOrder = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;

                // 首个有效（非注释/非空）行视为表头并跳过。
                if (!headerSkipped)
                {
                    headerSkipped = true;
                    continue;
                }

                var item = Parse(line, rowOrder);
                if (item != null)
                {
                    cache.Add(item);
                    rowOrder++;
                }
            }

            return cache;
        }

        /// <summary>返回指定 Tab 的道具，按 sortOrder 降序、并列按 CSV 行序稳定排序。</summary>
        public static List<DressUpItemConfig> GetItemsByTab(int tabIndex)
        {
            var all = Load();
            var result = new List<DressUpItemConfig>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].tabIndex == tabIndex)
                    result.Add(all[i]);
            }

            result.Sort((a, b) =>
            {
                if (a.sortOrder != b.sortOrder)
                    return b.sortOrder.CompareTo(a.sortOrder); // sortOrder 降序
                return a.rowOrder.CompareTo(b.rowOrder);        // 并列按行序稳定
            });

            return result;
        }

        /// <summary>清空缓存（便于编辑器下重载配置）。</summary>
        public static void ClearCache()
        {
            cache = null;
        }

        private static DressUpItemConfig Parse(string line, int rowOrder)
        {
            // 列序：itemId,tabIndex,icon,intimacyRequire,price,sortOrder,description
            var cols = line.Split(',');
            if (cols.Length < 7)
            {
                Debug.LogWarning("[DressUpItemCatalog] 跳过列数不足的行: " + line);
                return null;
            }

            var item = new DressUpItemConfig
            {
                itemId = cols[0].Trim(),
                icon = cols[2].Trim(),
                description = cols[6].Trim(),
                rowOrder = rowOrder,
            };

            int.TryParse(cols[1].Trim(), out item.tabIndex);
            int.TryParse(cols[4].Trim(), out item.price);
            int.TryParse(cols[5].Trim(), out item.sortOrder);

            string intimacy = cols[3].Trim();
            if (intimacy.Length > 0 && int.TryParse(intimacy, out int req))
            {
                item.hasIntimacyRequire = true;
                item.intimacyRequire = req;
            }

            return item;
        }
    }
}
