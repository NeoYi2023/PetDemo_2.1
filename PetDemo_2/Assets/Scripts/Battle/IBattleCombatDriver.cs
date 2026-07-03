// SPEC §12.11.10 (v3.172)：战斗数值/伤害/回合的极简驱动抽象。
// 用于把 InvasionBattleView 的回合循环与具体驱动方解耦：
//   - 全屏关卡战斗（§12.3/§12.9/§13.4）由 InvasionService 实现（携带体力/倒计时/自动连战等副作用）；
//   - InvasionBattleModal_2 的嵌入小战斗/BOSS 战由 LocalBattleCombatDriver 本地驱动（无任何全局副作用）。
using PetDemo.Core;
using UnityEngine;

namespace PetDemo.Battle
{
    /// <summary>SPEC §12.11.10：战斗回合循环所需的最小驱动接口。</summary>
    public interface IBattleCombatDriver
    {
        BattleSession GetBattleSession();
        void ApplyDamageToEnemy(int damage);
        void ApplyDamageToPlayer(int damage);
        void SetTurn(BattleTurn turn);
    }

    /// <summary>
    /// SPEC §12.11.10：嵌入战斗用的轻量本地驱动——仅持有一个 <see cref="BattleSession"/>，
    /// 内联伤害与结束判定（等价于 InvasionService 的对应逻辑），不产生体力/倒计时/存档等副作用。
    /// </summary>
    public sealed class LocalBattleCombatDriver : IBattleCombatDriver
    {
        private readonly BattleSession session;

        public LocalBattleCombatDriver(BattleSession session)
        {
            this.session = session;
        }

        public BattleSession GetBattleSession() => session;

        public void ApplyDamageToEnemy(int damage)
        {
            if (session == null)
                return;
            session.enemyHp = Mathf.Max(0, session.enemyHp - Mathf.Max(0, damage));
            CheckBattleEnd();
        }

        public void ApplyDamageToPlayer(int damage)
        {
            if (session == null)
                return;
            session.playerHp = Mathf.Max(0, session.playerHp - Mathf.Max(0, damage));
            CheckBattleEnd();
        }

        public void SetTurn(BattleTurn turn)
        {
            if (session != null)
                session.turn = turn;
        }

        private void CheckBattleEnd()
        {
            if (session == null)
                return;
            if (session.enemyHp <= 0)
            {
                session.playerWon = true;
                session.turn = BattleTurn.Result;
            }
            else if (session.playerHp <= 0)
            {
                session.playerWon = false;
                session.turn = BattleTurn.Result;
            }
        }
    }
}
