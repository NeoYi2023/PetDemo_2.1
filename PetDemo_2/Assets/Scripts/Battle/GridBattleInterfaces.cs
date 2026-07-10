// SPEC §12.14.8：多单位阵型战驱动接口。
using System.Collections.Generic;

namespace PetDemo.Battle
{
    public interface IBattlePartyAssembler
    {
        List<BattleUnitRuntime> BuildAllies(RunPartyRoster roster);
        void AssignAllyGridPositions(RunPartyRoster roster, List<BattleUnitRuntime> allies, int battleSeed);
    }

    public interface IGridEncounterBuilder
    {
        List<BattleUnitRuntime> BuildEnemies(string eventId, int battleSeed);
    }

    public interface IBattleTargetSelector
    {
        BattleUnitRuntime PickNormalAttackTarget(
            BattleUnitRuntime attacker,
            IReadOnlyList<BattleUnitRuntime> opponents,
            int roundIndex,
            int battleSeed);
    }

    public interface IGridBattleResolver
    {
        int ResolveNormalAttackDamage(BattleUnitRuntime attacker, BattleUnitRuntime defender);
    }

    public interface IGridBattleDriver
    {
        GridBattleSession GetSession();
        void BeginRound();
        BattleUnitRuntime GetCurrentActor();
        void ApplyNormalAttack(BattleUnitRuntime attacker, BattleUnitRuntime defender);
        bool AdvanceTurn();
        bool IsBattleFinished(out bool playerWon);
    }
}
