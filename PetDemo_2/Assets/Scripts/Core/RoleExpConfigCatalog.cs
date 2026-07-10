// SPEC §B.23 (v3.208)：主角经验表。
// 表 Resources/Configs/Farm/role_exp.csv；缺表/解析失败时回退 BuildDefault()。
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.Core
{
    /// <summary>SPEC §B.23：主角经验表一行。</summary>
    public sealed class RoleExpConfig
    {
        public int targetLevel;
        public int expForLevel;
        public int cumulativeExp;
    }

    public static class RoleExpConfigCatalog
    {
        public const string RoleExpCsvResourcePath = "Configs/Farm/role_exp";

        private static Dictionary<int, RoleExpConfig> cache;

        public static Dictionary<int, RoleExpConfig> GetAll()
        {
            if (cache != null)
                return cache;
            cache = LoadFromCsv();
            return cache;
        }

        public static bool TryGet(int targetLevel, out RoleExpConfig config)
        {
            var all = GetAll();
            if (all != null && all.TryGetValue(targetLevel, out config) && config != null)
                return true;
            config = null;
            return false;
        }

        /// <summary>处于 <paramref name="level"/> 时升到下一级所需的单级经验；缺行返回 0。</summary>
        public static int GetExpForLevel(int level)
        {
            if (level <= 0)
                level = 1;
            if (TryGet(level, out var cfg) && cfg != null)
                return Mathf.Max(1, cfg.expForLevel);
            return 0;
        }

        public static int GetCumulative(int level)
        {
            if (level <= 0)
                level = 1;
            if (TryGet(level, out var cfg) && cfg != null)
                return Mathf.Max(0, cfg.cumulativeExp);
            return 0;
        }

        public static void ClearCacheForEditor()
        {
            cache = null;
        }

        public static Dictionary<int, RoleExpConfig> LoadFromCsv()
        {
            var ta = Resources.Load<TextAsset>(RoleExpCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                Debug.LogWarning("[RoleExpConfigCatalog] 缺少经验表 Resources/" +
                    RoleExpCsvResourcePath + "，使用内置默认数据。");
                return BuildDefault();
            }

            var table = CsvTable.Parse(ta.text);
            int idLevel = table.IndexOfHeader("targetLevel");
            int idExp = table.IndexOfHeader("expForLevel");
            int idCum = table.IndexOfHeader("cumulativeExp");
            if (idLevel < 0 || idExp < 0 || idCum < 0)
            {
                Debug.LogWarning("[RoleExpConfigCatalog] 经验表缺必需列，使用内置默认数据。");
                return BuildDefault();
            }

            var map = new Dictionary<int, RoleExpConfig>();
            for (int r = 0; r < table.rows.Count; r++)
            {
                var row = table.rows[r];
                if (!TryParsePositiveInt(row.Get(idLevel), out int level))
                {
                    Debug.LogWarning($"[RoleExpConfigCatalog] 第 {row.lineNumber} 行 targetLevel 非法，跳过。");
                    continue;
                }

                map[level] = new RoleExpConfig
                {
                    targetLevel = level,
                    expForLevel = Mathf.Max(1, ParseIntOrZero(row.Get(idExp))),
                    cumulativeExp = Mathf.Max(0, ParseIntOrZero(row.Get(idCum))),
                };
            }

            if (map.Count == 0)
            {
                Debug.LogWarning("[RoleExpConfigCatalog] 经验表无有效行，使用内置默认数据。");
                return BuildDefault();
            }
            return map;
        }

        public static Dictionary<int, RoleExpConfig> BuildDefault()
        {
            var map = new Dictionary<int, RoleExpConfig>();
            Add(map, 1, 100, 100);
            Add(map, 2, 150, 250);
            Add(map, 3, 200, 450);
            Add(map, 4, 260, 710);
            Add(map, 5, 330, 1040);
            Add(map, 6, 410, 1450);
            Add(map, 7, 500, 1950);
            Add(map, 8, 600, 2550);
            Add(map, 9, 720, 3270);
            Add(map, 10, 850, 4120);
            return map;
        }

        private static void Add(Dictionary<int, RoleExpConfig> map, int level, int exp, int cum)
        {
            map[level] = new RoleExpConfig
            {
                targetLevel = level,
                expForLevel = exp,
                cumulativeExp = cum,
            };
        }

        private static bool TryParsePositiveInt(string raw, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(raw))
                return false;
            if (!int.TryParse(raw.Trim(), out value))
                return false;
            return value >= 1;
        }

        private static int ParseIntOrZero(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return 0;
            return int.TryParse(raw.Trim(), out int v) ? v : 0;
        }
    }
}
