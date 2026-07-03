// SPEC §12.11.9 / §B.18：InvasionBattleModal_2 领悟/顿悟三选一技能池静态配置目录（技能表）。
// 技能表 Resources/Configs/Battle/skills.csv：skillId,skillName,quality,description,icon,effect,weight。
// 解析约定与 §B.2.1 / Core/CsvTable 一致：UTF-8、header、`#` 注释、空行跳过、字段 Trim()、非法行 Warning 跳过。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using UnityEngine;

namespace PetDemo.Battle
{
    /// <summary>SPEC §12.11.9 / §B.18：技能品质。</summary>
    public enum SkillQuality
    {
        Normal,     // 普通（领悟）
        Legendary,  // 传说（顿悟）
    }

    /// <summary>SPEC §12.11.9 / §B.18：技能表一行。</summary>
    public sealed class BattleSkillConfig
    {
        public string skillId;
        public string skillName;
        public SkillQuality quality;
        public string description;  // 支持富文本 <color> 局部变色
        public string iconName;     // → AirUI/SkillIcon/{iconName}
        public string effect;       // 本期占位，不具体设计
        public int weight;          // 加权随机权重（> 0）
    }

    public static class SkillConfigCatalog
    {
        public const string SkillsCsvResourcePath = "Configs/Battle/skills";
        public const string SkillIconResourcePrefix = "AirUI/SkillIcon/";
        public const int PickCount = 3;

        // ============================================================
        // 技能表（§B.18）
        // ============================================================
        public static List<BattleSkillConfig> LoadSkillsFromCsv()
        {
            var ta = Resources.Load<TextAsset>(SkillsCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                UnityEngine.Debug.LogWarning("[SkillConfigCatalog] skills.csv 未找到，回退到 BuildDefaultSkills。");
                return BuildDefaultSkills();
            }

            var table = CsvTable.Parse(ta.text);
            int idxId = table.IndexOfHeader("skillId");
            int idxName = table.IndexOfHeader("skillName");
            int idxQuality = table.IndexOfHeader("quality");
            int idxDesc = table.IndexOfHeader("description");
            int idxIcon = table.IndexOfHeader("icon");
            int idxEffect = table.IndexOfHeader("effect");
            int idxWeight = table.IndexOfHeader("weight");
            if (idxId < 0 || idxName < 0 || idxQuality < 0 || idxIcon < 0 || idxWeight < 0)
            {
                UnityEngine.Debug.LogWarning("[SkillConfigCatalog] skills.csv 缺少必需列（skillId/skillName/quality/icon/weight），回退到 BuildDefaultSkills。");
                return BuildDefaultSkills();
            }

            var list = new List<BattleSkillConfig>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                string id = row.Get(idxId);
                if (string.IsNullOrEmpty(id))
                {
                    UnityEngine.Debug.LogWarning($"[SkillConfigCatalog] skills.csv 第 {row.lineNumber} 行 skillId 缺失，跳过。");
                    continue;
                }
                string icon = row.Get(idxIcon);
                if (string.IsNullOrEmpty(icon))
                {
                    UnityEngine.Debug.LogWarning($"[SkillConfigCatalog] skills.csv 第 {row.lineNumber} 行 icon 缺失，跳过。");
                    continue;
                }
                if (!int.TryParse(row.Get(idxWeight), out int weight) || weight <= 0)
                {
                    UnityEngine.Debug.LogWarning($"[SkillConfigCatalog] skills.csv 第 {row.lineNumber} 行 weight 非法（须 > 0），跳过。");
                    continue;
                }

                list.Add(new BattleSkillConfig
                {
                    skillId = id,
                    skillName = row.Get(idxName) ?? id,
                    quality = ParseQuality(row.Get(idxQuality), row.lineNumber),
                    description = idxDesc >= 0 ? (row.Get(idxDesc) ?? string.Empty) : string.Empty,
                    iconName = icon,
                    effect = idxEffect >= 0 ? (row.Get(idxEffect) ?? string.Empty) : string.Empty,
                    weight = weight,
                });
            }

            if (list.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[SkillConfigCatalog] skills.csv 全部行非法，回退到 BuildDefaultSkills。");
                return BuildDefaultSkills();
            }
            return list;
        }

