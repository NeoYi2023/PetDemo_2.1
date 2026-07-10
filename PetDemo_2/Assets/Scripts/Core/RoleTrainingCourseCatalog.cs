// SPEC §B.22 / §9.14.12 (v3.194)：主角训练课程表。
// 表 Resources/Configs/Farm/role_training_courses.csv；缺表/解析失败时回退 BuildDefault()。
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PetDemo.Core
{
    /// <summary>SPEC §B.22：属性增减条目（attrId 1–6）。</summary>
    [Serializable]
    public sealed class AttrDeltaEntry
    {
        public int attrId;
        public int delta;
    }

    /// <summary>SPEC §B.22：主角训练课程一行。</summary>
    public sealed class RoleTrainingCourseConfig
    {
        public string id;
        public string name;
        public string icon;
        public int[] filterTags = Array.Empty<int>();
        public int durationSec = 30;
        public AttrDeltaEntry[] attrGains = Array.Empty<AttrDeltaEntry>();
        public AttrDeltaEntry[] penalties = Array.Empty<AttrDeltaEntry>();
        public string rewardPool = string.Empty;
        public string description = string.Empty;
        public bool unlockedByDefault;
        public string unlockTip = string.Empty;
    }

    public static class RoleTrainingCourseCatalog
    {
        public const string CsvResourcePath = "Configs/Farm/role_training_courses";

        public static readonly string[] FilterAttrIconPaths =
        {
            "AirUI/SX_1_ZhiShang_A",
            "AirUI/SX_2_JiYi_A",
            "AirUI/SX_3_XiangXiang_B",
            "AirUI/SX_4_TiPo_A",
            "AirUI/SX_5_MeiLi_A",
            "AirUI/SX_6_QingShang_A",
        };

        /// <summary>SPEC §9.14.12：六项成长属性中文名（attrId 1–6）。</summary>
        public static readonly string[] GrowthAttrDisplayNames =
        {
            "智力",
            "记忆",
            "想象",
            "体魄",
            "魅力",
            "情商",
        };

        private static List<RoleTrainingCourseConfig> cache;
        private static Dictionary<string, RoleTrainingCourseConfig> byId;

        public static IReadOnlyList<RoleTrainingCourseConfig> GetAll()
        {
            EnsureLoaded();
            return cache;
        }

        public static bool TryGet(string id, out RoleTrainingCourseConfig config)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(id) && byId != null && byId.TryGetValue(id, out config) && config != null)
                return true;
            config = null;
            return false;
        }

        /// <summary>
        /// SPEC §9.14.12：OR 筛选。mask=0 显示全部；否则课程标签与选中 bit 有任一交集。
        /// bit0=智力(1) … bit5=情商(6)。
        /// </summary>
        public static bool MatchesFilter(RoleTrainingCourseConfig course, int filterMask)
        {
            if (course == null)
                return false;
            if (filterMask == 0)
                return true;
            if (course.filterTags == null || course.filterTags.Length == 0)
                return false;
            for (int i = 0; i < course.filterTags.Length; i++)
            {
                int tag = course.filterTags[i];
                if (tag < 1 || tag > 6)
                    continue;
                if ((filterMask & (1 << (tag - 1))) != 0)
                    return true;
            }
            return false;
        }

        public static string GetGrowthAttrIconPath(int attrId1Based)
        {
            if (attrId1Based < 1 || attrId1Based > 6)
                return null;
            return RoleLevelConfigCatalog.GrowthAttrIconPaths[attrId1Based - 1];
        }

        /// <summary>
        /// SPEC §9.14.12：将课程 <paramref name="attrGains"/> 格式化为「中文+数值」展示文案（如「智力+3，记忆+2」）。
        /// </summary>
        public static string FormatAttrGainsDisplay(AttrDeltaEntry[] attrGains)
        {
            if (attrGains == null || attrGains.Length == 0)
                return string.Empty;

            var sb = new StringBuilder();
            bool any = false;
            for (int i = 0; i < attrGains.Length; i++)
            {
                var entry = attrGains[i];
                if (entry == null || entry.attrId < 1 || entry.attrId > 6 || entry.delta == 0)
                    continue;

                if (any)
                    sb.Append('，');
                sb.Append(GrowthAttrDisplayNames[entry.attrId - 1]);
                sb.Append('+');
                sb.Append(Mathf.Abs(entry.delta));
                any = true;
            }

            return any ? sb.ToString() : string.Empty;
        }

        public static void ApplyDeltaToRole(RoleStats role, AttrDeltaEntry[] entries, bool isPenalty)
        {
            if (role == null || entries == null)
                return;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e == null || e.attrId < 1 || e.attrId > 6 || e.delta == 0)
                    continue;
                int delta = isPenalty ? -Mathf.Abs(e.delta) : Mathf.Abs(e.delta);
                switch (e.attrId)
                {
                    case 1: role.intelligence = ClampNonNeg(role.intelligence + delta); break;
                    case 2: role.memory = ClampNonNeg(role.memory + delta); break;
                    case 3: role.imagination = ClampNonNeg(role.imagination + delta); break;
                    case 4: role.physique = ClampNonNeg(role.physique + delta); break;
                    case 5: role.charm = ClampNonNeg(role.charm + delta); break;
                    case 6: role.emotionalIntelligence = ClampNonNeg(role.emotionalIntelligence + delta); break;
                }
            }
        }

        private static int ClampNonNeg(int v) => v < 0 ? 0 : v;

        private static void EnsureLoaded()
        {
            if (cache != null)
                return;
            cache = LoadFromCsv();
            byId = new Dictionary<string, RoleTrainingCourseConfig>(StringComparer.Ordinal);
            for (int i = 0; i < cache.Count; i++)
            {
                var c = cache[i];
                if (c != null && !string.IsNullOrEmpty(c.id) && !byId.ContainsKey(c.id))
                    byId[c.id] = c;
            }
        }

        public static List<RoleTrainingCourseConfig> LoadFromCsv()
        {
            var ta = Resources.Load<TextAsset>(CsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                Debug.LogWarning("[RoleTrainingCourseCatalog] 缺少课程表 Resources/" +
                    CsvResourcePath + "，使用内置默认数据。");
                return BuildDefault();
            }

            var table = CsvTable.Parse(ta.text);
            int idId = table.IndexOfHeader("id");
            int idName = table.IndexOfHeader("name");
            int idIcon = table.IndexOfHeader("icon");
            int idTags = table.IndexOfHeader("filterTags");
            int idDur = table.IndexOfHeader("durationSec");
            int idGains = table.IndexOfHeader("attrGains");
            int idPen = table.IndexOfHeader("penalties");
            int idReward = table.IndexOfHeader("rewardPool");
            int idDesc = table.IndexOfHeader("description");
            int idUnlock = table.IndexOfHeader("unlockedByDefault");
            int idTip = table.IndexOfHeader("unlockTip");

            if (idId < 0 || idName < 0 || idIcon < 0 || idTags < 0 || idDur < 0 || idUnlock < 0)
            {
                Debug.LogWarning("[RoleTrainingCourseCatalog] 课程表缺必需列，使用内置默认数据。");
                return BuildDefault();
            }

            var list = new List<RoleTrainingCourseConfig>();
            for (int r = 0; r < table.rows.Count; r++)
            {
                var row = table.rows[r];
                string id = (row.Get(idId) ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id))
                    continue;

                int duration = ParseIntOrZero(row.Get(idDur));
                if (duration < 1)
                    duration = 1;

                list.Add(new RoleTrainingCourseConfig
                {
                    id = id,
                    name = (row.Get(idName) ?? string.Empty).Trim(),
                    icon = (row.Get(idIcon) ?? string.Empty).Trim(),
                    filterTags = ParseIntPipeList(row.Get(idTags)),
                    durationSec = duration,
                    attrGains = ParseAttrDeltas(idGains >= 0 ? row.Get(idGains) : null),
                    penalties = ParseAttrDeltas(idPen >= 0 ? row.Get(idPen) : null),
                    rewardPool = idReward >= 0 ? (row.Get(idReward) ?? string.Empty).Trim() : string.Empty,
                    description = idDesc >= 0 ? (row.Get(idDesc) ?? string.Empty).Trim() : string.Empty,
                    unlockedByDefault = ParseIntOrZero(row.Get(idUnlock)) != 0,
                    unlockTip = idTip >= 0 ? (row.Get(idTip) ?? string.Empty).Trim() : string.Empty,
                });
            }

            if (list.Count == 0)
            {
                Debug.LogWarning("[RoleTrainingCourseCatalog] 课程表无有效行，使用内置默认数据。");
                return BuildDefault();
            }
            return list;
        }

        public static List<RoleTrainingCourseConfig> BuildDefault()
        {
            return new List<RoleTrainingCourseConfig>
            {
                Course("train_int_01", "晨读训练", "AirUI/SX_1_ZhiShang_B", new[] { 1 }, 30,
                    Gains(1, 3), null, "", "提升智力", true, ""),
                Course("train_mem_01", "记忆翻牌", "AirUI/SX_2_JiYi_B", new[] { 2 }, 30,
                    Gains(2, 3), null, "", "", true, ""),
                Course("train_img_01", "幻想速写", "AirUI/SX_3_XiangXiang_B", new[] { 3 }, 45,
                    Gains(3, 4), Gains(4, 1), "", "耗体力换想象", true, ""),
                Course("train_phy_01", "耐力跑", "AirUI/SX_4_TiPo_B", new[] { 4 }, 45,
                    Gains(4, 4), null, "", "", true, ""),
                Course("train_cha_01", "舞台彩排", "AirUI/SX_5_MeiLi_B", new[] { 5 }, 60,
                    Gains(5, 5), Gains(1, 1), "", "", false, "完成主线第1章解锁"),
                Course("train_eq_01", "倾诉练习", "AirUI/SX_6_QingShang_B", new[] { 6 }, 60,
                    Gains(6, 5), null, "", "", false, "亲密度达80解锁"),
                Course("train_mix_01", "综合脑力", "AirUI/SX_1_ZhiShang_B", new[] { 1, 2, 3 }, 90,
                    new[] { Gain(1, 2), Gain(2, 2), Gain(3, 2) }, Gains(4, 1),
                    "item:demo_reward", "多属性训练", true, ""),
            };
        }

        private static RoleTrainingCourseConfig Course(
            string id, string name, string icon, int[] tags, int duration,
            AttrDeltaEntry[] gains, AttrDeltaEntry[] pens, string reward, string desc,
            bool unlocked, string tip)
        {
            return new RoleTrainingCourseConfig
            {
                id = id,
                name = name,
                icon = icon,
                filterTags = tags ?? Array.Empty<int>(),
                durationSec = duration,
                attrGains = gains ?? Array.Empty<AttrDeltaEntry>(),
                penalties = pens ?? Array.Empty<AttrDeltaEntry>(),
                rewardPool = reward ?? string.Empty,
                description = desc ?? string.Empty,
                unlockedByDefault = unlocked,
                unlockTip = tip ?? string.Empty,
            };
        }

        private static AttrDeltaEntry Gain(int attrId, int delta) =>
            new AttrDeltaEntry { attrId = attrId, delta = delta };

        private static AttrDeltaEntry[] Gains(int attrId, int delta) =>
            new[] { Gain(attrId, delta) };

        private static int[] ParseIntPipeList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<int>();
            var parts = raw.Split('|');
            var list = new List<int>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i].Trim(), out int v) && v >= 1 && v <= 6)
                    list.Add(v);
            }
            return list.ToArray();
        }

        private static AttrDeltaEntry[] ParseAttrDeltas(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<AttrDeltaEntry>();
            var parts = raw.Split('|');
            var list = new List<AttrDeltaEntry>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                var seg = parts[i].Trim();
                if (string.IsNullOrEmpty(seg))
                    continue;
                int colon = seg.IndexOf(':');
                if (colon <= 0 || colon >= seg.Length - 1)
                    continue;
                if (!int.TryParse(seg.Substring(0, colon).Trim(), out int attrId))
                    continue;
                if (!int.TryParse(seg.Substring(colon + 1).Trim(), out int delta))
                    continue;
                if (attrId < 1 || attrId > 6 || delta == 0)
                    continue;
                list.Add(new AttrDeltaEntry { attrId = attrId, delta = delta });
            }
            return list.ToArray();
        }

        private static int ParseIntOrZero(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return 0;
            return int.TryParse(raw.Trim(), out int value) ? value : 0;
        }

#if UNITY_EDITOR
        public static void ClearCacheForEditor()
        {
            cache = null;
            byId = null;
        }
#endif
    }
}
