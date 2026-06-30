// SPEC §9.11.8 / §B.14：灭虫小游戏生成配置表。
using System.Collections.Generic;
using PetDemo.Core;
using UnityEngine;

namespace PetDemo.Farm
{
    public static class PestControlConfigCatalog
    {
        public const string SpawnCsvResourcePath = "Configs/Farm/pest_control_spawn";

        public static List<PestControlSpawnEntry> LoadSpawnEntriesFromCsv()
        {
            var ta = Resources.Load<TextAsset>(SpawnCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                Debug.LogWarning("[PestControlConfigCatalog] pest_control_spawn.csv 未找到，回退到 BuildDefaultSpawnEntries。");
                return BuildDefaultSpawnEntries();
            }

            var table = CsvTable.Parse(ta.text);
            int idxTurn = table.IndexOfHeader("turn");
            int idxType = table.IndexOfHeader("entityType");
            int idxValue = table.IndexOfHeader("value");
            int idxCount = table.IndexOfHeader("spawnCount");
            if (idxTurn < 0 || idxType < 0 || idxValue < 0 || idxCount < 0)
            {
                Debug.LogWarning("[PestControlConfigCatalog] pest_control_spawn.csv 缺少必需列，回退到 BuildDefaultSpawnEntries。");
                return BuildDefaultSpawnEntries();
            }

            var list = new List<PestControlSpawnEntry>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                if (!int.TryParse(row.Get(idxTurn), out int turn) || turn < 0)
                {
                    Debug.LogWarning($"[PestControlConfigCatalog] 第 {row.lineNumber} 行 turn 非法，跳过。");
                    continue;
                }

                if (!TryParseEntityType(row.Get(idxType), out var entityType))
                {
                    Debug.LogWarning($"[PestControlConfigCatalog] 第 {row.lineNumber} 行 entityType 非法，跳过。");
                    continue;
                }

                if (!int.TryParse(row.Get(idxValue), out int value) || !IsPowerOfTwoAtLeastTwo(value))
                {
                    Debug.LogWarning($"[PestControlConfigCatalog] 第 {row.lineNumber} 行 value 非法（须为 2 的幂且 >= 2），跳过。");
                    continue;
                }

                if (!int.TryParse(row.Get(idxCount), out int spawnCount) || spawnCount < 0)
                {
                    Debug.LogWarning($"[PestControlConfigCatalog] 第 {row.lineNumber} 行 spawnCount 非法，跳过。");
                    continue;
                }

                list.Add(new PestControlSpawnEntry
                {
                    turn = turn,
                    type = entityType,
                    value = value,
                    spawnCount = spawnCount,
                });
            }

            if (list.Count == 0)
            {
                Debug.LogWarning("[PestControlConfigCatalog] pest_control_spawn.csv 全部行非法，回退到 BuildDefaultSpawnEntries。");
                return BuildDefaultSpawnEntries();
            }

            return list;
        }

        public static List<PestControlSpawnEntry> BuildDefaultSpawnEntries()
        {
            var list = new List<PestControlSpawnEntry>(40);
            list.Add(new PestControlSpawnEntry { turn = 0, type = PestControlEntityType.Werewolf, value = 2, spawnCount = 1 });
            list.Add(new PestControlSpawnEntry { turn = 0, type = PestControlEntityType.Bug, value = 2, spawnCount = 2 });
            for (int turn = 1; turn <= 29; turn++)
            {
                list.Add(new PestControlSpawnEntry { turn = turn, type = PestControlEntityType.Bug, value = 2, spawnCount = 1 });
            }

            for (int turn = 30; turn <= 35; turn++)
            {
                int value = turn % 2 == 0 ? 2 : 4;
                list.Add(new PestControlSpawnEntry { turn = turn, type = PestControlEntityType.Bug, value = value, spawnCount = 1 });
            }

            return list;
        }

        public static bool IsPowerOfTwoAtLeastTwo(int value)
        {
            return value >= 2 && (value & (value - 1)) == 0;
        }

        private static bool TryParseEntityType(string raw, out PestControlEntityType entityType)
        {
            entityType = PestControlEntityType.Bug;
            if (string.IsNullOrEmpty(raw))
                return false;

            string trimmed = raw.Trim();
            if (trimmed == "Bug" || trimmed == "bug" || trimmed == "虫子")
            {
                entityType = PestControlEntityType.Bug;
                return true;
            }

            if (trimmed == "Werewolf" || trimmed == "werewolf" || trimmed == "狼人")
            {
                entityType = PestControlEntityType.Werewolf;
                return true;
            }

            return false;
        }
    }
}
