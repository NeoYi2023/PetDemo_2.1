// SPEC §B.21 / §9.14.11 (v3.188)：角色等级成长表。
// 表 Resources/Configs/Farm/role_levels.csv；缺表/解析失败时回退 BuildDefault()。
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.Core
{
    /// <summary>SPEC §B.21：角色等级成长表一行。</summary>
    public sealed class RoleLevelConfig
    {
        public int level;
        public int intelligence;
        public int memory;
        public int imagination;
        public int physique;
        public int charm;
        public int emotionalIntelligence;
        public int expToNextLevel;
        public int baseHp;
        public int baseAtk;
        public int baseAtkSpeed;
    }

    public static class RoleLevelConfigCatalog
    {
        public const string RoleLevelsCsvResourcePath = "Configs/Farm/role_levels";

        public static readonly string[] GrowthAttrIconPaths =
        {
            "AirUI/SX_1_ZhiShang_B",
            "AirUI/SX_2_JiYi_B",
            "AirUI/SX_3_XiangXiang_B",
            "AirUI/SX_4_TiPo_B",
            "AirUI/SX_5_MeiLi_B",
            "AirUI/SX_6_QingShang_B",
        };

        private static Dictionary<int, RoleLevelConfig> cache;

        public static Dictionary<int, RoleLevelConfig> GetAll()
        {
            if (cache != null)
                return cache;
            cache = LoadFromCsv();
            return cache;
        }

        public static bool TryGet(int level, out RoleLevelConfig config)
        {
            var all = GetAll();
            if (all != null && all.TryGetValue(level, out config) && config != null)
                return true;
            config = null;
            return false;
        }

        /// <summary>
        /// 按 <paramref name="role"/>.level 写入六项成长属性、expToNextLevel、maxHp/atk/agility。
        /// 新建时若 currentHp 小于等于 0 则设为 maxHp；应用后若 currentHp &gt; maxHp 则 clamp。
        /// </summary>
        public static void ApplyToRole(RoleStats role)
        {
            if (role == null)
                return;

            int level = role.level <= 0 ? 1 : role.level;
            role.level = level;

            if (!TryGet(level, out var cfg) || cfg == null)
            {
                if (!TryGet(1, out cfg) || cfg == null)
                    cfg = BuildDefaultLevel1();
            }

            role.intelligence = cfg.intelligence;
            role.memory = cfg.memory;
            role.imagination = cfg.imagination;
            role.physique = cfg.physique;
            role.charm = cfg.charm;
            role.emotionalIntelligence = cfg.emotionalIntelligence;
            // SPEC §B.23 (v3.208)：expToNextLevel 优先读经验表，缺则回退本表列。
            int expFromExpTable = RoleExpConfigCatalog.GetExpForLevel(level);
            role.expToNextLevel = expFromExpTable > 0
                ? expFromExpTable
                : Mathf.Max(1, cfg.expToNextLevel);
            role.maxHp = Mathf.Max(1, cfg.baseHp);
            role.atk = Mathf.Max(0, cfg.baseAtk);
            role.agility = Mathf.Max(0, cfg.baseAtkSpeed);

            if (role.currentHp <= 0)
                role.currentHp = role.maxHp;
            else if (role.currentHp > role.maxHp)
                role.currentHp = role.maxHp;
        }

        public static Dictionary<int, RoleLevelConfig> LoadFromCsv()
        {
            var ta = Resources.Load<TextAsset>(RoleLevelsCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                Debug.LogWarning("[RoleLevelConfigCatalog] 缺少等级表 Resources/" +
                    RoleLevelsCsvResourcePath + "，使用内置默认数据。");
                return BuildDefault();
            }

            var table = CsvTable.Parse(ta.text);
            int idLevel = table.IndexOfHeader("level");
            int idInt = table.IndexOfHeader("intelligence");
            int idMem = table.IndexOfHeader("memory");
            int idImg = table.IndexOfHeader("imagination");
            int idPhy = table.IndexOfHeader("physique");
            int idCha = table.IndexOfHeader("charm");
            int idEq = table.IndexOfHeader("emotionalIntelligence");
            int idExp = table.IndexOfHeader("expToNextLevel");
            int idHp = table.IndexOfHeader("baseHp");
            int idAtk = table.IndexOfHeader("baseAtk");
            int idSpd = table.IndexOfHeader("baseAtkSpeed");

            if (idLevel < 0 || idInt < 0 || idMem < 0 || idImg < 0 || idPhy < 0
                || idCha < 0 || idEq < 0 || idExp < 0 || idHp < 0 || idAtk < 0 || idSpd < 0)
            {
                Debug.LogWarning("[RoleLevelConfigCatalog] 等级表缺必需列，使用内置默认数据。");
                return BuildDefault();
            }

            var map = new Dictionary<int, RoleLevelConfig>();
            for (int r = 0; r < table.rows.Count; r++)
            {
                var row = table.rows[r];
                if (!TryParsePositiveInt(row.Get(idLevel), out int level))
                {
                    Debug.LogWarning($"[RoleLevelConfigCatalog] 第 {row.lineNumber} 行 level 非法，跳过。");
                    continue;
                }

                var cfg = new RoleLevelConfig
                {
                    level = level,
                    intelligence = ParseIntOrZero(row.Get(idInt)),
                    memory = ParseIntOrZero(row.Get(idMem)),
                    imagination = ParseIntOrZero(row.Get(idImg)),
                    physique = ParseIntOrZero(row.Get(idPhy)),
                    charm = ParseIntOrZero(row.Get(idCha)),
                    emotionalIntelligence = ParseIntOrZero(row.Get(idEq)),
                    expToNextLevel = Mathf.Max(1, ParseIntOrZero(row.Get(idExp))),
                    baseHp = Mathf.Max(1, ParseIntOrZero(row.Get(idHp))),
                    baseAtk = ParseIntOrZero(row.Get(idAtk)),
                    baseAtkSpeed = ParseIntOrZero(row.Get(idSpd)),
                };
                map[level] = cfg;
            }

            if (map.Count == 0)
            {
                Debug.LogWarning("[RoleLevelConfigCatalog] 等级表无有效行，使用内置默认数据。");
                return BuildDefault();
            }
            return map;
        }

        public static Dictionary<int, RoleLevelConfig> BuildDefault()
        {
            var map = new Dictionary<int, RoleLevelConfig>();
            void Add(int lv, int growth, int exp, int hp, int atk, int spd)
            {
                map[lv] = new RoleLevelConfig
                {
                    level = lv,
                    intelligence = growth,
                    memory = growth,
                    imagination = growth,
                    physique = growth,
                    charm = growth,
                    emotionalIntelligence = growth,
                    expToNextLevel = exp,
                    baseHp = hp,
                    baseAtk = atk,
                    baseAtkSpeed = spd,
                };
            }

            Add(1, 10, 100, 25, 12, 2);
            Add(2, 12, 150, 30, 14, 3);
            Add(3, 14, 200, 36, 16, 3);
            Add(4, 16, 260, 42, 18, 4);
            Add(5, 18, 330, 50, 20, 4);
            Add(6, 20, 410, 58, 23, 5);
            Add(7, 22, 500, 66, 26, 5);
            Add(8, 24, 600, 75, 29, 6);
            Add(9, 26, 720, 85, 32, 6);
            Add(10, 28, 850, 95, 36, 7);
            return map;
        }

        private static RoleLevelConfig BuildDefaultLevel1()
        {
            return new RoleLevelConfig
            {
                level = 1,
                intelligence = 10,
                memory = 10,
                imagination = 10,
                physique = 10,
                charm = 10,
                emotionalIntelligence = 10,
                expToNextLevel = 100,
                baseHp = 25,
                baseAtk = 12,
                baseAtkSpeed = 2,
            };
        }

        private static bool TryParsePositiveInt(string raw, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            return int.TryParse(raw.Trim(), out value) && value >= 1;
        }

        private static int ParseIntOrZero(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return 0;
            return int.TryParse(raw.Trim(), out int value) ? value : 0;
        }

#if UNITY_EDITOR
        /// <summary>编辑器测试用：清空缓存以便重载 CSV。</summary>
        public static void ClearCacheForEditor()
        {
            cache = null;
        }
#endif
    }
}
