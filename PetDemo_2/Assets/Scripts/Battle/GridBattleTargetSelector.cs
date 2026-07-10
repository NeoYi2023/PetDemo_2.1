// SPEC §12.14.4：普攻目标选择（列优先、同行优先）。
using System;
using System.Collections.Generic;

namespace PetDemo.Battle
{
    public sealed class GridBattleTargetSelector : IBattleTargetSelector
    {
        public BattleUnitRuntime PickNormalAttackTarget(
            BattleUnitRuntime attacker,
            IReadOnlyList<BattleUnitRuntime> opponents,
            int roundIndex,
            int battleSeed)
        {
            if (attacker == null || opponents == null || opponents.Count == 0)
                return null;

            var living = new List<BattleUnitRuntime>();
            for (int i = 0; i < opponents.Count; i++)
            {
                if (GridBattleLiving.IsLiving(opponents[i]))
                    living.Add(opponents[i]);
            }
            if (living.Count == 0)
                return null;

            int bestCol = FindBestColumn(living);
            var colCandidates = new List<BattleUnitRuntime>();
            for (int i = 0; i < living.Count; i++)
            {
                if (living[i].gridPos.col == bestCol)
                    colCandidates.Add(living[i]);
            }

            int bestRowRank = int.MaxValue;
            var rowCandidates = new List<BattleUnitRuntime>();
            for (int i = 0; i < colCandidates.Count; i++)
            {
                int rank = RowDistanceRank(attacker.gridPos.row, colCandidates[i].gridPos.row);
                if (rank < bestRowRank)
                {
                    bestRowRank = rank;
                    rowCandidates.Clear();
                    rowCandidates.Add(colCandidates[i]);
                }
                else if (rank == bestRowRank)
                {
                    rowCandidates.Add(colCandidates[i]);
                }
            }

            if (rowCandidates.Count == 1)
                return rowCandidates[0];

            int tieSeed = GridBattleSeedUtil.MixSeed(
                battleSeed,
                roundIndex,
                GridBattleSeedUtil.HashString(attacker.instanceId),
                GridBattleSeedUtil.TargetTieBreakSalt);
            var rng = GridBattleSeedUtil.CreateRng(tieSeed);
            return rowCandidates[rng.NextInt(rowCandidates.Count)];
        }

        private static int FindBestColumn(List<BattleUnitRuntime> living)
        {
            int best = 3;
            for (int i = 0; i < living.Count; i++)
            {
                int col = living[i].gridPos.col;
                if (col < best)
                    best = col;
            }
            return best;
        }

        private static int RowDistanceRank(int attackerRow, int targetRow)
        {
            int delta = Math.Abs(attackerRow - targetRow);
            if (delta == 0)
                return 0;
            if (delta == 1)
                return 1;
            return 2;
        }
    }
}
