// SPEC §B.19 / §12.12：属性增强表静态配置目录（SlotMachineModal 三轴/五轴老虎机候选与产出）。
// 表 Resources/Configs/Battle/attr_enhance.csv：attrId, attrName, icon, desc, value1..value5。
// value{n} = 该属性项在 n 个轴上同时出现时获得的固定增加值；GetGain(count) 取 value{count}（钳制 1..5）。
// 解析约定与 §B.2.1 / Core/CsvTable 一致：UTF-8、header、`#` 注释、空行跳过、字段 Trim()、非法行 Warning 跳过。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using UnityEngine;

namespace PetDemo.Battle
{
    /// <summary>SPEC §B.19 / §12.12.5：属性增强表一行。</summary>
    public sealed class AttrEnhanceConfig
    {
        public string attrId;    // 作为 RoleStats 字段键：hp/atk/def/speed（atk2→atk、hp2→maxHp 为额外展示项）
        public string attrName;  // 属性名称（展示）
        public string icon;      // 图标 Resources 相对路径（无扩展名），可空 → 占位
        public string desc;      // 文字描述
        public int[] values;     // 长度 5：value1..value5

        /// <summary>出现 count 次时的固定增加值；count 钳制到 1..5。</summary>
        public int GetGain(int count)
        {
            if (values == null || values.Length == 0)
                return 0;
            int idx = Mathf.Clamp(count, 1, values.Length) - 1;
            return values[idx];
        }
    }

    public static class AttrEnhanceConfigCatalog
    {
        public const string AttrEnhanceCsvResourcePath = "Configs/Battle/attr_enhance";
        public const int ValueColumnCount = 5;

        // ============================================================
        // 加载与解析（§B.19.3）
        // ============================================================
        public static List<AttrEnhanceConfig> LoadFromCsv()
        {
            var ta = Resources.Load<TextAsset>(AttrEnhanceCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                Debug.LogWarning("[AttrEnhanceConfigCatalog] 缺少属性增强表 Resources/" +
                    AttrEnhanceCsvResourcePath + "，使用内置默认数据。");
                return BuildDefault();
            }

            var table = CsvTable.Parse(ta.text);
            int idId = table.IndexOfHeader("attrId");
            int idName = table.IndexOfHeader("attrName");
            int idIcon = table.IndexOfHeader("icon");
            int idDesc = table.IndexOfHeader("desc");
            int[] idValues = new int[ValueColumnCount];
            for (int v = 0; v < ValueColumnCount; v++)
                idValues[v] = table.IndexOfHeader("value" + (v + 1));

            if (idId < 0 || idName < 0)
            {
                Debug.LogWarning("[AttrEnhanceConfigCatalog] 属性增强表缺必需列（attrId/attrName），使用内置默认数据。");
                return BuildDefault();
            }

            var list = new List<AttrEnhanceConfig>();
            for (int r = 0; r < table.rows.Count; r++)
            {
                var row = table.rows[r];
                string attrId = row.Get(idId);
                if (string.IsNullOrEmpty(attrId))
                {
                    Debug.LogWarning($"[AttrEnhanceConfigCatalog] 属性增强表第 {row.lineNumber} 行 attrId 为空，跳过。");
                    continue;
                }

                var cfg = new AttrEnhanceConfig
                {
                    attrId = attrId,
                    attrName = idName >= 0 ? row.Get(idName) : attrId,
                    icon = idIcon >= 0 ? row.Get(idIcon) : null,
                    desc = idDesc >= 0 ? row.Get(idDesc) : null,
                    values = new int[ValueColumnCount],
                };
                for (int v = 0; v < ValueColumnCount; v++)
                {
                    string raw = idValues[v] >= 0 ? row.Get(idValues[v]) : null;
                    cfg.values[v] = ParseIntOrZero(raw);
                }
                list.Add(cfg);
            }

            if (list.Count == 0)
            {
                Debug.LogWarning("[AttrEnhanceConfigCatalog] 属性增强表无有效行，使用内置默认数据。");
                return BuildDefault();
            }
            return list;
        }

        private static int ParseIntOrZero(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return 0;
            return int.TryParse(raw.Trim(), out int value) ? value : 0;
        }

        // ============================================================
        // 随机不重复选取（§12.12.2 第一步）
        // ============================================================
        /// <summary>从 all 中随机不重复取 count 项；count 大于池大小时返回洗牌后的全部；输入为空返回空表。</summary>
        public static List<AttrEnhanceConfig> PickDistinct(List<AttrEnhanceConfig> all, int count)
        {
            var result = new List<AttrEnhanceConfig>();
            if (all == null || all.Count == 0 || count <= 0)
                return result;

            var pool = new List<AttrEnhanceConfig>(all);
            int take = Mathf.Min(count, pool.Count);
            for (int i = 0; i < take; i++)
            {
                int idx = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            return result;
        }

        // ============================================================
        // Demo 默认数据（等价 §B.19.2）
        // ============================================================
        public static List<AttrEnhanceConfig> BuildDefault()
        {
            return new List<AttrEnhanceConfig>
            {
                Make("atk", "攻击", "攻击力提升", 3, 8, 15, 24, 35),
                Make("hp", "生命", "生命上限提升", 5, 12, 22, 35, 50),
                Make("def", "防御", "防御力提升", 2, 5, 9, 14, 20),
                Make("speed", "速度", "速度提升", 1, 3, 6, 10, 15),
                Make("atk2", "暴击强化", "额外攻击提升", 4, 10, 18, 28, 40),
                Make("hp2", "体魄", "额外生命提升", 6, 14, 25, 38, 55),
            };
        }

        private static AttrEnhanceConfig Make(string id, string name, string desc,
            int v1, int v2, int v3, int v4, int v5)
        {
            return new AttrEnhanceConfig
            {
                attrId = id,
                attrName = name,
                icon = null,
                desc = desc,
                values = new[] { v1, v2, v3, v4, v5 },
            };
        }
    }
}
