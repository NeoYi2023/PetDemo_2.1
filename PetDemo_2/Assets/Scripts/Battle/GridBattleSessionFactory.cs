// SPEC §12.14.8：组装 GridBattleSession 并 headless 跑完整场战斗。
using System;
using System.Collections.Generic;
using PetDemo.Core;

namespace PetDemo.Battle
{
    public static class GridBattleSessionFactory
    {
        public static GridBattleSession Create(
            RunPartyRoster roster,
            string eventId,
            int battleSeed,
            Func<string, InvasionUnitConfig> resolveUnit,
            IBattlePartyAssembler assembler = null,
            IGridEncounterBuilder encounterBuilder = null)
        {
            var session = new GridBattleSession
            {
                battleSeed = battleSeed,
                pendingEventId = eventId,
                roundIndex = 1,
            };

            var partyAssembler = assembler ?? new BattlePartyAssembler();
            var allies = partyAssembler.BuildAllies(roster);
            partyAssembler.AssignAllyGridPositions(roster, allies, battleSeed);
            session.allies.AddRange(allies);

            var builder = encounterBuilder
                ?? new GridEncounterBuilder(resolveUnit);
            session.enemies.AddRange(builder.BuildEnemies(eventId, battleSeed));
            return session;
        }
    }

    /// <summary>无 UI 的整场战斗模拟（阶段 3 验收）。</summary>
    public static class GridBattleHeadlessRunner
    {
        public const int DefaultMaxTurns = 10000;

        public static bool RunToCompletion(
            GridBattleSession session,
            out bool playerWon,
            IBattleTargetSelector targetSelector = null,
            IGridBattleDriver driver = null,
            int maxTurns = DefaultMaxTurns)
        {
            playerWon = false;
            if (session == null)
                return false;

            var selector = targetSelector ?? new GridBattleTargetSelector();
            var battleDriver = driver ?? new GridBattleDriver(session);
            battleDriver.BeginRound();

            int safety = maxTurns;
            while (!session.finished && safety-- > 0)
            {
                var actor = battleDriver.GetCurrentActor();
                if (actor == null)
                {
                    if (!battleDriver.AdvanceTurn())
                        battleDriver.BeginRound();
                    continue;
                }

                var opponents = actor.side == BattleSide.Ally
                    ? session.enemies
                    : (IReadOnlyList<BattleUnitRuntime>)session.allies;

                var target = selector.PickNormalAttackTarget(
                    actor, opponents, session.roundIndex, session.battleSeed);
                if (target == null)
                {
                    if (!battleDriver.AdvanceTurn())
                        battleDriver.BeginRound();
                    continue;
                }

                battleDriver.ApplyNormalAttack(actor, target);
                if (session.finished)
                    break;

                if (!battleDriver.AdvanceTurn())
                    battleDriver.BeginRound();
            }

            playerWon = session.playerWon;
            return session.finished;
        }
    }
}
