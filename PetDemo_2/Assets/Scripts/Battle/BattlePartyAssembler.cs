// SPEC §12.14.1 / §12.14.2：从 RunPartyRoster 组装我方 BattleUnitRuntime 并分配九宫格站位。
using System;
using System.Collections.Generic;
using PetDemo.Core;

namespace PetDemo.Battle
{
    public sealed class BattlePartyAssembler : IBattlePartyAssembler
    {
        private static readonly BattleGridPos RoleDefaultPos = new BattleGridPos(2, 2);

        private static readonly BattleGridPos[] Col2Slots =
        {
            new BattleGridPos(1, 2),
            new BattleGridPos(3, 2),
        };

        private static readonly BattleGridPos[] Col1Slots =
        {
            new BattleGridPos(1, 1),
            new BattleGridPos(2, 1),
            new BattleGridPos(3, 1),
        };

        private static readonly BattleGridPos[] Col3Slots =
        {
            new BattleGridPos(1, 3),
            new BattleGridPos(2, 3),
            new BattleGridPos(3, 3),
        };

        public List<BattleUnitRuntime> BuildAllies(RunPartyRoster roster)
        {
            var result = new List<BattleUnitRuntime>();
            if (roster?.members == null)
                return result;

            for (int i = 0; i < roster.members.Count; i++)
            {
                var entry = roster.members[i];
                if (entry == null)
                    continue;
                result.Add(CreateAllyRuntime(entry, i));
            }
            return result;
        }

        public void AssignAllyGridPositions(RunPartyRoster roster, List<BattleUnitRuntime> allies, int battleSeed)
        {
            if (allies == null || allies.Count == 0)
                return;

            int placementSeed = battleSeed ^ GridBattleSeedUtil.HashString(GridBattleSeedUtil.AllyPlacementSalt);
            var rng = GridBattleSeedUtil.CreateRng(placementSeed);

            var roleUnit = FindRole(allies);
            if (roleUnit != null)
                roleUnit.gridPos = RoleDefaultPos;

            var followers = new List<BattleUnitRuntime>();
            for (int i = 0; i < allies.Count; i++)
            {
                var unit = allies[i];
                if (unit == null || unit.kind == BattleUnitKind.Role)
                    continue;
                followers.Add(unit);
            }

            var available = new List<BattleGridPos>();
            var col2 = new List<BattleGridPos>(Col2Slots);
            var col1 = new List<BattleGridPos>(Col1Slots);
            var col3 = new List<BattleGridPos>(Col3Slots);
            GridBattleSeedUtil.Shuffle(col2, rng);
            GridBattleSeedUtil.Shuffle(col1, rng);
            GridBattleSeedUtil.Shuffle(col3, rng);
            available.AddRange(col2);
            available.AddRange(col1);
            available.AddRange(col3);

            int assignCount = Math.Min(followers.Count, available.Count);
            for (int i = 0; i < assignCount; i++)
                followers[i].gridPos = available[i];
        }

        private static BattleUnitRuntime FindRole(List<BattleUnitRuntime> allies)
        {
            for (int i = 0; i < allies.Count; i++)
            {
                if (allies[i]?.kind == BattleUnitKind.Role)
                    return allies[i];
            }
            return allies.Count > 0 ? allies[0] : null;
        }

        private static BattleUnitRuntime CreateAllyRuntime(RunAllyEntry entry, int index)
        {
            return new BattleUnitRuntime
            {
                instanceId = "ally_" + (string.IsNullOrEmpty(entry.rosterId) ? index.ToString() : entry.rosterId),
                rosterId = entry.rosterId,
                side = BattleSide.Ally,
                kind = entry.kind,
                sourceNpcId = entry.sourceNpcId,
                stats = RunPartyRosterFactory.CloneRoleStats(entry.stats),
                isBattleDead = false,
                displayName = entry.displayName,
                skeletonPrefab = entry.skeletonPrefab,
            };
        }
    }
}
