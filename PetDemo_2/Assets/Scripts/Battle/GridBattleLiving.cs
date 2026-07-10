// SPEC §12.14.6：存活单位判定。
using System.Collections.Generic;

namespace PetDemo.Battle
{
    public static class GridBattleLiving
    {
        public static bool IsLiving(BattleUnitRuntime unit)
        {
            return unit != null
                && unit.stats != null
                && unit.stats.currentHp > 0
                && !unit.isBattleDead;
        }

        public static bool AnyLiving(IReadOnlyList<BattleUnitRuntime> units)
        {
            if (units == null)
                return false;
            for (int i = 0; i < units.Count; i++)
            {
                if (IsLiving(units[i]))
                    return true;
            }
            return false;
        }

        public static bool AllDead(IReadOnlyList<BattleUnitRuntime> units)
        {
            return !AnyLiving(units);
        }
    }
}
