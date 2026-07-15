// SPEC §12.14.7 (v3.220)：遭遇构建——按 pendingEventId 刷怪（全队九宫格战）。
// evt_fight_small_1 → 1 只 enemy_small；evt_fight_small_2 → 2~3 只；evt_fight_boss → 1 只 boss。
using System;
using System.Collections.Generic;
using PetDemo.Core;

namespace PetDemo.Battle
{
    public sealed class GridEncounterBuilder : IGridEncounterBuilder
    {
        public const string EventFightSmall1 = "evt_fight_small_1";
        public const string EventFightSmall2 = "evt_fight_small_2";
        public const string EventFightBoss = "evt_fight_boss";
        public const int DefaultEnemyAgility = 2;
        public const int Small2EnemyCountMin = 2;
        public const int Small2EnemyCountMaxExclusive = 4;

        private readonly Func<string, InvasionUnitConfig> resolveUnit;

        public GridEncounterBuilder(Func<string, InvasionUnitConfig> resolveUnit)
        {
            this.resolveUnit = resolveUnit;
        }

        /// <summary>Modal_2 嵌入战斗是否走九宫格全队战（v3.220：三类战斗事件均是）。</summary>
        public static bool IsGridPartyBattleEvent(string eventId)
        {
            return string.Equals(eventId, EventFightSmall1, StringComparison.Ordinal)
                || string.Equals(eventId, EventFightSmall2, StringComparison.Ordinal)
                || string.Equals(eventId, EventFightBoss, StringComparison.Ordinal);
        }

        public List<BattleUnitRuntime> BuildEnemies(string eventId, int battleSeed)
        {
            if (string.Equals(eventId, EventFightSmall1, StringComparison.Ordinal))
                return SpawnEnemies(InvasionConfigCatalog.SmallEnemyUnitId, 1, 1, battleSeed);

            if (string.Equals(eventId, EventFightSmall2, StringComparison.Ordinal))
                return SpawnEnemies(
                    InvasionConfigCatalog.SmallEnemyUnitId,
                    Small2EnemyCountMin,
                    Small2EnemyCountMaxExclusive - 1,
                    battleSeed);

            if (string.Equals(eventId, EventFightBoss, StringComparison.Ordinal))
                return SpawnEnemies(InvasionConfigCatalog.BossEnemyUnitId, 1, 1, battleSeed);

            return new List<BattleUnitRuntime>();
        }

        /// <summary>
        /// 刷怪：count 在 [minCount, maxCount] 闭区间内随机（min==max 则固定）。
        /// </summary>
        private List<BattleUnitRuntime> SpawnEnemies(
            string unitId, int minCount, int maxCount, int battleSeed)
        {
            var result = new List<BattleUnitRuntime>();
            var template = resolveUnit?.Invoke(unitId);
            if (template == null)
                return result;

            int enemySeed = battleSeed ^ GridBattleSeedUtil.HashString(GridBattleSeedUtil.EnemyPlacementSalt);
            var rng = GridBattleSeedUtil.CreateRng(enemySeed);

            int lo = Math.Max(1, Math.Min(minCount, maxCount));
            int hi = Math.Max(minCount, maxCount);
            int count = lo == hi ? lo : lo + rng.NextInt(hi - lo + 1);

            var slots = BuildAllEnemySlots();
            GridBattleSeedUtil.Shuffle(slots, rng);
            int spawnCount = Math.Min(count, slots.Count);

            for (int i = 0; i < spawnCount; i++)
                result.Add(CreateEnemyRuntime(template, i, slots[i]));

            return result;
        }

        private static List<BattleGridPos> BuildAllEnemySlots()
        {
            var slots = new List<BattleGridPos>(9);
            for (int row = 1; row <= 3; row++)
            {
                for (int col = 1; col <= 3; col++)
                    slots.Add(new BattleGridPos(row, col));
            }
            return slots;
        }

        private static BattleUnitRuntime CreateEnemyRuntime(
            InvasionUnitConfig template, int index, BattleGridPos gridPos)
        {
            int maxHp = Math.Max(1, template.maxHp);
            int atk = Math.Max(0, template.attack);
            return new BattleUnitRuntime
            {
                instanceId = "enemy_" + index,
                rosterId = null,
                side = BattleSide.Enemy,
                kind = BattleUnitKind.Role,
                sourceNpcId = null,
                gridPos = gridPos,
                stats = new RoleStats
                {
                    displayName = template.displayName,
                    atk = atk,
                    def = 0,
                    maxHp = maxHp,
                    currentHp = maxHp,
                    agility = DefaultEnemyAgility,
                },
                isBattleDead = false,
                displayName = template.displayName,
                skeletonPrefab = template.skeletonPrefab,
                displayScale = template.displayScale <= 0f ? 1f : template.displayScale,
            };
        }
    }
}
