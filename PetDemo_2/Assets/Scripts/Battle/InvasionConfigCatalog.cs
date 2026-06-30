// SPEC §12.5 / §B.9：入侵单位静态配置目录。
// 运行时从 Resources/Configs/Battle/invasion_units.csv 装载，缺表或解析失败时回退到 BuildDefaultInvasionUnits()。
// 解析约定与 §B.2.1 / Core/CsvTable 一致：UTF-8、header、`#` 注释、空行跳过、字段 Trim()、非法行 Warning 跳过。
using System.Collections.Generic;
using PetDemo.Core;
using UnityEngine;

namespace PetDemo.Battle
{
    public static class InvasionConfigCatalog
    {
        public const string InvasionUnitsCsvResourcePath = "Configs/Battle/invasion_units";
        public const string InvasionVictoryRewardsCsvResourcePath = "Configs/Battle/invasion_victory_rewards";

        public const string PlayerUnitId = "player";
        public const string DefaultEnemyUnitId = "boss_langren";

        public static List<InvasionUnitConfig> LoadInvasionUnitsFromCsv()
        {
            var ta = Resources.Load<TextAsset>(InvasionUnitsCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                UnityEngine.Debug.LogWarning("[InvasionConfigCatalog] invasion_units.csv 未找到，回退到 BuildDefaultInvasionUnits。");
                return BuildDefaultInvasionUnits();
            }

            var table = CsvTable.Parse(ta.text);
            int idxId = table.IndexOfHeader("unitId");
            int idxName = table.IndexOfHeader("displayName");
            int idxAtk = table.IndexOfHeader("attack");
            int idxHp = table.IndexOfHeader("maxHp");
            if (idxId < 0 || idxName < 0 || idxAtk < 0 || idxHp < 0)
            {
                UnityEngine.Debug.LogWarning("[InvasionConfigCatalog] invasion_units.csv 缺少必需列，回退到 BuildDefaultInvasionUnits。");
                return BuildDefaultInvasionUnits();
            }

            var list = new List<InvasionUnitConfig>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                string id = row.Get(idxId);
                string name = row.Get(idxName);
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                {
                    UnityEngine.Debug.LogWarning($"[InvasionConfigCatalog] invasion_units.csv 第 {row.lineNumber} 行字段缺失，跳过。");
                    continue;
                }

                if (!int.TryParse(row.Get(idxAtk), out int atk) || atk < 0)
                {
                    UnityEngine.Debug.LogWarning($"[InvasionConfigCatalog] invasion_units.csv 第 {row.lineNumber} 行 attack 非法（须 >= 0），跳过。");
                    continue;
                }
                if (!int.TryParse(row.Get(idxHp), out int hp) || hp <= 0)
                {
                    UnityEngine.Debug.LogWarning($"[InvasionConfigCatalog] invasion_units.csv 第 {row.lineNumber} 行 maxHp 非法（须 > 0），跳过。");
                    continue;
                }

                list.Add(new InvasionUnitConfig
                {
                    unitId = id,
                    displayName = name,
                    attack = atk,
                    maxHp = hp,
                });
            }

            if (list.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[InvasionConfigCatalog] invasion_units.csv 全部行非法，回退到 BuildDefaultInvasionUnits。");
                return BuildDefaultInvasionUnits();
            }
            return list;
        }

        // SPEC §B.9.2：Demo 默认数据（与 CSV 保持等价）。
        public static List<InvasionUnitConfig> BuildDefaultInvasionUnits()
        {
            return new List<InvasionUnitConfig>
            {
                new InvasionUnitConfig { unitId = PlayerUnitId, displayName = "Role", attack = 12, maxHp = 80 },
                new InvasionUnitConfig { unitId = DefaultEnemyUnitId, displayName = "狼人入侵者", attack = 8, maxHp = 60 },
            };
        }

        public static InvasionUnitConfig FindById(List<InvasionUnitConfig> list, string unitId)
        {
            if (list == null || string.IsNullOrEmpty(unitId))
                return null;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].unitId == unitId)
                    return list[i];
            }
            return null;
        }

        // SPEC §12.8 / §B.10：战斗胜利掉落（Seed/Fertilizer/SeedPack）。
        public static List<InvasionRewardConfig> LoadVictoryRewardsFromCsv()
        {
            var ta = Resources.Load<TextAsset>(InvasionVictoryRewardsCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                UnityEngine.Debug.LogWarning("[InvasionConfigCatalog] invasion_victory_rewards.csv 未找到，回退到 BuildDefaultVictoryRewards。");
                return BuildDefaultVictoryRewards();
            }

            var table = CsvTable.Parse(ta.text);
            int idxKind = table.IndexOfHeader("kind");
            int idxId = table.IndexOfHeader("id");
            int idxCount = table.IndexOfHeader("count");
            if (idxKind < 0 || idxId < 0 || idxCount < 0)
            {
                UnityEngine.Debug.LogWarning("[InvasionConfigCatalog] invasion_victory_rewards.csv 缺少必需列，回退到 BuildDefaultVictoryRewards。");
                return BuildDefaultVictoryRewards();
            }

            var list = new List<InvasionRewardConfig>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                string kindRaw = row.Get(idxKind);
                string id = row.Get(idxId);
                if (string.IsNullOrEmpty(kindRaw) || string.IsNullOrEmpty(id))
                {
                    UnityEngine.Debug.LogWarning($"[InvasionConfigCatalog] invasion_victory_rewards.csv 第 {row.lineNumber} 行字段缺失，跳过。");
                    continue;
                }
                if (!System.Enum.TryParse<InvasionRewardKind>(kindRaw, false, out var kind))
                {
                    UnityEngine.Debug.LogWarning($"[InvasionConfigCatalog] invasion_victory_rewards.csv 第 {row.lineNumber} 行 kind='{kindRaw}' 非法（仅支持 Seed/Fertilizer/SeedPack），跳过。");
                    continue;
                }
                if (!int.TryParse(row.Get(idxCount), out int count) || count <= 0)
                {
                    UnityEngine.Debug.LogWarning($"[InvasionConfigCatalog] invasion_victory_rewards.csv 第 {row.lineNumber} 行 count 非法（须 > 0），跳过。");
                    continue;
                }

                list.Add(new InvasionRewardConfig
                {
                    kind = kind,
                    id = id,
                    count = count,
                });
            }

            if (list.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[InvasionConfigCatalog] invasion_victory_rewards.csv 全部行非法，回退到 BuildDefaultVictoryRewards。");
                return BuildDefaultVictoryRewards();
            }
            return list;
        }

        public static List<InvasionRewardConfig> BuildDefaultVictoryRewards()
        {
            return new List<InvasionRewardConfig>
            {
                new InvasionRewardConfig { kind = InvasionRewardKind.Seed, id = "fanqie", count = 2 },
                new InvasionRewardConfig { kind = InvasionRewardKind.Fertilizer, id = "demo", count = 1 },
                new InvasionRewardConfig { kind = InvasionRewardKind.SeedPack, id = "Common", count = 1 },
            };
        }
    }
}