        // ============================================================
        // 三选一抽取（§12.11.9 / §B.18.3）
        // ============================================================
        /// <summary>
        /// 按品质过滤 + 排除已获得（excludeIds），按 weight 无重复加权随机抽取最多 PickCount 项；
        /// 不足则返回全部剩余，无剩余返回空列表。
        /// </summary>
        public static List<BattleSkillConfig> PickThreeByQuality(
            IList<BattleSkillConfig> all, SkillQuality quality, ICollection<string> excludeIds)
        {
            var pool = new List<BattleSkillConfig>();
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    var s = all[i];
                    if (s == null || s.weight <= 0 || s.quality != quality)
                        continue;
                    if (excludeIds != null && !string.IsNullOrEmpty(s.skillId) && excludeIds.Contains(s.skillId))
                        continue;
                    pool.Add(s);
                }
            }

            var result = new List<BattleSkillConfig>(PickCount);
            while (result.Count < PickCount && pool.Count > 0)
            {
                int totalWeight = 0;
                for (int i = 0; i < pool.Count; i++)
                    totalWeight += pool[i].weight;
                if (totalWeight <= 0)
                    break;

                int roll = UnityEngine.Random.Range(0, totalWeight);
                int chosen = pool.Count - 1;
                for (int i = 0; i < pool.Count; i++)
                {
                    roll -= pool[i].weight;
                    if (roll < 0)
                    {
                        chosen = i;
                        break;
                    }
                }
                result.Add(pool[chosen]);
                pool.RemoveAt(chosen);
            }
            return result;
        }

        // ============================================================
        // 解析辅助
        // ============================================================
        public static SkillQuality ParseQuality(string raw, int lineNumber = -1)
        {
            string s = raw != null ? raw.Trim() : string.Empty;
            switch (s)
            {
                case "普通":
                case "Normal":
                    return SkillQuality.Normal;
                case "传说":
                case "Legendary":
                    return SkillQuality.Legendary;
            }
            if (Enum.TryParse<SkillQuality>(s, true, out var parsed))
                return parsed;
            UnityEngine.Debug.LogWarning($"[SkillConfigCatalog] 技能表第 {lineNumber} 行 quality 非法（{raw}），回退为 普通。");
            return SkillQuality.Normal;
        }

        // ============================================================
        // Demo 默认数据（与 CSV 等价，§B.18.2）
        // ============================================================
        public static List<BattleSkillConfig> BuildDefaultSkills()
        {
            var list = new List<BattleSkillConfig>();
            AddDefault(list, "skill_n_1", "迅捷步伐", SkillQuality.Normal, "身法灵动，<color=#33CC33>速度提升</color>，先发制人。", "Card_30101", 10);
            AddDefault(list, "skill_n_2", "铁骨强身", SkillQuality.Normal, "筋骨如铁，<color=#33CC33>生命上限提升</color>，更加耐打。", "Card_30102", 10);
            AddDefault(list, "skill_n_3", "锐利爪击", SkillQuality.Normal, "爪锋凌厉，<color=#FF3B30>攻击提升</color>，撕裂防御。", "Card_30103", 10);
            AddDefault(list, "skill_n_4", "回复吐息", SkillQuality.Normal, "吐纳生息，每回合<color=#33CC33>恢复少量生命</color>。", "Card_30104", 8);
            AddDefault(list, "skill_n_5", "坚韧护盾", SkillQuality.Normal, "凝聚护盾，<color=#3399FF>减免一次伤害</color>。", "Card_30105", 8);
            AddDefault(list, "skill_n_6", "疾风连打", SkillQuality.Normal, "出手如风，<color=#FF3B30>连续攻击两次</color>。", "Card_30106", 6);
            AddDefault(list, "skill_l_1", "龙神之怒", SkillQuality.Legendary, "召唤龙神之力，<color=#FFB300>造成范围毁灭打击</color>。", "Card_30201", 5);
            AddDefault(list, "skill_l_2", "不灭金身", SkillQuality.Legendary, "化身不灭，<color=#FFB300>短时间内免疫致命伤害</color>。", "Card_30203", 5);
            AddDefault(list, "skill_l_3", "万象天引", SkillQuality.Legendary, "操纵万象，<color=#FFB300>牵引全场并大幅削弱敌人</color>。", "Card_30204", 4);
            AddDefault(list, "skill_l_4", "时空断裂", SkillQuality.Legendary, "撕裂时空，<color=#FFB300>令敌人停滞一回合</color>。", "Card_30205", 3);
            return list;
        }

        private static void AddDefault(List<BattleSkillConfig> list, string id, string name,
            SkillQuality quality, string desc, string icon, int weight)
        {
            list.Add(new BattleSkillConfig
            {
                skillId = id,
                skillName = name,
                quality = quality,
                description = desc,
                iconName = icon,
                effect = "tbd",
                weight = weight,
            });
        }

        public static string IconResourcePath(string iconName)
        {
            return string.IsNullOrEmpty(iconName) ? null : SkillIconResourcePrefix + iconName;
        }
    }
}
