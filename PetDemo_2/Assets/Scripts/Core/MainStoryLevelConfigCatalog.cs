// SPEC §9.8.8.7：主线关卡槽位布局与关卡定义 CSV 装载。
using System.Collections.Generic;
using PetDemo.Core;
using UnityEngine;

namespace PetDemo.Core
{
    public static class MainStoryLevelConfigCatalog
    {
        public const string SlotLayoutsCsvResourcePath = "Configs/MainStory/main_story_level_slots";
        public const string LevelsCsvResourcePath = "Configs/MainStory/main_story_levels";
        public const int SlotsPerPage = 15;

        public static List<MainStoryLevelSlotLayout> LoadSlotLayoutsFromCsv()
        {
            var ta = Resources.Load<TextAsset>(SlotLayoutsCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                UnityEngine.Debug.LogWarning(
                    "[MainStoryLevelConfigCatalog] main_story_level_slots.csv 未找到，回退到 BuildDefaultSlots。");
                return BuildDefaultSlots();
            }

            var table = CsvTable.Parse(ta.text);
            int idxSlot = table.IndexOfHeader("slotIndex");
            int idxX = table.IndexOfHeader("posX");
            int idxY = table.IndexOfHeader("posY");
            int idxW = table.IndexOfHeader("width");
            int idxH = table.IndexOfHeader("height");
            if (idxSlot < 0 || idxX < 0 || idxY < 0)
            {
                UnityEngine.Debug.LogWarning(
                    "[MainStoryLevelConfigCatalog] main_story_level_slots.csv 缺少必需列，回退到 BuildDefaultSlots。");
                return BuildDefaultSlots();
            }

            var list = new List<MainStoryLevelSlotLayout>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                if (!int.TryParse(row.Get(idxSlot), out int slotIndex) || slotIndex < 0 || slotIndex >= SlotsPerPage)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[MainStoryLevelConfigCatalog] main_story_level_slots.csv 第 {row.lineNumber} 行 slotIndex 非法，跳过。");
                    continue;
                }

                if (!float.TryParse(row.Get(idxX), out float posX) ||
                    !float.TryParse(row.Get(idxY), out float posY))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[MainStoryLevelConfigCatalog] main_story_level_slots.csv 第 {row.lineNumber} 行坐标非法，跳过。");
                    continue;
                }

                float width = 120f;
                float height = 120f;
                if (idxW >= 0 && !string.IsNullOrEmpty(row.Get(idxW)))
                    float.TryParse(row.Get(idxW), out width);
                if (idxH >= 0 && !string.IsNullOrEmpty(row.Get(idxH)))
                    float.TryParse(row.Get(idxH), out height);

