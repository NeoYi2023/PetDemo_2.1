// SPEC §12.14.6 / §12.14.6.1.1 / §12.14.8：多单位阵型战规则驱动（无 UI/动画）。
using System;
using System.Collections.Generic;

namespace PetDemo.Battle
{
    public sealed class GridBattleDriver : IGridBattleDriver
    {
        private readonly GridBattleSession session;
        private readonly IGridBattleResolver resolver;

        public GridBattleDriver(GridBattleSession session, IGridBattleResolver resolver = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.resolver = resolver ?? new GridBattleResolver();
        }

        public GridBattleSession GetSession() => session;

        public void BeginRound()
        {
            if (session.finished)
                return;

            session.turnQueue.Clear();
            session.turnCursor = 0;

            var living = new List<BattleUnitRuntime>();
            AddLiving(session.allies, living);
            AddLiving(session.enemies, living);
            if (living.Count == 0)
                return;

            living.Sort(CompareTurnOrder);
            session.turnQueue.AddRange(living);
        }

        public BattleUnitRuntime GetCurrentActor()
        {
            if (session.finished)
                return null;
            if (session.turnCursor < 0 || session.turnCursor >= session.turnQueue.Count)
                return null;
            var actor = session.turnQueue[session.turnCursor];
            return GridBattleLiving.IsLiving(actor) ? actor : null;
        }

        public void ApplyNormalAttack(BattleUnitRuntime attacker, BattleUnitRuntime defender)
        {
            if (session.finished || attacker == null || defender == null)
                return;
            if (!GridBattleLiving.IsLiving(attacker) || !GridBattleLiving.IsLiving(defender))
                return;

            int damage = resolver.ResolveNormalAttackDamage(attacker, defender);
            defender.stats.currentHp = Math.Max(0, defender.stats.currentHp - damage);
            if (defender.stats.currentHp <= 0)
                defender.isBattleDead = true;

            IsBattleFinished(out _);
        }

        public bool AdvanceTurn()
        {
            if (session.finished)
                return false;

            session.turnCursor++;
            if (session.turnCursor >= session.turnQueue.Count)
            {
                session.roundIndex++;
                return false;
            }
            return true;
        }

        public bool IsBattleFinished(out bool playerWon)
        {
            playerWon = false;
            if (session.finished)
            {
                playerWon = session.playerWon;
                return true;
            }

            bool enemiesDead = GridBattleLiving.AllDead(session.enemies);
            bool alliesDead = GridBattleLiving.AllDead(session.allies);

            if (enemiesDead)
            {
                session.playerWon = true;
                session.finished = true;
                playerWon = true;
                return true;
            }

            if (alliesDead)
            {
                session.playerWon = false;
                session.finished = true;
                playerWon = false;
                return true;
            }

            return false;
        }

        private int CompareTurnOrder(BattleUnitRuntime a, BattleUnitRuntime b)
        {
            int agilityCmp = b.stats.agility.CompareTo(a.stats.agility);
            if (agilityCmp != 0)
                return agilityCmp;

            int tieA = GridBattleSeedUtil.MixSeed(
                session.battleSeed,
                session.roundIndex * GridBattleSeedUtil.TieBreakRoundMultiplier,
                GridBattleSeedUtil.HashString(a.instanceId));
            int tieB = GridBattleSeedUtil.MixSeed(
                session.battleSeed,
                session.roundIndex * GridBattleSeedUtil.TieBreakRoundMultiplier,
                GridBattleSeedUtil.HashString(b.instanceId));
            return tieA.CompareTo(tieB);
        }

        private static void AddLiving(List<BattleUnitRuntime> source, List<BattleUnitRuntime> dest)
        {
            if (source == null)
                return;
            for (int i = 0; i < source.Count; i++)
            {
                if (GridBattleLiving.IsLiving(source[i]))
                    dest.Add(source[i]);
            }
        }
    }
}
