// SPEC §B.19 / §12.12 / §12.13：属性增强表静态配置目录（SlotMachineModal 三轴/五轴老虎机候选与产出 + 详细属性六宫图）。
// 表 Resources/Configs/Battle/attr_enhance.csv：attrId, attrName, icon, desc, value1..value5。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using UnityEngine;

namespace PetDemo.Battle
{
    /// <summary>SPEC §B.19 / §12.12.5：属性增强表一行。</summary>
    public sealed class AttrEnhanceConfig
    {
        public string attrId;
        public string attrName;
        public string icon;
        public string desc;
        public int[] values;

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

        public const string AttrLife = "Life";
        public const string AttrAttack = "Attack";
        public const string AttrCriticalHit = "Critical Hit";
        public const string AttrCombo = "Combo";
        public const string AttrCounterattack = "Counterattack";
        public const string AttrStun = "Stun";
        public const string AttrEvasion = "Evasion";
        public const string AttrLifeSteal = "Life Steal";

        /// <summary>六宫图属性 id（顺序 = 0° 起顺时针每 60°，§12.13.2）。</summary>
        public static readonly string[] HexRadarAttrIds =
        {
            AttrCriticalHit,
            AttrCombo,
            AttrCounterattack,
            AttrStun,
            AttrEvasion,
            AttrLifeSteal,
        };

        public static readonly string[] HexRadarDisplayNames =
        {
            "暴击", "连击", "反击", "击晕", "闪避", "吸血",
        };

        // ============================================================
        // attrId 规范化（§12.12.3 / §B.19）
        // ============================================================
        public static bool TryNormalizeAttrId(string raw, out string canonical)
        {
            canonical = null;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string key = raw.Trim();
            switch (key.ToLowerInvariant())
            {
                case "life":
                case "hp":
                case "hp2":
                    canonical = AttrLife;
                    return true;
                case "attack":
                case "atk":
                case "atk2":
                    canonical = AttrAttack;
                    return true;
                case "def":
                    canonical = "def";
                    return true;
                case "speed":
                    canonical = "speed";
                    return true;
                case "critical hit":
                case "criticalhit":
                    canonical = AttrCriticalHit;
                    return true;
                case "combo":
                    canonical = AttrCombo;
                    return true;
                case "counterattack":
                    canonical = AttrCounterattack;
                    return true;
                case "stun":
                    canonical = AttrStun;
                    return true;
                case "evasion":
                    canonical = AttrEvasion;
                    return true;
                case "life steal":
                case "lifesteal":
                    canonical = AttrLifeSteal;
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsHexRadarAttr(string canonicalOrRaw)
        {
            if (TryNormalizeAttrId(canonicalOrRaw, out string c))
            {
                for (int i = 0; i < HexRadarAttrIds.Length; i++)
                {
                    if (string.Equals(HexRadarAttrIds[i], c, StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        /// <summary>从局外 RoleStats 读取六宫属性值（§5 / §12.13）。</summary>
        public static int GetHexValueFromRole(RoleStats role, string canonicalAttrId)
        {
            if (role == null || string.IsNullOrEmpty(canonicalAttrId))
                return 0;
            switch (canonicalAttrId)
            {
                case AttrCriticalHit: return Mathf.Max(0, role.criticalHit);
                case AttrCombo: return Mathf.Max(0, role.combo);
                case AttrCounterattack: return Mathf.Max(0, role.counterattack);
                case AttrStun: return Mathf.Max(0, role.stun);
                case AttrEvasion: return Mathf.Max(0, role.evasion);
                case AttrLifeSteal: return Mathf.Max(0, role.lifeSteal);
                default: return 0;
            }
        }

        /// <summary>将局外 RoleStats 六宫字段写入 runEnhanceBonuses 基线（Show 时调用）。</summary>
        public static void SeedHexBonusesFromRole(RoleStats role, Dictionary<string, int> target)
        {
            if (target == null)
                return;
            target.Clear();
            if (role == null)
                return;
            for (int i = 0; i < HexRadarAttrIds.Length; i++)
            {
                string id = HexRadarAttrIds[i];
                target[id] = GetHexValueFromRole(role, id);
            }
        }

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
                    attrId = attrId.Trim(),
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

        public static List<AttrEnhanceConfig> BuildDefault()
        {
            return new List<AttrEnhanceConfig>
            {
                Make(AttrLife, "生命", "battle_img_009_01", 1, 3, 6, 12, 20),
                Make(AttrAttack, "攻击", "battle_img_001_01", 1, 3, 6, 12, 20),
                Make(AttrCriticalHit, "暴击", "battle_img_003_01", 1, 3, 6, 12, 20),
                Make(AttrCombo, "连击", "battle_img_005_01", 1, 3, 6, 12, 20),
                Make(AttrCounterattack, "反击", "battle_img_007_01", 1, 3, 6, 12, 20),
                Make(AttrStun, "击晕", "battle_img_008_01", 1, 3, 6, 12, 20),
                Make(AttrEvasion, "闪避", "battle_img_004_01", 1, 3, 6, 12, 20),
                Make(AttrLifeSteal, "吸血", "battle_img_006_01", 1, 3, 6, 12, 20),
            };
        }

        private static AttrEnhanceConfig Make(string id, string name, string icon,
            int v1, int v2, int v3, int v4, int v5)
        {
            return new AttrEnhanceConfig
            {
                attrId = id,
                attrName = name,
                icon = icon,
                desc = null,
                values = new[] { v1, v2, v3, v4, v5 },
            };
        }
    }
}
