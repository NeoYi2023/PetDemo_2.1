// SPEC §12.14.6.2 / §12.14.13 / §12.14.16 阶段 5：战斗结束回写名册 HP 与胜后 30% 复活。
using System;

namespace PetDemo.Battle
{
    public static class RosterBattleSync
    {
        /// <summary>
        /// 将多单位战 ally 战后 HP 回写 <see cref="RunPartyRoster"/>。
        /// 胜：存活者保留战后 currentHp；本场死亡者复活为 max(1, floor(maxHp×0.3))。
        /// 负：不写回（由 UI 关闭本局）。
        /// </summary>
        public static void SyncRosterHpAfterBattle(
            RunPartyRoster roster,
            GridBattleSession session,
            bool playerWon)
        {
            if (!playerWon || roster?.members == null || session?.allies == null)
                return;

            for (int i = 0; i < session.allies.Count; i++)
            {
                var unit = session.allies[i];
                if (unit == null || string.IsNullOrEmpty(unit.rosterId))
                    continue;

                var entry = FindMember(roster, unit.rosterId);
                if (entry?.stats == null)
                    continue;

                if (unit.isBattleDead || unit.stats.currentHp <= 0)
                    entry.stats.currentHp = ComputeRevivalHp(entry.stats.maxHp);
                else
                    entry.stats.currentHp = Math.Min(unit.stats.currentHp, entry.stats.maxHp);
            }
        }

        /// <summary>SPEC §12.14.6.2：胜后死亡者复活 HP。</summary>
        public static int ComputeRevivalHp(int maxHp)
        {
            int safeMax = Math.Max(1, maxHp);
            return Math.Max(1, (int)Math.Floor(safeMax * 0.3));
        }

        private static RunAllyEntry FindMember(RunPartyRoster roster, string rosterId)
        {
            for (int i = 0; i < roster.members.Count; i++)
            {
                var member = roster.members[i];
                if (member != null && string.Equals(member.rosterId, rosterId, StringComparison.Ordinal))
                    return member;
            }
            return null;
        }
    }
}