                list.Add(new MainStoryLevelSlotLayout
                {
                    slotIndex = slotIndex,
                    posX = posX,
                    posY = posY,
                    width = width > 0f ? width : 120f,
                    height = height > 0f ? height : 120f,
                });
            }

            if (list.Count == 0)
            {
                UnityEngine.Debug.LogWarning(
                    "[MainStoryLevelConfigCatalog] main_story_level_slots.csv 全部行非法，回退到 BuildDefaultSlots。");
                return BuildDefaultSlots();
            }

            list.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
            return list;
        }

        public static List<MainStoryLevelConfig> LoadLevelConfigsFromCsv()
        {
            var ta = Resources.Load<TextAsset>(LevelsCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                UnityEngine.Debug.LogWarning(
                    "[MainStoryLevelConfigCatalog] main_story_levels.csv 未找到，回退到 BuildDefaultLevels。");
                return BuildDefaultLevels();
            }

            var table = CsvTable.Parse(ta.text);
            int idxLevel = table.IndexOfHeader("levelNumber");
            int idxBoss = table.IndexOfHeader("isBoss");
            int idxName = table.IndexOfHeader("displayName");
            int idxInfo = table.IndexOfHeader("infoSpritePath");
            int idxRewards = table.IndexOfHeader("victoryRewards");
            if (idxLevel < 0 || idxBoss < 0 || idxName < 0)
            {
                UnityEngine.Debug.LogWarning(
                    "[MainStoryLevelConfigCatalog] main_story_levels.csv 缺少必需列，回退到 BuildDefaultLevels。");
                return BuildDefaultLevels();
            }

            var list = new List<MainStoryLevelConfig>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                if (!int.TryParse(row.Get(idxLevel), out int levelNumber) || levelNumber < 1)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[MainStoryLevelConfigCatalog] main_story_levels.csv 第 {row.lineNumber} 行 levelNumber 非法，跳过。");
                    continue;
                }

                string bossRaw = row.Get(idxBoss);
                bool isBoss = bossRaw == "1" || string.Equals(bossRaw, "true", System.StringComparison.OrdinalIgnoreCase);
                string displayName = row.Get(idxName);
                if (string.IsNullOrEmpty(displayName))
                    displayName = "第" + levelNumber + "关";

                string rewardsRaw = idxRewards >= 0 ? row.Get(idxRewards) : null;
                list.Add(new MainStoryLevelConfig
                {
                    levelNumber = levelNumber,
                    isBoss = isBoss,
                    displayName = displayName,
                    infoSpritePath = idxInfo >= 0 ? row.Get(idxInfo) : null,
                    victoryRewards = FixedRewardListParser.Parse(rewardsRaw),
                });
            }

            if (list.Count == 0)
            {
                UnityEngine.Debug.LogWarning(
                    "[MainStoryLevelConfigCatalog] main_story_levels.csv 全部行非法，回退到 BuildDefaultLevels。");
                return BuildDefaultLevels();
            }

            list.Sort((a, b) => a.levelNumber.CompareTo(b.levelNumber));
            return list;
        }

        public static MainStoryLevelState ResolveState(int levelNumber, int highestCleared)
        {
            if (levelNumber <= highestCleared)
                return MainStoryLevelState.Cleared;
            if (levelNumber == highestCleared + 1)
                return MainStoryLevelState.Available;
            return MainStoryLevelState.Locked;
        }

        public static int GetPageIndexForLevel(int levelNumber)
        {
            if (levelNumber < 1)
                return 0;
            return (levelNumber - 1) / SlotsPerPage;
        }

        public static int LevelNumberForSlot(int pageIndex, int slotIndex)
        {
            return pageIndex * SlotsPerPage + slotIndex + 1;
        }

        public static MainStoryLevelConfig FindLevelByNumber(List<MainStoryLevelConfig> levels, int levelNumber)
        {
            if (levels == null || levelNumber < 1)
                return null;
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] != null && levels[i].levelNumber == levelNumber)
                    return levels[i];
            }
            return null;
        }

        public static MainStoryLevelSlotLayout FindSlotByIndex(List<MainStoryLevelSlotLayout> slots, int slotIndex)
        {
            if (slots == null)
                return null;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].slotIndex == slotIndex)
                    return slots[i];
            }
            return null;
        }

        public static List<InvasionRewardConfig> GetVictoryRewardsForLevel(
            List<MainStoryLevelConfig> levels,
            int levelNumber)
        {
            var level = FindLevelByNumber(levels, levelNumber);
            if (level?.victoryRewards == null || level.victoryRewards.Count == 0)
                return new List<InvasionRewardConfig>();
            return level.victoryRewards;
        }

        public static int ResolveMainStoryBattleLevelNumber(int highestCleared, List<MainStoryLevelConfig> levels)
        {
            int level = highestCleared + 1;
            int maxLevel = GetMaxLevelNumber(levels);
            if (maxLevel > 0 && level > maxLevel)
                level = maxLevel;
            if (level < 1)
                level = 1;
            return level;
        }

        public static int GetMaxLevelNumber(List<MainStoryLevelConfig> levels)
        {
            int max = 0;
            if (levels == null)
                return max;
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] != null && levels[i].levelNumber > max)
                    max = levels[i].levelNumber;
            }
            return max;
        }

        public static List<MainStoryLevelSlotLayout> BuildDefaultSlots()
        {
            var list = new List<MainStoryLevelSlotLayout>(SlotsPerPage);
            // 3 列 × 5 行 Demo 布局，相对面板中心锚点。
            float[] xs = { -320f, 0f, 320f };
            float startY = 280f;
            float rowStep = -140f;
            int slot = 0;
            for (int row = 0; row < 5 && slot < SlotsPerPage; row++)
            {
                for (int col = 0; col < 3 && slot < SlotsPerPage; col++)
                {
                    list.Add(new MainStoryLevelSlotLayout
                    {
                        slotIndex = slot,
                        posX = xs[col],
                        posY = startY + row * rowStep,
                        width = 120f,
                        height = 120f,
                    });
                    slot++;
                }
            }
            return list;
        }

        public static List<MainStoryLevelConfig> BuildDefaultLevels()
        {
            var list = new List<MainStoryLevelConfig>(20);
            for (int i = 1; i <= 20; i++)
            {
                bool isBoss = i == 5 || i == 10 || i == 15 || i == 20;
                var config = new MainStoryLevelConfig
                {
                    levelNumber = i,
                    isBoss = isBoss,
                    displayName = isBoss ? $"第{i}关·BOSS" : $"第{i}关",
                    infoSpritePath = null,
                };
                if (i == 1)
                    config.victoryRewards = FixedRewardListParser.Parse("Seed:fanqie:2");
                list.Add(config);
            }
            return list;
        }
    }
}
