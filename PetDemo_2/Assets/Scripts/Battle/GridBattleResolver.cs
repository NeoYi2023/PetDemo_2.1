// SPEC §12.14.3 / §4.2：普攻伤害结算。
namespace PetDemo.Battle
{
    public sealed class GridBattleResolver : IGridBattleResolver
    {
        public int ResolveNormalAttackDamage(BattleUnitRuntime attacker, BattleUnitRuntime defender)
        {
            if (attacker?.stats == null || defender?.stats == null)
                return 1;
            int atk = attacker.stats.atk;
            int def = defender.stats.def;
            int damage = atk - def;
            return damage < 1 ? 1 : damage;
        }
    }
}
